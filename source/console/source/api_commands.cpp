#include "headers.hpp"

extern uint64_t g_api_total_requests;
extern double g_api_total_latency_us;
extern std::chrono::high_resolution_clock::time_point g_api_metrics_start_time;

namespace cmds
{

  namespace connection
  {
    void status()
    {
      using namespace std::chrono;
      auto now = steady_clock::now();
      auto secs = duration_cast<seconds>(now - start_time).count();

      std::ostringstream uptime;
      uptime << "Server is running. Uptime: " << (secs / 86400) << "D "
             << ((secs % 86400) / 3600) << "H " << ((secs % 3600) / 60) << "M "
             << (secs % 60) << "S";

      send_response(uptime.str());
    }

    void setup()
    {
      json info;
      info["NAME"] = g_system_info.name();
      info["FW"] = g_system_info.firmware_str();
      info["TYPE"] = g_system_info.type();
      info["SYS"] = g_system_info.is_ps5() ? "PS5" : "PS4";
      info["HEN"] = g_system_info.hen_name();

      send_response(info);
    }

    void version()
    {
      json info;
      info["DATE"] = std::string(__DATE__);
      info["VERSION"] = std::string(VERSION);
      info["NUMBER"] = BUILD;
      send_response(info);
    }

    void connect()
    {
      connected = true;
      send_response(SUCCESS_MESSAGE);
    }

    void metrics()
    {
      auto now = std::chrono::high_resolution_clock::now();
      double time_elapsed =
          std::chrono::duration<double>(now - g_api_metrics_start_time).count();

      double rps =
          (time_elapsed > 0.0) ? (g_api_total_requests / time_elapsed) : 0.0;
      double avg_latency = (g_api_total_requests > 0)
                               ? (g_api_total_latency_us / g_api_total_requests)
                               : 0.0;

      json info;
      info["REQUESTS"] = g_api_total_requests;
      info["RPS"] = rps;
      info["LATENCY"] = avg_latency;
      info["UPTIME"] = time_elapsed;
      info["CONNECTED"] = connected;

      send_response(info);
    }

    void unload()
    {
      send_response(SUCCESS_MESSAGE);

      if (server.sockets.client >= 0)
      {
        shutdown(server.sockets.client, SHUT_RDWR);
        close(server.sockets.client);
        server.sockets.client = -1;
      }

      if (server.sockets.server >= 0)
      {
        shutdown(server.sockets.server, SHUT_RDWR);
        close(server.sockets.server);
        server.sockets.server = -1;
      }

      if (server.threads.client != nullptr)
      {
        pthread_cancel(server.threads.client);
        pthread_join(server.threads.client, nullptr);
        server.threads.client = nullptr;
      }

      if (server.threads.server != nullptr)
      {
        pthread_cancel(server.threads.server);
        pthread_join(server.threads.server, nullptr);
        server.threads.server = nullptr;
      }

      if (!server.buffer.empty())
      {
        std::fill(server.buffer.begin(), server.buffer.end(), 0);
        server.buffer.clear();
      }

      memset(&server.sockets.server_addr, 0, sizeof(server.sockets.server_addr));
      memset(&server.sockets.client_addr, 0, sizeof(server.sockets.client_addr));
      server.sockets.client_addr_len = sizeof(struct sockaddr_in);

      connected = false;
      unloaded = true;
    }

    void disconnect()
    {
      send_response(SUCCESS_MESSAGE);

      if (server.sockets.client >= 0)
      {
        shutdown(server.sockets.client, SHUT_RDWR);
        close(server.sockets.client);
        server.sockets.client = -1;
      }

      connected = false;
    }

  }

  namespace sys_info
  {
    void get_ids()
    {
      json info;

      info["IDPS"] = g_system_info.idps();
      info["PSID"] = g_system_info.psid();

      send_response(info);
    }

    void get_name() { send_response(g_system_info.name()); }

    void get_fw() { send_response(g_system_info.firmware_str()); }

    void get_type() { send_response(g_system_info.type()); }

    void get_model() { send_response(g_system_info.model()); }

    void get_temp()
    {

      json info;

      auto temps = g_system_info.get_temperatures();

      info["CPU"] = temps.first;
      info["SOC"] = temps.second;

      send_response(info);
    }

    void get_user() { send_response(g_system_info.get_username()); }

    void get_uptime() { send_response(g_system_info.get_uptime()); }

    void get_ip() { send_response(g_system_info.get_ip_address()); }

    void get_cpu_freq() { send_response(g_system_info.get_cpu_frequency()); }

    void get_disk_info()
    {
      nlohmann::json info;

      info["%"] = g_system_info.get_disk_info(disk_info::PERCENT_USED);
      info["TOTAL"] = g_system_info.get_disk_info(disk_info::TOTAL_SPACE);
      info["USED"] = g_system_info.get_disk_info(disk_info::USED_SPACE);
      info["FREE"] = g_system_info.get_disk_info(disk_info::FREE_SPACE);

      send_response(info);
    }

    void get_all()
    {
      json info;

      info["IDPS"] = g_system_info.idps();
      info["PSID"] = g_system_info.psid();
      info["NAME"] = g_system_info.name();
      info["FW"] = g_system_info.firmware_str();
      info["TYPE"] = g_system_info.type();
      info["MODEL"] = g_system_info.model();
      info["USER"] = g_system_info.get_username();
      info["UPTIME"] = g_system_info.get_uptime();
      info["IP"] = g_system_info.get_ip_address();
      info["CPU_FREQ"] = g_system_info.get_cpu_frequency();

      nlohmann::json temp_info;
      auto temps = g_system_info.get_temperatures();
      temp_info["CPU"] = temps.first;
      temp_info["SOC"] = temps.second;

      info["TEMPS"] = temp_info;

      nlohmann::json disk_info;
      disk_info["%"] = g_system_info.get_disk_info(disk_info::PERCENT_USED);
      disk_info["TOTAL"] = g_system_info.get_disk_info(disk_info::TOTAL_SPACE);
      disk_info["USED"] = g_system_info.get_disk_info(disk_info::USED_SPACE);
      disk_info["FREE"] = g_system_info.get_disk_info(disk_info::FREE_SPACE);

      info["DISK"] = disk_info;

      send_response(info);
    }

  }

  namespace sys_control
  {
    void fan_threshold()
    {
      uint8_t limit = 0;
      std::string limit_str = extract_param("limit");

      if (!limit_str.empty())
      {
        if (sscanf(limit_str.c_str(), "%hhu", &limit) != 1)
          return send_error_response(INVALID_ARGS);

        if (limit < 50)
          limit = 50;

        if (limit > 85)
          limit = 85;

        int fd = open("/dev/icc_fan", O_RDONLY, 0);
        if (fd < 0)
          return;

        char data[10] = {0x00, 0x00, 0x00, 0x00, 0x00, static_cast<char>(limit),
                         0x00, 0x00, 0x00, 0x00};

        ioctl(fd, 0xC01C8F07, data);
        close(fd);

        send_response(SUCCESS_MESSAGE);
      }
      else
        send_error_response(INVALID_ARGS);
    }

    void ring_buzzer()
    {
      std::string type_str = extract_param("type");
      if (type_str.empty())
        return;

      int type = std::stoul(type_str, nullptr, 0);
      useconds_t delay = 200000;

      if (type == -1) // continuous
        ::ring_buzzer(6);
      else if (type == 0) // stop
        ::ring_buzzer(0);
      else if (type > 0) // dynamic
      {
        for (int i = 0; i < type; ++i)
        {
          ::ring_buzzer(1);
          if (i + 1 < type)
            usleep(delay);
        }
      }

      send_response(SUCCESS_MESSAGE);
    }

    void send_notify()
    {
      std::string message = decode_url(extract_param("msg"));
      if (message.empty())
        return send_error_response(INVALID_ARGS);
      else
      {
        notify_debug(message.c_str());
        send_response(SUCCESS_MESSAGE);
      }
    }

    void launch_app()
    {
      std::string titleId = extract_param("tid");

      if (titleId.empty())
        return send_error_response(INVALID_ARGS);

      launch_app_params params;
      memset(&params, 0, sizeof(params));
      params.size = sizeof(launch_app_params);
      params.app_option = 0;
      params.crash_report = 0;
      params.check_flag = 2; // SkipSystemUpdate

      sceUserServiceGetForegroundUser(&params.user_id);

      const char *argv[] = {NULL};
      int ret = sceSystemServiceLaunchApp(titleId.c_str(), argv, &params);

      ::log_message("sceSystemServiceLaunchApp returned: 0x%X", ret);

      if (ret < 0)
        return send_error_response(UNKNOWN_ERROR);

      send_response(SUCCESS_MESSAGE);
    }

    void launch_uri()
    {
      std::string uri = decode_url(extract_param("uri"));

      if (uri.empty())
        return send_error_response(INVALID_ARGS);

      launch_uri_params params;
      params.size = sizeof(launch_uri_params);
      sceUserServiceGetForegroundUser(&params.user_id);
      sceShellUIUtilLaunchByUri(uri.data(), &params);

      send_response(SUCCESS_MESSAGE);
    }

    void system_state()
    {
      std::string action = extract_param("action");

      if (action.empty())
        return send_error_response(INVALID_ARGS);

      if (action == "standby")
        sceSystemStateMgrEnterStandby();
      else if (action == "reboot")
        sceSystemStateMgrReboot();
      else if (action == "turnoff")
        sceSystemStateMgrTurnOff();
      else
        return send_error_response(INVALID_ARGS);

      send_response(SUCCESS_MESSAGE);
    }

    void exit_app()
    {
      pid_t pid = get_running_app_pid();

      if (no_open_app_response())
        return;

      if (pid > 0)
        kill(pid, SIGKILL);

      send_response(SUCCESS_MESSAGE);
    }

    void log_message()
    {

      std::string msg_param = decode_url(extract_param("msg"));

      if (msg_param.empty())
        return send_error_response(INVALID_ARGS);

      ::log_message(msg_param.c_str());

      send_response(SUCCESS_MESSAGE);
    }

    void dump_kernel()
    {
#ifdef __PROSPERO__
      return send_error_response(PS4_ONLY);
#else
      uint8_t buf[0x4000];
      size_t len;

      int fd = open("/data/kernel.elf", O_WRONLY | O_CREAT | O_TRUNC, 0777);
      if (fd < 0)
        return send_error_response(UNKNOWN_ERROR);

      for (size_t i = 0; i < KERNEL_IMAGE_SIZE; i += sizeof(buf))
      {
        len = KERNEL_IMAGE_SIZE - i;
        if (len > sizeof(buf))
          len = sizeof(buf);

        if (kernel_copyout(KERNEL_ADDRESS_IMAGE_BASE + i, buf, len))
        {
          close(fd);
          return send_error_response(UNKNOWN_ERROR);
        }

        if (write(fd, buf, len) != len)
        {
          close(fd);
          return send_error_response(UNKNOWN_ERROR);
        }
      }

      close(fd);

      notify_debug("Dumped kernel to /data/kernel.elf");

      send_response(SUCCESS_MESSAGE);
#endif
    }

  }

  namespace process
  {
    void get_list()
    {
      std::vector<process_info> list = get_proc_list();

      json info;
      info["COUNT"] = list.size();

      json array = json::array();

      for (const auto &entry : list)
      {
        json object;
        object["AID"] = entry.app_id;
        object["TID"] = entry.tid;
        object["EXEC"] = entry.exec;
        object["PID"] = entry.process_id;

        array.push_back(object);
      }

      info["LIST"] = array;

      send_response(info);
    }

    void get_info()
    {
      static const std::regex ps1(
          R"(^(SLES|SCES|SCED|SLUS|SCUS|SLPS|SLPM|SCPS|SCPM|PAPX|PBPX)\d*$)");

      static const std::regex ps2(
          R"(^(SCAJ|SLKA|CF00|SCKA|ALCH|CPCS|SLAJ|KOEI|ARZE|TCPS|SCCS|SRPM|GUST|WLFD|ULKS|VUGJ|HAKU|ROSE|CZP2|ARP2|PKP2|SLPN|NMP2|MTP2)\d*$)");

      std::string aid = std::to_string(get_running_app_id());
      std::string tid = get_running_app_tid();
      std::string exec = get_running_app_exec();
      std::string pid = std::to_string(get_running_app_pid());
      std::string name = get_name_by_tid(tid);
      std::string ver = get_app_version();
      std::string sdk = get_app_sdk();
      std::string region = get_app_region();

      bool is_daemon = is_daemon_process();
      bool is_home = (tid == SHELLUI_TID);

      if (is_home)
        is_daemon = true;

      if (is_daemon)
      {
        is_home = true;
        tid = SHELLUI_TID;

#ifndef __PROSPERO__
        if (g_system_info.firmware_int() <= 0x05050000)
          exec = "libSceVsh_aot.sprx";
        else
          exec = "app.exe.sprx";
#endif
      }

      json info;
      info["AID"] = aid;
      info["TID"] = tid;
      info["EXEC"] = exec;
      info["PID"] = pid;
      info["NAME"] = name;
      info["VER"] = ver;
      info["SDK"] = sdk;
      info["REGION"] = region;

      std::string type;
      if (is_daemon)
        type = "DAEMON";
      else if (tid.rfind("CUSA", 0) == 0)
        type = "PS4";
      else if (tid.rfind("PPSA", 0) == 0)
        type = "PS5";
      else if (std::regex_match(tid, ps1))
        type = "PS1";
      else if (std::regex_match(tid, ps2))
        type = "PS2";
      else
        type = "HOMEBREW";

      info["type"] = type;

      std::string icon0 = is_home ? "" : "/user/appmeta/" + tid + "/icon0.png";

      info["icon0"] = icon0;

      send_response(info);
    }

    void find_proc() {}

    void get_vm_maps()
    {
      std::string pid_param = extract_param("pid");

      pid_t pid = pid_param.empty() ? get_running_app_pid()
                                    : std::stoul(pid_param, nullptr, 0);

      proc_vm_maps maps = ::get_vm_maps(pid);
      if (maps.entries.empty())
      {
        send_error_response(UNKNOWN_ERROR);
        return;
      }

      json vm_maps = json::object();

      for (size_t i = 0; i < maps.entries.size(); i++)
      {
        const auto &m = maps.entries[i];

        char start[20], end[20], size[20], offset[20];
        snprintf(start, sizeof(start), "0x%lx", m.start);
        snprintf(end, sizeof(end), "0x%lx", m.end);
        snprintf(size, sizeof(size), "0x%lx", m.size);
        snprintf(offset, sizeof(offset), "0x%lx", m.offset);

        std::string name =
            (m.name[0] != '\0') ? m.name : ("(NoName)" + std::to_string(i));

        if (vm_maps.contains(name))
          name += "_" + std::to_string(i);

        vm_maps[name] = {{"START", start},
                         {"END", end},
                         {"SIZE", size},
                         {"OFFSET", offset},
                         {"PROT", m.prot}};
      }

      send_response(vm_maps);
    }

    void kill_process()
    {
      pid_t pid = -1;
      int aid = -1;

      std::string pid_param = extract_param("pid");
      std::string tid_param = extract_param("tid");
      std::string aid_param = extract_param("aid");
      std::string exec_param = extract_param("exec");

      if (pid_param.empty() && tid_param.empty() && aid_param.empty() &&
          exec_param.empty())
        send_error_response(UNKNOWN_ERROR);

      if (!aid_param.empty())
      {
        aid = std::stoul(aid_param, nullptr, 0);
        pid = get_proc_info(aid, &process_info::app_id, &process_info::process_id,
                            -1);
      }

      if (!tid_param.empty())
        pid = get_proc_info(tid_param.data(), &process_info::tid,
                            &process_info::process_id, -1);

      if (!pid_param.empty())
        pid = std::stoul(pid_param, nullptr, 0);

      if (!exec_param.empty())
        pid = get_proc_info(exec_param.data(), &process_info::exec,
                            &process_info::process_id, -1);

      if (pid < 0)
        return send_error_response(UNKNOWN_ERROR);

      if (pid > 0)
        kill(pid, SIGKILL);

      send_response(SUCCESS_MESSAGE);
    }

    void suspend_process()
    {
      if (pt_attach(get_running_app_pid()) == 0)
        send_response(SUCCESS_MESSAGE);
      else
        send_error_response(UNKNOWN_ERROR);
    }

    void resume_process()
    {
      if (pt_continue(get_running_app_pid(), 0) == 0)
        send_response(SUCCESS_MESSAGE);
      else
        send_error_response(UNKNOWN_ERROR);
    }

    std::string to_hex(uint64_t value)
    {
      std::stringstream ss;
      ss << "0x" << std::hex << std::uppercase << value;
      return ss.str();
    }

    void load_elf()
    {
      std::string pid_param = extract_param("pid");
      std::string path_param = extract_param("path");

      if (path_param.empty())
        return send_error_response(INVALID_ARGS);

      pid_t pid = -1;
      if (pid_param.empty())
        pid = get_running_app_pid();
      else
        pid = std::stoul(pid_param, nullptr, 0);

      uint8_t *bytes = get_elf_bytes(path_param.data());
      if (!bytes)
        return send_error_response(UNKNOWN_ERROR);

      RemoteElfInfo elf = ::inject_elf(pid, bytes);

      log_message("BASE: 0x%p | ENTRY: 0x%p | SIZE: %zu", (void *)elf.base,
                  (void *)elf.entry, elf.size);

      json info;
      info["BASE"] = to_hex(elf.base);
      info["ENTRY"] = to_hex(elf.entry);
      info["SIZE"] = elf.size;

      send_response(info);
    }

    void unload_elf()
    {
      std::string pid_param = extract_param("pid");
      std::string base_str = extract_param("base");
      std::string entry_str = extract_param("entry");
      std::string size_str = extract_param("size");

      if (base_str.empty() || entry_str.empty() || size_str.empty())
        return send_error_response(INVALID_ARGS);

      pid_t pid = -1;
      if (pid_param.empty())
        pid = get_running_app_pid();
      else
        pid = std::stoul(pid_param, nullptr, 0);

      RemoteElfInfo data;
      data.base = std::stoull(base_str, nullptr, 16);
      data.entry = std::stoull(entry_str, nullptr, 16);
      data.size = std::stoul(size_str, nullptr, 16);

      int ret = unload_elf(pid, data);

      if (ret == 0)
        send_response(SUCCESS_MESSAGE);
      else
        send_error_response(UNKNOWN_ERROR);
    }

    void read_memory()
    {
      std::string raw_param = extract_param("raw");
      std::string pid_param = extract_param("pid");
      std::string address_param = extract_param("address");
      std::string length_param = extract_param("length");

      if (address_param.empty() || length_param.empty())
        return send_error_response(INVALID_ARGS);

      bool raw = (raw_param == "true" || raw_param == "1");
      pid_t pid = pid_param.empty() ? get_running_app_pid()
                                    : std::stoul(pid_param, nullptr, 0);

      uintptr_t address = std::stoull(address_param, nullptr, 16);
      size_t length = std::stoull(length_param, nullptr, 0);

      if (pt_attach(pid))
        return send_error_response(UNKNOWN_ERROR);

      std::vector<uint8_t> buffer(length);
      if (pt_copyout(pid, address, buffer.data(), length) != 0)
      {
        pt_detach(pid, 0);
        return send_error_response(UNKNOWN_ERROR);
      }

      pt_detach(pid, 0);

      if (raw)
      {
        std::string out;
        out.reserve(buffer.size() * 5);
        char tmp[6];
        for (uint8_t b : buffer)
        {
          snprintf(tmp, sizeof(tmp), "0x%02X ", b);
          out += tmp;
        }
        send_response(out);
      }
      else
      {
        send_raw_file_response(buffer.data(), buffer.size());
      }
    }

    void write_memory()
    {
      std::string pid_param = extract_param("pid", false);
      std::string address_param = extract_param("address", false);
      std::string data_param = extract_param("data", false);

      if (address_param.empty() || data_param.empty())
        return send_error_response(INVALID_ARGS);

      if (data_param.length() % 2 != 0)
        return send_error_response(INVALID_ARGS);

      pid_t pid = pid_param.empty() ? get_running_app_pid()
                                    : std::stoul(pid_param, nullptr, 0);
      uintptr_t address = std::stoull(address_param, nullptr, 16);
      size_t length = data_param.length() / 2;

      std::vector<uint8_t> buffer(length);
      for (size_t i = 0; i < length; ++i)
      {
        unsigned int byte;
        sscanf(data_param.c_str() + i * 2, "%2x", &byte);
        buffer[i] = static_cast<uint8_t>(byte);
      }

      if (pt_attach(pid))
        return send_error_response(UNKNOWN_ERROR);

      if (pt_copyin(pid, buffer.data(), address, length) != 0)
      {
        pt_detach(pid, 0);
        return send_error_response(UNKNOWN_ERROR);
      }

      pt_detach(pid, 0);
      send_response(SUCCESS_MESSAGE);
    }

    void allocate_memory()
    {
      std::string pid_param = extract_param("pid");
      std::string length_param = extract_param("length");

      if (length_param.empty())
        return send_error_response(INVALID_ARGS);

      uintptr_t length = std::stoull(length_param, nullptr, 0);
      if (length == 0)
        return send_error_response(INVALID_ARGS);

      pid_t pid = pid_param.empty() ? get_running_app_pid()
                                    : std::stoul(pid_param, nullptr, 0);

      if (pt_attach(pid))
        return send_error_response(UNKNOWN_ERROR);

      uintptr_t address = pt_mmap(pid, 0, length, PROT_READ | PROT_WRITE,
                                  MAP_PRIVATE | MAP_ANONYMOUS, -1, 0);

      pt_detach(pid, 0);

      if (address == -1)
        return send_error_response(UNKNOWN_ERROR);

      char buf[21];
      snprintf(buf, sizeof(buf), "0x%" PRIxPTR, address);
      send_response(std::string(buf));
    }

    void free_memory()
    {
      std::string pid_param = extract_param("pid");
      std::string address_param = extract_param("address");
      std::string length_param = extract_param("length");

      if (address_param.empty() || length_param.empty())
        return send_error_response(INVALID_ARGS);

      pid_t pid = pid_param.empty() ? get_running_app_pid()
                                    : std::stoul(pid_param, nullptr, 0);
      uintptr_t address = std::stoull(address_param, nullptr, 16);
      uintptr_t length = std::stoull(length_param, nullptr, 0);

      if (pt_attach(pid))
        return send_error_response(UNKNOWN_ERROR);

      pt_munmap(pid, address, length);

      pt_detach(pid, 0);

      if (address == -1)
        return send_error_response(UNKNOWN_ERROR);

      send_response(SUCCESS_MESSAGE);
    }

    void memory_protection()
    {
      std::string pid_param = extract_param("pid");
      std::string addressParam = extract_param("address");
      std::string lengthParam = extract_param("length");
      std::string protParam = extract_param("prot");

      if (addressParam.empty() || lengthParam.empty() || protParam.empty())
        return send_error_response(INVALID_ARGS);

      uint64_t address = std::stoull(addressParam, nullptr, 16);
      uint64_t length = std::stoull(lengthParam, nullptr, 16);
      uint32_t protVal = static_cast<uint32_t>(std::stoul(protParam, nullptr, 0));

      constexpr uint32_t validMask = VM_PROT_NONE | VM_PROT_READ | VM_PROT_WRITE |
                                     VM_PROT_EXECUTE | VM_PROT_COPY | VM_PROT_ALL |
                                     VM_PROT_RW | VM_PROT_DEFAULT;

      if (protVal & ~validMask)
        return send_error_response(UNKNOWN_ERROR);

      pid_t pid = pid_param.empty() ? get_running_app_pid()
                                    : std::stoul(pid_param, nullptr, 0);

      if (pt_attach(pid))
        return send_error_response(UNKNOWN_ERROR);

      intptr_t addr = pt_mprotect(pid, address, length, protVal);

      pt_detach(pid, 0);

      send_response(SUCCESS_MESSAGE);
    }

    void load_module()
    {
      std::string pid_param = extract_param("pid");
      std::string module = extract_param("module");

      if (module.empty())
        module = extract_param("path");

      if (module.empty())
        module = extract_param("file");

      if (module.empty())
        return send_error_response(INVALID_ARGS);

      pid_t pid = pid_param.empty() ? get_running_app_pid()
                                    : std::stoul(pid_param, nullptr, 0);
      int handle = load_prx_remote(pid, module.data());

      if (handle > 0)
      {
        nlohmann::json response;
        response["HANDLE"] = handle;
        response["MODULE"] = module;
        send_response(response);
      }
      else
        send_error_response(UNKNOWN_ERROR);
    }

    void unload_module()
    {
      std::string pid_param = extract_param("pid");
      std::string module = extract_param("module");
      std::string handle_string = extract_param("handle");

      if (module.empty())
        module = extract_param("path");

      if (module.empty())
        module = extract_param("file");

      if (handle_string.empty() && module.empty())
        return send_error_response(INVALID_ARGS);

      pid_t pid = pid_param.empty() ? get_running_app_pid()
                                    : std::stoul(pid_param, nullptr, 0);

      int ret = -1;
      int handle = -1;
      if (!handle_string.empty())
        handle = std::stoul(handle_string, nullptr, 0);

      if (handle > 0)
        ret = unload_prx_remote(pid, handle);

      if (!module.empty())
        ret = unload_prx_remote(pid, module.data());

      if (ret == 0)
        send_response(SUCCESS_MESSAGE);
      else
        send_error_response(UNKNOWN_ERROR);
    }

    void get_module_handle()
    {
      std::string pid_param = extract_param("pid");
      std::string name_param = extract_param("name");

      if (name_param.empty())
        return send_error_response(UNKNOWN_ERROR);

      pid_t pid = pid_param.empty() ? get_running_app_pid()
                                    : std::stoul(pid_param, nullptr, 0);

      if (pt_attach(pid) != 0)
        return send_error_response(UNKNOWN_ERROR);

      char nid[12];
      intptr_t fn_get_list =
          pt_resolve(pid, nid_encode("sceKernelGetModuleList", nid));

      intptr_t fn_get_info =
          pt_resolve(pid, nid_encode("sceKernelGetModuleInfo", nid));

      if (fn_get_list <= 0 || fn_get_info <= 0)
      {
        pt_detach(pid, 0);
        return send_error_response(UNKNOWN_ERROR);
      }

      int result_handle = -1;
      size_t array_sz = 800 * sizeof(uint32_t);
      intptr_t r_array = pt_mmap(pid, 0, array_sz, PROT_READ | PROT_WRITE,
                                 MAP_PRIVATE | MAP_ANONYMOUS, -1, 0);

      intptr_t r_avail = pt_mmap(pid, 0, sizeof(size_t), PROT_READ | PROT_WRITE,
                                 MAP_PRIVATE | MAP_ANONYMOUS, -1, 0);

      intptr_t r_info = pt_mmap(pid, 0, 352, PROT_READ | PROT_WRITE,
                                MAP_PRIVATE | MAP_ANONYMOUS, -1, 0);

      if (r_array > 0 && r_avail > 0 && r_info > 0)
      {
        size_t count = 0;
        pt_copyin(pid, &count, r_avail, sizeof(size_t));

        if (pt_call_trampoline(pid, fn_get_list, r_array, 800, r_avail, 0, 0, 0) ==
            0)
        {
          pt_copyout(pid, r_avail, &count, sizeof(count));
          if (count > 800)
            count = 800;

          for (size_t i = 0; i < count; i++)
          {
            uint32_t handle;
            char name[256];
            size_t info_sz = 352;

            pt_copyout(pid, r_array + (i * sizeof(uint32_t)), &handle,
                       sizeof(handle));
            pt_copyin(pid, &info_sz, r_info, sizeof(info_sz));

            if (pt_call_trampoline(pid, fn_get_info, handle, r_info, 0, 0, 0, 0) ==
                0)
            {
              pt_copyout(pid, r_info + 0x08, name, 256);

              if (strcmp(name, name_param.c_str()) == 0)
              {
                result_handle = (int)handle;
                break;
              }
            }
          }
        }
      }

      if (r_array > 0)
        pt_munmap(pid, r_array, array_sz);

      if (r_avail > 0)
        pt_munmap(pid, r_avail, sizeof(size_t));

      if (r_info > 0)
        pt_munmap(pid, r_info, 352);

      pt_detach(pid, 0);

      send_response(result_handle);
    }

    void get_all_modules()
    {
      std::string pid_param = extract_param("pid");
      pid_t pid = pid_param.empty() ? get_running_app_pid()
                                    : std::stoul(pid_param, nullptr, 0);

      if (pt_attach(pid) != 0)
        return send_error_response(UNKNOWN_ERROR);

      char nid[12];
      intptr_t fn_list = pt_resolve(pid, nid_encode("sceKernelGetModuleList", nid));
      intptr_t fn_info = pt_resolve(pid, nid_encode("sceKernelGetModuleInfo", nid));

      if (fn_list <= 0 || fn_info <= 0)
      {
        pt_detach(pid, 0);
        return send_error_response(UNKNOWN_ERROR);
      }

      nlohmann::json response;

      size_t array_sz = 800 * sizeof(uint32_t);

      intptr_t r_array = pt_mmap(pid, 0, array_sz, PROT_READ | PROT_WRITE,
                                 MAP_PRIVATE | MAP_ANONYMOUS, -1, 0);

      intptr_t r_avail = pt_mmap(pid, 0, sizeof(size_t), PROT_READ | PROT_WRITE,
                                 MAP_PRIVATE | MAP_ANONYMOUS, -1, 0);

      intptr_t r_info = pt_mmap(pid, 0, 352, PROT_READ | PROT_WRITE,
                                MAP_PRIVATE | MAP_ANONYMOUS, -1, 0);

      if (r_array > 0 && r_avail > 0 && r_info > 0)
      {
        size_t count = 0;

        pt_copyin(pid, &count, r_avail, sizeof(size_t));

        if (pt_call_trampoline(pid, fn_list, r_array, 800, r_avail, 0, 0, 0) == 0)
        {
          pt_copyout(pid, r_avail, &count, sizeof(count));
          if (count > 800)
            count = 800;

          for (size_t i = 0; i < count; i++)
          {
            uint32_t handle = 0;
            char name[256] = {0};
            size_t info_sz = 352;

            pt_copyout(pid, r_array + (i * sizeof(uint32_t)), &handle,
                       sizeof(handle));

            pt_copyin(pid, &info_sz, r_info, sizeof(info_sz));

            if (pt_call_trampoline(pid, fn_info, handle, r_info, 0, 0, 0, 0) == 0)
            {
              pt_copyout(pid, r_info + 0x08, name, 256);

              std::string name_str(name);

              if (name_str.size() >= 5 &&
                  name_str.compare(name_str.size() - 5, 5, ".sprx") == 0)
              {

                response[name_str] = handle;
              }
            }
          }
        }
      }

      if (r_array > 0)
        pt_munmap(pid, r_array, array_sz);

      if (r_avail > 0)
        pt_munmap(pid, r_avail, sizeof(size_t));

      if (r_info > 0)
        pt_munmap(pid, r_info, 352);

      pt_detach(pid, 0);

      send_response(response);
    }

    void resolve_symbol()
    {
      std::string pid_param = extract_param("pid");
      std::string handle_param = extract_param("handle");
      std::string name_param = extract_param("name");

      if (name_param.empty())
        return send_error_response(INVALID_ARGS);

      pid_t pid = pid_param.empty() ? get_running_app_pid()
                                    : std::stoul(pid_param, nullptr, 0);

      int handle =
          handle_param.empty() ? 0x1 : std::stoul(handle_param, nullptr, 0);

      char nid[12] = {0};
      nid_encode(name_param.data(), nid);
      intptr_t address = pt_resolve(pid, nid, handle);

      if (address == 0)
        return send_error_response(UNKNOWN_ERROR);

      char addr_buf[32];
      snprintf(addr_buf, sizeof(addr_buf), "0x%llx", (unsigned long long)address);

      send_response(addr_buf);
    }

    // not implemented
    void aob_scan()
    {
      const std::string pid_param = extract_param("pid");
      const std::string sig_param = extract_param("sig");
      const std::string start_param = extract_param("start");
      const std::string end_param = extract_param("end");

      send_response(SUCCESS_MESSAGE);
    }

    static uint64_t to_reg(const std::string &s)
    {
      if (s.empty())
        return 0;

      if (s == "true")
        return 1;
      if (s == "false")
        return 0;

      if (s.size() > 2 && s[0] == '0' && (std::tolower(s[1]) == 'x'))
      {
        try
        {
          return std::stoull(s, nullptr, 16);
        }
        catch (...)
        {
          return 0;
        }
      }

      if (s.find('.') != std::string::npos)
      {
        try
        {
          float f = std::stof(s);
          uint32_t bits;
          std::memcpy(&bits, &f, sizeof(f));
          return static_cast<uint64_t>(bits);
        }
        catch (...)
        {
          return 0;
        }
      }

      try
      {
        return static_cast<uint64_t>(std::stoll(s, nullptr, 10));
      }
      catch (...)
      {
        return 0;
      }
    }

    void rpc_call()
    {
      std::string addr_param = extract_param("address", false);
      if (addr_param.empty())
      {
        return send_error_response(UNKNOWN_ERROR);
      }

      intptr_t target_func =
          static_cast<intptr_t>(std::stoull(addr_param, nullptr, 0));

      std::string pid_param = extract_param("pid", false);
      pid_t pid = pid_param.empty()
                      ? get_running_app_pid()
                      : static_cast<pid_t>(std::stoul(pid_param, nullptr, 0));

      if (pt_attach(pid) != 0)
      {
        return send_error_response(UNKNOWN_ERROR);
      }

      std::vector<uint64_t> args(6, 0);
      for (int i = 0; i < 6; ++i)
      {
        std::string key = "arg" + std::to_string(i + 1);
        std::string val = extract_param(key, false);

        if (val.empty())
        {
          break;
        }

        args[i] = to_reg(val);
      }

      long ret = pt_call_trampoline(pid, target_func, args[0], args[1], args[2],
                                    args[3], args[4], args[5]);

      pt_detach(pid, 0);

      char buf[64];
      snprintf(buf, sizeof(buf), "0x%lx", ret);
      send_response(buf);
    }

  }

}
