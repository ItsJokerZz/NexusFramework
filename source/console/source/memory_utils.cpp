#include "headers.hpp"

proc_vm_maps get_vm_maps(pid_t pid) {
  uintptr_t proc = 0;
  kernel_copyout(KERNEL_ADDRESS_ALLPROC, &proc, sizeof(proc));

  if (!proc)
    return {};

  while (proc) {
    int cur_pid = 0;
    kernel_copyout(proc + KERNEL_OFFSET_PROC_P_PID, &cur_pid, sizeof(cur_pid));

    if (cur_pid == pid)
      break;

    uintptr_t next_proc = 0;
    kernel_copyout(proc, &next_proc, sizeof(next_proc));
    proc = next_proc;
  }

  if (!proc)
    return {};

  uintptr_t vmspace = 0;
  kernel_copyout(proc + PROC_VMSPACE_OFFSET, &vmspace, sizeof(vmspace));
  if (!vmspace)
    return {};

  uintptr_t entry = 0;
  kernel_copyout(vmspace + VMSPACE_ROOT_ENTRY, &entry, sizeof(entry));

  uint32_t num_entries = 0;
  kernel_copyout(vmspace + VMSPACE_NUM_ENTRIES, &num_entries,
                 sizeof(num_entries));

  if (!num_entries || !entry)
    return {};

  proc_vm_maps maps;
  maps.entries.reserve(num_entries);

  for (uint32_t i = 0; i < num_entries && entry; i++) {
    proc_vm_map_entry map_entry{};

    kernel_copyout(entry + VMSPACE_ENTRY_START, &map_entry.start,
                   sizeof(map_entry.start));
    kernel_copyout(entry + VMSPACE_ENTRY_END, &map_entry.end,
                   sizeof(map_entry.end));
    kernel_copyout(entry + VMSPACE_ENTRY_OFFSET, &map_entry.offset,
                   sizeof(map_entry.offset));

    map_entry.size = map_entry.end - map_entry.start;

    uint16_t prot = 0;
    kernel_copyout(entry + VMSPACE_ENTRY_PROT, &prot, sizeof(prot));
    map_entry.prot = prot & VMSPACE_PROT_MASK;

    char namebuf[32] = {0};
    kernel_copyout(entry + VMSPACE_ENTRY_NAME, namebuf, sizeof(namebuf));

    bool valid = false;
    for (int j = 0; j < 32 && namebuf[j] != '\0'; j++) {
      if (namebuf[j] >= 32 && namebuf[j] <= 126) {
        valid = true;
        break;
      }
    }

    if (valid) {
      std::strncpy(map_entry.name, namebuf, sizeof(map_entry.name) - 1);
    } else
      snprintf(map_entry.name, sizeof(map_entry.name), "(NoName)%u", i);

    maps.entries.push_back(map_entry);

    uintptr_t next_entry = 0;
    kernel_copyout(entry + 0x08, &next_entry, sizeof(next_entry));

    if (next_entry == 0 || next_entry == entry)
      break;
    entry = next_entry;
  }

  return maps;
}

vm_text_region find_vm_text_regions(pid_t pid) {
  vm_text_region region = {0};

  proc_vm_maps full_map = ::get_vm_maps(pid);
  if (full_map.entries.empty())
    return region;

  uintptr_t best_base = 0;
  size_t max_size = 0;
  bool found = false;

  for (const auto &m : full_map.entries) {
    if (m.prot & 4) {
      if (m.size > max_size) {
        max_size = m.size;
        best_base = m.start;
        found = true;
      }
    }
  }

  if (found) {
    region.base = best_base;
    region.size = max_size;
    region.pid = pid;
  }

  return region;
}

int mprotect(pid_t pid, uintptr_t addr, size_t len, int prot) {
#ifdef __PROSPERO__
  char test[0x100];
  if (sceKernelMprotect(test, sizeof(test),
                       PROT_RWX) != 0)
    return kernel_mprotect(pid, addr, len, prot);
#endif

  return sceKernelMprotect((void *)addr, len, prot);
}

bool is_elf_header(uint8_t *data) {
  uint8_t header[] = {0x7f, 'E', 'L', 'F'};

  return !memcmp(data, header, 4);
}

uint8_t *get_elf_bytes(const char *path) {
  static uint8_t *static_buf = nullptr;
  static size_t static_size = 0;

  if (!path_exists(path))
    return nullptr;

  FILE *f = fopen(path, "rb");
  if (!f)
    return nullptr;

  fseek(f, 0, SEEK_END);
  size_t size = ftell(f);
  rewind(f);

  if (size == 0) {
    fclose(f);
    return nullptr;
  }

  if (static_size < size) {
    delete[] static_buf;
    static_buf = new uint8_t[size];
    static_size = size;
  }

  if (fread(static_buf, 1, size, f) != size || !is_elf_header(static_buf)) {
    fclose(f);
    return nullptr;
  }

  fclose(f);
  return static_buf;
}

bool is_prx_loaded_remote(pid_t pid, const std::string &full_path,
                          uint64_t *out_handle) {
  std::string filename = get_filename_from_path(full_path);
  uint32_t handle = 0;
  
  log_message("[DEBUG] Checking if PRX is loaded: %s (PID: %d)", filename.c_str(), pid);

  if (kernel_dynlib_handle(pid, filename.c_str(), &handle) != 0) {
    log_message("[INFO] PRX %s not found in PID %d", filename.c_str(), pid);
    return false;
  }

  if (out_handle)
    *out_handle = handle;

  log_message("[INFO] PRX %s found. Handle: 0x%X", filename.c_str(), handle);
  return handle > 0;
}

int unload_prx_remote(pid_t pid, uint64_t handle) {
  if (handle <= 0) {
    log_message("[ERROR] Invalid handle (0x%llX) provided for unload", handle);
    return -1;
  }

  log_message("[DEBUG] Unloading handle 0x%llX from PID %d", handle, pid);
  remote_executor executor(pid);
  int result = executor.execute_call("sceKernelStopUnloadModule", handle);
  
  log_message("[INFO] sceKernelStopUnloadModule returned: %d", result);
  return result;
}

int unload_prx_remote(pid_t pid, const char *prx_path) {
  uint64_t handle = 0;
  log_message("[DEBUG] Attempting to unload PRX by path: %s", prx_path);

  if (!is_prx_loaded_remote(pid, prx_path, &handle)) {
    log_message("[WARN] Cannot unload; PRX not loaded: %s", prx_path);
    return -1;
  }

  return unload_prx_remote(pid, handle);
}

int load_prx_remote(pid_t pid, const char *prx_path) {
  uint64_t existing_handle = 0;
  log_message("[DEBUG] Requesting load for: %s", prx_path);

  if (is_prx_loaded_remote(pid, prx_path, &existing_handle)) {
    log_message("[INFO] PRX already loaded. Returning handle: 0x%llX", existing_handle);
    return static_cast<int>(existing_handle);
  }

  remote_executor executor(pid);
  if (!executor.is_attached) {
    log_message("[ERROR] Failed to attach executor to PID %d", pid);
    return -1;
  }

  auto path_buffer = remote_memory_block::from_string(pid, prx_path);
  if (!path_buffer.is_valid()) {
    log_message("[ERROR] Failed to allocate remote memory for path: %s", prx_path);
    return -1;
  }

  log_message("[DEBUG] Calling sceKernelLoadStartModule for: %s", prx_path);
  int result = executor.execute_call("sceKernelLoadStartModule", path_buffer.address);
  log_message("[INFO] sceKernelLoadStartModule returned handle/result: %d", result);
  
  return result;
}

int call_plugin_function(pid_t pid, const char *prx_path,
                         const char *func_name) {
  uint64_t handle = 0;
  log_message("[DEBUG] Calling function '%s' in PRX: %s", func_name, prx_path);

  if (!is_prx_loaded_remote(pid, prx_path, &handle)) {
    log_message("[ERROR] Function call failed: PRX not loaded");
    return -1;
  }

  remote_executor executor(pid);
  if (!executor.is_attached) {
    log_message("[ERROR] Executor failed to attach for function call");
    return -1;
  }

  intptr_t func_addr = kernel_dynlib_dlsym(pid, (uint32_t)handle, func_name);
  if (!func_addr) {
    log_message("[ERROR] Could not find symbol '%s' in PRX", func_name);
    return -1;
  }

  log_message("[DEBUG] Executing trampoline at address: 0x%p", (void*)func_addr);
  int result = static_cast<int>(pt_call_trampoline(pid, func_addr, 0, 0, 0, 0, 0, 0));
  log_message("[INFO] Function '%s' returned: %d", func_name, result);
  
  return result;
}

int plugin_start_remote(pid_t pid, const char *prx_path) {
  log_message("[DEBUG] Invoking plugin_start...");
  return call_plugin_function(pid, prx_path, "plugin_start");
}

int plugin_stop_remote(pid_t pid, const char *prx_path) {
  log_message("[DEBUG] Invoking plugin_stop...");
  return call_plugin_function(pid, prx_path, "plugin_stop");
}

int loadStart_plugin_remote(pid_t pid, const char *prx_path,
                            uint64_t *out_handle) {
  log_message("[PROCESS] Beginning LoadStart sequence for: %s", prx_path);
  
  int handle = load_prx_remote(pid, prx_path);
  if (handle <= 0) {
    log_message("[ERROR] LoadStart failed at the loading stage");
    return -1;
  }

  if (out_handle)
    *out_handle = static_cast<uint64_t>(handle);

  return plugin_start_remote(pid, prx_path);
}

int unloadStop_plugin_remote(pid_t pid, const char *prx_path) {
  log_message("[PROCESS] Beginning UnloadStop sequence for: %s", prx_path);
  
  int stop_result = plugin_stop_remote(pid, prx_path);
  if (stop_result != 0) {
    log_message("[WARN] plugin_stop returned non-zero: %d", stop_result);
  }

  int unload_result = unload_prx_remote(pid, prx_path);
  if (unload_result != 0) {
    log_message("[ERROR] unload_prx failed with result: %d", unload_result);
  }

  return (stop_result == 0 && unload_result == 0) ? 0 : -1;
}

//////////////////////////////////////////////////////////////////////////////////////
