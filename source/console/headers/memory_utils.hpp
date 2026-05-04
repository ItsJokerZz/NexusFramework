#pragma once

#define PROT_RWX (PROT_READ | PROT_WRITE | PROT_EXEC)

#ifdef __PROSPERO__
#define PROC_VMSPACE_OFFSET 0x200
#define VMSPACE_ROOT_ENTRY 0x08
#define VMSPACE_NUM_ENTRIES 0x1A8
#define VMSPACE_ENTRY_OFFSET 0x58
#define VMSPACE_ENTRY_PROT 0x64
#define VMSPACE_ENTRY_NAME 0x142
#else
#define PROC_VMSPACE_OFFSET 0x168
#define VMSPACE_ROOT_ENTRY 0x00
#define VMSPACE_NUM_ENTRIES 0x100
#define VMSPACE_ENTRY_OFFSET 0x50
#define VMSPACE_ENTRY_PROT 0x5C
#define VMSPACE_ENTRY_NAME 0x8D
#endif

#define VMSPACE_ENTRY_START 0x20
#define VMSPACE_ENTRY_END 0x28
#define VMSPACE_PROT_MASK 0x7

struct proc_vm_map_entry {
  uintptr_t start;
  uintptr_t end;
  uintptr_t size;
  uintptr_t offset;
  uint32_t prot;
  char name[32];
};

struct proc_vm_maps {
  std::vector<proc_vm_map_entry> entries;
};

extern const off_t KERNEL_OFFSET_PROC_P_PID;
extern const intptr_t KERNEL_ADDRESS_ALLPROC;

proc_vm_maps get_vm_maps(pid_t pid);

struct vm_text_region {
  uintptr_t base;
  size_t size;
  pid_t pid;
};

vm_text_region find_vm_text_regions(pid_t pid);

int mprotect(pid_t pid, uintptr_t addr, size_t len, int prot);

bool is_elf_header(uint8_t *data);

uint8_t *get_elf_bytes(const char *path);

struct remote_executor {
  pid_t target_pid;
  uint32_t libkernel_handle = 0;
  bool is_attached = false;

  remote_executor(pid_t pid) : target_pid(pid) {
    is_attached = (pt_attach(target_pid) == 0);
    if (is_attached)
      kernel_dynlib_handle(target_pid, "libkernel.sprx", &libkernel_handle);
  }

  ~remote_executor() {
    if (is_attached)
      pt_detach(target_pid, 0);
  }

  int execute_call(const char *symbol_name, uint64_t a1 = 0, uint64_t a2 = 0,
                   uint64_t a3 = 0, uint64_t a4 = 0, uint64_t a5 = 0,
                   uint64_t a6 = 0) {
    if (!is_attached || !libkernel_handle)
      return -1;

    intptr_t fn_addr =
        kernel_dynlib_dlsym(target_pid, libkernel_handle, symbol_name);
    if (!fn_addr)
      return -1;

    return static_cast<int>(
        pt_call_trampoline(target_pid, fn_addr, a1, a2, a3, a4, a5, a6));
  }
};

struct remote_memory_block {
  pid_t target_pid;
  intptr_t address = -1;
  size_t block_size;

  remote_memory_block(pid_t pid, size_t size)
      : target_pid(pid), block_size(size) {
    address = pt_mmap(target_pid, 0, block_size, PROT_READ | PROT_WRITE,
                      MAP_PRIVATE | MAP_ANONYMOUS, -1, 0);
  }

  remote_memory_block(remote_memory_block &&other) noexcept
      : target_pid(other.target_pid), address(other.address),
        block_size(other.block_size) {
    other.address = -1;
  }

  ~remote_memory_block() {
    if (address >= 0)
      pt_munmap(target_pid, address, block_size);
  }

  static remote_memory_block from_string(pid_t pid, const char *str,
                                         size_t size = 0x4000) {
    remote_memory_block block(pid, size);
    if (block.is_valid())
      pt_copyin(pid, str, block.address, strlen(str) + 1);

    return block;
  }

  bool is_valid() const { return address >= 0; }
  remote_memory_block(const remote_memory_block &) = delete;
  remote_memory_block &operator=(const remote_memory_block &) = delete;
};

bool is_prx_loaded_remote(pid_t pid, const std::string &full_path,
                          uint64_t *out_handle);
int unload_prx_remote(pid_t pid, uint64_t handle);
int unload_prx_remote(pid_t pid, const char *prx_path);
int load_prx_remote(pid_t pid, const char *prx_path);
int call_plugin_function(pid_t pid, const char *prx_path,
                         const char *func_name);
int plugin_start_remote(pid_t pid, const char *prx_path);
int plugin_stop_remote(pid_t pid, const char *prx_path);
int loadStart_plugin_remote(pid_t pid, const char *prx_path,
                            uint64_t *out_handle);
int unloadStop_plugin_remote(pid_t pid, const char *prx_path);
