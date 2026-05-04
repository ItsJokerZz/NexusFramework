#include "headers.hpp"

using Handler = void (*)();

struct CommandEntry
{
  std::string_view name;
  Handler func;
};

bool g_is_library_request = false;
uint64_t g_api_total_requests = 0;
double g_api_total_latency_us = 0.0;
std::chrono::high_resolution_clock::time_point g_api_metrics_start_time =
    std::chrono::high_resolution_clock::now();

////////////////////////////////////////////////////////////////////
#include <stdio.h>
#include <string.h>
#include <time.h>

struct MonitorMemory
{
  int Used, Total;
  float Percent;
};

struct MonitorThreadData
{
  timespec time;
  int Count;
  process_stats Threads[3072];
};

static MonitorThreadData Global_Prev = {};
static MonitorThreadData Global_Curr = {};

void UpdateSystemStats(float usage_out[8], float &average, int &threads)
{
  // 1. Snapshot current thread states
  Global_Curr.Count = 3072;
  if (sceKernelGetCpuUsage((process_stats *)&Global_Curr.Threads,
                           &Global_Curr.Count) != 0)
  {
    return;
  }
  sceKernelClockGettime(4, &Global_Curr.time);

  // Update thread count output
  threads = Global_Curr.Count;
  average = 0.0f;

  // 2. Calculate time delta between snapshots
  double dt = (Global_Curr.time.tv_sec - Global_Prev.time.tv_sec) +
              (Global_Curr.time.tv_nsec - Global_Prev.time.tv_nsec) / 1e9;

  // 3. Process Core Usage (requires a previous snapshot)
  if (Global_Prev.Count > 0 && dt > 0)
  {
    for (int core = 0; core < 8; core++)
    {
      process_stats *cur_p = nullptr, *prev_p = nullptr;
      char target[16];
      snprintf(target, sizeof(target), "SceIdleCpu%d", core);

      // Find idle thread for this core in current snapshot
      for (int i = 0; i < Global_Curr.Count; i++)
      {
        char name[64];
        if (sceKernelGetThreadName(Global_Curr.Threads[i].td_tid, name) == 0 &&
            strcmp(name, target) == 0)
        {
          cur_p = &Global_Curr.Threads[i];
          break;
        }
      }

      // Match TID in previous snapshot
      if (cur_p)
      {
        for (int i = 0; i < Global_Prev.Count; i++)
        {
          if (Global_Prev.Threads[i].td_tid == cur_p->td_tid)
          {
            prev_p = &Global_Prev.Threads[i];
            break;
          }
        }
      }

      // Calculate Idle % and invert it for Usage %
      if (cur_p && prev_p)
      {
        double idle_diff = (cur_p->system_cpu_usage_time.tv_sec -
                            prev_p->system_cpu_usage_time.tv_sec) +
                           (cur_p->system_cpu_usage_time.tv_nsec -
                            prev_p->system_cpu_usage_time.tv_nsec) /
                               1e9;

        float usage = (1.0f - (float)(idle_diff / dt)) * 100.0f;
        usage_out[core] = (usage < 0) ? 0 : (usage > 100) ? 100
                                                          : usage;
        average += usage_out[core] / 8.0f;
      }
    }
  }

  // 4. Cycle snapshots
  Global_Prev = Global_Curr;
}

MonitorMemory GetMemory(int type)
{
  int total = 0, free = 0;
  if (get_page_table_stats(1, type, &total, &free) != -1)
  {
    float p =
        (total > 0) ? ((float)(total - free) / (float)total) * 100.0f : 0.0f;
    return {total - free, total, p};
  }
  return {0, 0, 0.0f};
}

// --- Usage Example ---
void log_all_stats()
{
  float cores[8] = {0}, avg = 0;
  int threads = 0;

  UpdateSystemStats(cores, avg, threads);
  MonitorMemory ram = GetMemory(1);
  MonitorMemory vram = GetMemory(2);

  log_message("\n--- System Status ---\n");
  log_message("Threads: %d | CPU Avg: %.2f%%\n", threads, avg);
  log_message("RAM Usage:  %d / %d MB (%.1f%%)\n", ram.Used, ram.Total,
              ram.Percent);
  log_message("VRAM Usage: %d / %d MB (%.1f%%)\n", vram.Used, vram.Total,
              vram.Percent);
}
////////////////////////////////////////////////////////////////////

#define MIN_HOOK_LENGTH 14
void *pt_detour_function(pid_t pid, uint64_t address, void *destination, intptr_t trampoline_hint = 0)
{
  if (!address || !destination)
  {
    log_message("[Detour] Error: Null arguments (Addr: 0x%llX, Dest: 0x%p)\n", address, destination);
    return NULL;
  }

  log_message("--- Starting Detour Session (PID: %d) ---\n", pid);
  log_message("[Detour] Target: 0x%llX | Redirect: 0x%p\n", address, destination);

  pt_attach(pid);

  // --- Calc hook length ---
  size_t hook_len = 0;
  {
    uint8_t buffer[64];
    memset(buffer, 0, sizeof(buffer));
    log_message("[Detour] Analyzing instructions at 0x%llX...\n", address);

    if (pt_copyout(pid, address, buffer, sizeof(buffer)) != 0)
    {
      log_message("[Detour] Error: pt_copyout failed during disassembly.\n");
      pt_detach(pid, 0);
      return NULL;
    }

    while (hook_len < MIN_HOOK_LENGTH)
    {
      hde64s hs;
      uint32_t len = hde64_disasm((void *)(buffer + hook_len), &hs);

      if ((hs.flags & F_ERROR || len == 0) && (buffer[hook_len] == 0xC5 || buffer[hook_len] == 0xC4))
      {
        uint8_t p1 = buffer[hook_len];
        uint8_t p2 = buffer[hook_len + 1];
        log_message("[Detour] AVX Detected: %02X %02X. Attempting manual length parse...\n", p1, p2);

        if (p1 == 0xC5 && p2 == 0xF8 && buffer[hook_len + 2] == 0x77)
        {
          len = 3;
          log_message("[Detour] Identified VZEROUPPER (3 bytes)\n");
        }
        else if (p1 == 0xC5)
        {
          len = 4;
          log_message("[Detour] Warning: Guessing 2-byte VEX length as 4 bytes. This is risky.\n");
        }
        else
        {
          log_message("[Detour] CRITICAL: Unsupported AVX instruction. Aborting to prevent crash.\n");
          pt_detach(pid, 0);
          return NULL;
        }
      }

      if ((hs.flags & F_ERROR || len == 0) && hook_len < MIN_HOOK_LENGTH)
      {
        log_message("[Detour] Disassembler error at +%zu (Bytes: %02X %02X %02X)\n",
                    hook_len, buffer[hook_len], buffer[hook_len + 1], buffer[hook_len + 2]);
        pt_detach(pid, 0);
        return NULL;
      }

      log_message("[Detour] Instruction at +%zu: len=%u (Opcode: %02X)\n", hook_len, len, buffer[hook_len]);
      hook_len += len;

      if (hook_len >= sizeof(buffer))
      {
        log_message("[Detour] Error: Buffer overflow during disassembly.\n");
        pt_detach(pid, 0);
        return NULL;
      }
    }
  }

  if (hook_len < MIN_HOOK_LENGTH || hook_len > 32)
  {
    log_message("[Detour] Error: Instruction boundary check failed (Length: %zu, Required: %d)\n",
                hook_len, MIN_HOOK_LENGTH);
    pt_detach(pid, 0);
    return NULL;
  }
  log_message("[Detour] Calculated safe hook length: %zu bytes\n", hook_len);

  // --- Trampoline allocation ---
  intptr_t remote_trampoline;
  if (trampoline_hint > 0)
  {
    log_message("[Detour] Using caller-supplied trampoline at 0x%llX\n", (uint64_t)trampoline_hint);
    remote_trampoline = trampoline_hint;
  }
  else
  {
#ifdef __PROSPERO__
    log_message("[Detour] PS5: Allocating RW trampoline memory...\n");
    remote_trampoline = pt_mmap(pid, 0, PAGE_SIZE, PROT_READ | PROT_WRITE, MAP_ANONYMOUS | MAP_PRIVATE, -1, 0);
#else
    log_message("[Detour] PS4: Allocating RWX trampoline memory...\n");
    remote_trampoline = pt_mmap(pid, 0, PAGE_SIZE, PROT_RWX, MAP_ANONYMOUS | MAP_PRIVATE, -1, 0);
#endif
  }

  if (remote_trampoline <= 0)
  {
    log_message("[Detour] Error: trampoline memory unavailable (Status: %lld)\n", (long long)remote_trampoline);
    pt_detach(pid, 0);
    return NULL;
  }
  log_message("[Detour] Trampoline at 0x%llX\n", (uint64_t)remote_trampoline);

  // --- Copy stolen bytes into trampoline ---
  uint8_t local_tramp_buf[64];
  memset(local_tramp_buf, 0, sizeof(local_tramp_buf));
  log_message("[Detour] Relocating %zu bytes from 0x%llX to trampoline\n", hook_len, address);
  pt_copyout(pid, address, local_tramp_buf, hook_len);
  pt_copyin(pid, local_tramp_buf, remote_trampoline, hook_len);

  // --- Write return jump at end of trampoline ---
  uint64_t jump_back_addr = address + hook_len;
  log_message("[Detour] Writing trampoline return jump to 0x%llX\n", jump_back_addr);
  {
    uint8_t jump_proto[MIN_HOOK_LENGTH] = {
        0xFF, 0x25, 0x00, 0x00, 0x00, 0x00,
        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00};
    *(uint64_t *)(jump_proto + 6) = jump_back_addr;

    uint64_t tramp_jump_addr = remote_trampoline + hook_len;
    uint64_t tramp_page_base = tramp_jump_addr & ~(uint64_t)0xFFF;
    size_t tramp_prot_size = (tramp_jump_addr - tramp_page_base) + MIN_HOOK_LENGTH;

#ifdef __PROSPERO__
    log_message("[Detour] PS5: kernel_mprotect(0x%llX, %zu) PROT_RWX\n", tramp_page_base, tramp_prot_size);
    kernel_mprotect(pid, tramp_page_base, tramp_prot_size, PROT_RWX);
#else
    int mprot_res = pt_mprotect(pid, tramp_page_base, tramp_prot_size, PROT_RWX);
    log_message("[Detour] PS4: pt_mprotect(0x%llX, %zu) = %d\n", tramp_page_base, tramp_prot_size, mprot_res);
#endif

    int res = pt_copyin(pid, jump_proto, tramp_jump_addr, MIN_HOOK_LENGTH);
    if (res != 0)
      log_message("[Detour] CRITICAL: pt_copyin failed during return jump write (Result: %d)\n", res);
  }

#ifdef __PROSPERO__
  log_message("[Detour] PS5: Finalizing trampoline permissions to RWX...\n");
  kernel_mprotect(pid, remote_trampoline, PAGE_SIZE, PROT_RWX);
#endif

  // --- Patch target function ---
  log_message("[Detour] Patching target function entry point...\n");
  {
    uint64_t page_base = address & ~(uint64_t)0xFFF;
    size_t prot_size = (address - page_base) + hook_len;

#ifdef __PROSPERO__
    kernel_mprotect(pid, page_base, prot_size, PROT_RWX);
#else
    pt_mprotect(pid, page_base, prot_size, PROT_RWX);
#endif

    if (hook_len > MIN_HOOK_LENGTH)
    {
      size_t padding = hook_len - MIN_HOOK_LENGTH;
      uint8_t nops[32];
      memset(nops, 0x90, sizeof(nops));
      pt_copyin(pid, nops, address + MIN_HOOK_LENGTH, padding);
      log_message("[Detour] Padded %zu NOP(s) for instruction alignment at 0x%llX\n",
                  padding, address + MIN_HOOK_LENGTH);
    }

    uint8_t jump_proto[MIN_HOOK_LENGTH] = {
        0xFF, 0x25, 0x00, 0x00, 0x00, 0x00,
        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00};
    *(uint64_t *)(jump_proto + 6) = (uint64_t)destination;
    log_message("[Detour] Writing absolute jump: 0x%llX -> 0x%llX\n", address, (uint64_t)destination);

    int res = pt_copyin(pid, jump_proto, address, MIN_HOOK_LENGTH);
    if (res != 0)
      log_message("[Detour] CRITICAL: pt_copyin failed during hook write (Result: %d)\n", res);
  }

  log_message("[Detour] SUCCESS: Hook applied. Trampoline: 0x%llX\n", (uint64_t)remote_trampoline);
  log_message("--- Detour Session Complete ---\n");

  pt_detach(pid, 0);

  return (void *)remote_trampoline;
}

constexpr CommandEntry commands[] = {
    {"GET /test",
     []
     {
       // inject_elf_(get_running_app_pid(),
       // get_elf_bytes("/data/PS5HEN/assets/fps_counter.elf"));

       inject_elf_ps5(get_running_app_pid(),
                      get_elf_bytes("/data/hello_world.elf"));

       ///////////////////////////////
       send_response(SUCCESS_MESSAGE);
     }},

    {"POST /install_shellcode",
     []
     {
       std::string pid_param = extract_param("pid", false);
       std::string size_param = extract_param("size", false);
       std::string data_param = extract_param("data", false);

       char preview_buf[128];
       snprintf(preview_buf, sizeof(preview_buf),
                "install_shellcode: pid=[%s] size=[%s] data_len=%zu "
                "data_preview=[%.32s]",
                pid_param.c_str(), size_param.c_str(), data_param.size(),
                data_param.c_str());
       log_message(preview_buf);

       if (data_param.empty() || (data_param.size() & 1))
       {
         char err_buf[64];
         snprintf(err_buf, sizeof(err_buf),
                  "install_shellcode: INVALID_ARGS data_len=%zu",
                  data_param.size());
         log_message(err_buf);
         return send_error_response(INVALID_ARGS);
       }

       size_t code_len = data_param.size() / 2;
       uint8_t *shellcode = (uint8_t *)malloc(code_len);
       if (!shellcode)
         return send_error_response(UNKNOWN_ERROR);

       for (size_t i = 0; i < code_len; ++i)
       {
         char byte_str[3] = {data_param[i * 2], data_param[i * 2 + 1], '\0'};
         shellcode[i] = (uint8_t)strtol(byte_str, nullptr, 16);
       }

       pid_t pid = pid_param.empty() ? get_running_app_pid()
                                     : (pid_t)std::stoul(pid_param, nullptr, 0);

       size_t alloc_size = size_param.empty()
                               ? code_len
                               : (size_t)std::stoull(size_param, nullptr, 0);
       alloc_size = (alloc_size + 0xFFF) & ~0xFFFULL;

       char alloc_buf[64];
       snprintf(alloc_buf, sizeof(alloc_buf),
                "install_shellcode: code_len=%zu alloc_size=%zu pid=%d",
                code_len, alloc_size, pid);
       log_message(alloc_buf);

       int attach_res = pt_attach(pid);
       if (attach_res != 0)
       {
         char attach_buf[64];
         snprintf(attach_buf, sizeof(attach_buf),
                  "install_shellcode: pt_attach failed errno=%d", errno);
         log_message(attach_buf);
         free(shellcode);
         return send_error_response(UNKNOWN_ERROR);
       }

#ifdef __PROSPERO__
       intptr_t remote_addr =
           pt_mmap(pid, 0, alloc_size, PROT_READ | PROT_WRITE,
                   MAP_PRIVATE | MAP_ANONYMOUS, -1, 0);
#else
       intptr_t remote_addr = pt_mmap(pid, 0, alloc_size, PROT_RWX,
                                      MAP_PRIVATE | MAP_ANONYMOUS, -1, 0);
#endif

       char mmap_buf[64];
       snprintf(mmap_buf, sizeof(mmap_buf),
                "install_shellcode: pt_mmap=0x%llX errno=%d",
                (unsigned long long)remote_addr, errno);
       log_message(mmap_buf);

       if (remote_addr <= 0)
       {
         free(shellcode);
         pt_detach(pid, 0);
         return send_error_response(UNKNOWN_ERROR);
       }

       int write_res = pt_copyin(pid, shellcode, remote_addr, code_len);
       free(shellcode);

       char write_buf[64];
       snprintf(write_buf, sizeof(write_buf),
                "install_shellcode: pt_copyin=%d errno=%d", write_res, errno);
       log_message(write_buf);

       if (write_res != 0)
       {
         pt_munmap(pid, remote_addr, alloc_size);
         pt_detach(pid, 0);
         return send_error_response(UNKNOWN_ERROR);
       }

#ifdef __PROSPERO__
       kernel_mprotect(pid, remote_addr, alloc_size, PROT_RWX);
#endif

       uint8_t verify[16] = {};
       pt_copyout(pid, remote_addr, verify, sizeof(verify));
       char log_buf[192];
       snprintf(log_buf, sizeof(log_buf),
                "install_shellcode: @ 0x%llX  "
                "%02X %02X %02X %02X %02X %02X %02X %02X "
                "%02X %02X %02X %02X %02X %02X %02X %02X",
                (unsigned long long)remote_addr, verify[0], verify[1],
                verify[2], verify[3], verify[4], verify[5], verify[6],
                verify[7], verify[8], verify[9], verify[10], verify[11],
                verify[12], verify[13], verify[14], verify[15]);
       log_message(log_buf);

       pt_detach(pid, 0);

       char response_buffer[64];
       snprintf(response_buffer, sizeof(response_buffer), "0x%llX",
                (unsigned long long)remote_addr);
       send_response(response_buffer);
     }},

    {"GET /detour_method",
     []
     {
       std::string pid_param = extract_param("pid");
       std::string address_param = extract_param("address");
       std::string destination_param = extract_param("destination");
       std::string trampoline_param = extract_param("trampoline"); // new

       if (address_param.empty() || destination_param.empty())
         return send_error_response(INVALID_ARGS);

       pid_t pid = pid_param.empty() ? get_running_app_pid()
                                     : (pid_t)std::stoul(pid_param, nullptr, 0);

       uint64_t address = std::stoull(address_param, nullptr, 0);
       uint64_t destination = std::stoull(destination_param, nullptr, 0);
       intptr_t trampoline_hint = trampoline_param.empty() ? 0
                                                           : (intptr_t)std::stoull(trampoline_param, nullptr, 0);

       void *tramp = pt_detour_function(pid, address, (void *)destination, trampoline_hint);

       if (tramp == nullptr)
         return send_error_response(UNKNOWN_ERROR);

       char response_buffer[64];
       snprintf(response_buffer, sizeof(response_buffer), "0x%llX",
                (unsigned long long)tramp);

       send_response(response_buffer);
     }},

#pragma region System Connection
    {"GET /status", connection::status},

    /* {"GET /setup", connection::setup}, */

    {"GET /version", connection::version},

    {"GET /connect", connection::connect},

    {"GET /unload", connection::unload},

    {"GET /disconnect", connection::disconnect},

    {"GET /metrics", connection::metrics},

#pragma endregion

#pragma region System Information
    {"GET /get_sys_info", []
     { handle_command(sys_info::get_all); }},

    {"GET /get_sys_ids", []
     { handle_command(sys_info::get_ids); }},

    {"GET /get_console_name", []
     { handle_command(sys_info::get_name); }},

    {"GET /get_fw_version", []
     { handle_command(sys_info::get_fw); }},

    {"GET /get_sys_type", []
     { handle_command(sys_info::get_type); }},

    {"GET /get_sys_model", []
     { handle_command(sys_info::get_model); }},

    {"GET /get_disk_info", []
     { handle_command(sys_info::get_disk_info); }},

    {"GET /get_temperature", []
     { handle_command(sys_info::get_temp); }},

    {"GET /get_username", []
     { handle_command(sys_info::get_user); }},

    {"GET /get_sys_uptime", []
     { handle_command(sys_info::get_uptime); }},

    {"GET /get_sys_freq", []
     { handle_command(sys_info::get_cpu_freq); }},

    {"GET /get_sys_ip", []
     { handle_command(sys_info::get_ip); }},

#pragma endregion

#pragma region System Control
    {"GET /fan_threshold", []
     { handle_command(sys_control::fan_threshold); }},

    {"GET /ring_buzzer", []
     { handle_command(sys_control::ring_buzzer); }},

    {"GET /send_notify", []()
     { handle_command(sys_control::send_notify); }},

    {"GET /launch_app", []()
     { handle_command(sys_control::launch_app); }},

    {"GET /launch_uri", []()
     { handle_command(sys_control::launch_uri); }},

    {"GET /system_state", []()
     { handle_command(sys_control::system_state); }},

    {"GET /exit_app", []()
     { handle_command(sys_control::exit_app); }},

    {"GET /log_message", []()
     { handle_command(sys_control::log_message); }},

    {"GET /dump_kernel", []()
     { handle_command(sys_control::dump_kernel); }},

#pragma endregion

#pragma region Process Info/Control
    {"GET /get_proc_list", []
     { handle_command(process::get_list); }},

    {"GET /get_proc_info", []
     { handle_command(process::get_info); }},

    {"GET /get_vm_maps", []
     { handle_command(process::get_vm_maps); }},

    {"GET /load_elf",
     []
     {
       handle_command(process::load_elf);
     }}, // allow for post as well so we can provde the bytes remotely

    {"GET /unload_elf", []
     { handle_command(process::unload_elf); }},

    {"GET /kill_process", []
     { handle_command(process::kill_process); }},

    {"GET /suspend_process", []
     { handle_command(process::suspend_process); }},

    {"GET /resume_process", []
     { handle_command(process::resume_process); }},

    {"GET /read_memory", []
     { handle_command(process::read_memory); }},

    {"POST /write_memory", []
     { handle_command(process::write_memory); }},

    {"GET /allocate_memory", []
     { handle_command(process::allocate_memory); }},

    {"GET /free_memory", []
     { handle_command(process::free_memory); }},

    {"GET /memory_protection",
     []
     { handle_command(process::memory_protection); }},

    {"GET /load_module", []
     { handle_command(process::load_module); }},

    {"GET /unload_module", []
     { handle_command(process::unload_module); }},

    {"GET /get_module_handle",
     []
     { handle_command(process::get_module_handle); }},

    {"GET /get_all_modules", []
     { handle_command(process::get_all_modules); }},

    {"GET /resolve_symbol", []
     { handle_command(process::resolve_symbol); }},

    {"GET /aob_scan", []
     { handle_command(process::aob_scan); }},

    {"POST /rpc_call", []
     { handle_command(process::rpc_call); }},

#pragma endregion

};

Handler find_handler(std::string_view cmd)
{
  for (const auto &entry : commands)
  {
    if (entry.name == cmd)
      return entry.func;
  }
  return nullptr;
}

void handle_request()
{
  std::string_view buf = server.buffer;
  size_t http_pos = buf.find(" HTTP/");
  std::string_view command = buf.substr(0, http_pos);

  size_t hash_pos = command.find('#');
  if (hash_pos != std::string_view::npos)
    command = command.substr(0, hash_pos);

  size_t q_pos = command.find('?');
  if (q_pos != std::string_view::npos)
    command = command.substr(0, q_pos);

  Handler func = find_handler(command);
  if (!func)
    return send_error_response(INVALID_CMD);

  if (buf.compare(0, 7, "OPTIONS") == 0)
  {
    func();
    return;
  }

  auto start = std::chrono::high_resolution_clock::now();
  func();
  auto end = std::chrono::high_resolution_clock::now();

  double duration =
      std::chrono::duration<double, std::micro>(end - start).count();

  double raw_duration_us = duration; // Preserve unscaled duration for metrics

  /* const char *unit;

  if (duration < 1000.0) {
    unit = "microseconds";
  } else if (duration < 1'000'000.0) {
    unit = "milliseconds";
    duration /= 1000.0;
  } else {
    unit = "seconds";
    duration /= 1'000'000.0;
  }

   log_message("Endpoint '%.*s' responded in %.3f %s",
   static_cast<int>(command.length()), command.data(), duration, unit);
   */

  log_message("Endpoint '%.*s' requested!",
              static_cast<int>(command.length()), command.data());

  g_api_total_requests++;
  g_api_total_latency_us += raw_duration_us;
}

void handle_client()
{
  constexpr bool RESTRICT_CONNECTIONS = false;

  if (RESTRICT_CONNECTIONS &&
      server.sockets.client_addr.sin_addr.s_addr != htonl(INADDR_LOOPBACK))
  {
    shutdown(server.sockets.client, SHUT_RDWR);
    return;
  }

  /*
    char client_ip[INET_ADDRSTRLEN];
  inet_ntop(AF_INET, &(server.sockets.client_addr.sin_addr), client_ip,
            sizeof(client_ip));
  uint16_t client_port = ntohs(server.sockets.client_addr.sin_port);

  log_message("Client connected: %s:%d", client_ip, client_port);
  */

  server.buffer.clear();

  char chunk[4096];
  ssize_t received;

  while ((received = recv(server.sockets.client, chunk, sizeof(chunk), 0)) >
         0)
  {
    server.buffer.append(chunk, received);

    size_t header_end = server.buffer.find("\r\n\r\n");
    if (header_end != std::string::npos)
    {
      if (server.buffer.compare(0, 5, "POST ") != 0)
      {
        break;
      }

      size_t cl_pos = server.buffer.find("Content-Length:");
      if (cl_pos != std::string::npos)
      {
        size_t cl_end = server.buffer.find("\r\n", cl_pos);
        int content_length = std::stoi(
            server.buffer.substr(cl_pos + 15, cl_end - (cl_pos + 15)));

        size_t body_received = server.buffer.size() - (header_end + 4);
        while (body_received < static_cast<size_t>(content_length))
        {
          received = recv(server.sockets.client, chunk, sizeof(chunk), 0);
          if (received <= 0)
            break;
          server.buffer.append(chunk, received);
          body_received += received;
        }
      }
      break;
    }
  }

  if (server.buffer.empty())
  {
    shutdown(server.sockets.client, SHUT_RDWR);
    return;
  }

  g_is_library_request = server.buffer.find("#library") != std::string::npos;

  if (server.buffer.compare(0, 7, "OPTIONS") == 0)
    send(server.sockets.client, RESPONSE_OPTIONS, strlen(RESPONSE_OPTIONS), 0);
  else if (server.buffer.compare(0, 14, "GET / HTTP/1.1") == 0 ||
           server.buffer.compare(0, 15, "POST / HTTP/1.1") == 0)
    connection::status();
  else
    handle_request();

  // log_message("Client disconnected: %s:%d", client_ip, client_port);
}
