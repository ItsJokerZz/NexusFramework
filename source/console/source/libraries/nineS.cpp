#include "headers.hpp"
#include <elf.h>

RemoteElfInfo inject_elf(pid_t pid, void *elf_bytes) {
  RemoteElfInfo info = {0, 0, 0};

  log_message("[inject_elf] start pid=%d elf_bytes=%p", pid, elf_bytes);

  if (!elf_bytes) {
    log_message("[inject_elf] abort: elf_bytes is NULL");
    return info;
  }

  Elf64_Ehdr *ehdr = (Elf64_Ehdr *)elf_bytes;

  if (ehdr->e_ident[EI_MAG0] != ELFMAG0 || ehdr->e_ident[EI_MAG1] != ELFMAG1) {
    log_message("[inject_elf] abort: invalid ELF magic (0x%x 0x%x)",
                ehdr->e_ident[EI_MAG0], ehdr->e_ident[EI_MAG1]);
    return info;
  }

  log_message(
      "[inject_elf] valid ELF detected entry=0x%lx phoff=0x%lx phnum=%d",
      (unsigned long)ehdr->e_entry, (unsigned long)ehdr->e_phoff,
      ehdr->e_phnum);

  Elf64_Phdr *phdr = (Elf64_Phdr *)((uint8_t *)elf_bytes + ehdr->e_phoff);

  uintptr_t min_vaddr = (uintptr_t)-1;
  uintptr_t max_vaddr = 0;

  for (int i = 0; i < ehdr->e_phnum; i++) {
    if (phdr[i].p_type == PT_LOAD) {
      log_message("[inject_elf] PT_LOAD[%d] vaddr=0x%lx memsz=0x%lx", i,
                  (unsigned long)phdr[i].p_vaddr,
                  (unsigned long)phdr[i].p_memsz);

      if (phdr[i].p_vaddr < min_vaddr)
        min_vaddr = phdr[i].p_vaddr;

      if (phdr[i].p_vaddr + phdr[i].p_memsz > max_vaddr)
        max_vaddr = phdr[i].p_vaddr + phdr[i].p_memsz;
    }
  }

  info.size = max_vaddr - min_vaddr;

  log_message("[inject_elf] computed range min=0x%lx max=0x%lx size=0x%lx",
              (unsigned long)min_vaddr, (unsigned long)max_vaddr,
              (unsigned long)info.size);

  if (pt_attach(pid) < 0) {
    log_message("[inject_elf] pt_attach failed pid=%d", pid);
    return info;
  }

  log_message("[inject_elf] attached to pid=%d", pid);

  info.entry = (uintptr_t)elfldr_load(pid, (uint8_t *)elf_bytes);

  log_message("[inject_elf] elfldr_load returned entry=0x%lx",
              (unsigned long)info.entry);

  if (info.entry <= 0) {
    log_message("[inject_elf] load failed, detaching");
    pt_detach(pid, 0);
    info.entry = 0;
    return info;
  }

  info.base = info.entry - (ehdr->e_entry - min_vaddr);

  log_message("[inject_elf] computed base=0x%lx (entry=0x%lx elf_entry=0x%lx "
              "min_vaddr=0x%lx)",
              (unsigned long)info.base, (unsigned long)info.entry,
              (unsigned long)ehdr->e_entry, (unsigned long)min_vaddr);

  log_message("[inject_elf] calling stub entry=0x%lx",
              (unsigned long)info.entry);

  pt_call_stub(pid, info.entry, 0, 0, 0, 0, 0, 0);

  log_message("[inject_elf] stub executed, detaching pid=%d", pid);

  pt_detach(pid, 0);

  log_message("[inject_elf] done pid=%d base=0x%lx size=0x%lx entry=0x%lx", pid,
              (unsigned long)info.base, (unsigned long)info.size,
              (unsigned long)info.entry);

  return info;
}

int unload_elf(pid_t pid, RemoteElfInfo info) {
  if (info.base == 0 || info.size == 0)
    return 1;

  if (pt_attach(pid) < 0)
    return 1;

  int res = pt_munmap(pid, info.base, info.size);

  pt_detach(pid, 0);

  return (res == 0) ? 0 : 1;
}

struct SCEFunctions {
  int (*elf_main)(void *payload_args);
  void *payload_args;
  int (*pthread_create_ptr)(pthread_t *, const pthread_addr_t *,
                            void *(*)(void *), void *);
};

__attribute__((section(".stager_shellcode$1"))) static int
stager(SCEFunctions *fn) {
  pthread_t thread;
  fn->pthread_create_ptr(&thread, nullptr, (void *(*)(void *))fn->elf_main,
                         fn->payload_args);
  __asm__("int3");
  return 0;
}

__attribute__((section(".stager_shellcode$2"))) static int stager_end() {
  return 0;
}

static uint32_t shellcode_size() {
  return (uint8_t *)&stager_end - (uint8_t *)&stager;
}

static intptr_t resolve_pthread_create(pid_t pid) {
  char nid[12] = {};
  nid_encode("pthread_create", nid);
  return pt_resolve(pid, nid);
}

int inject_elf_ps5(pid_t pid, void *elf) {
  if (pt_attach(pid) < 0) {
    log_message("Error attaching to PID %d\n", pid);
    return false;
  }

  log_message("[+] Attached to %d\n", pid);

  intptr_t entry = elfldr_load(pid, (uint8_t *)elf);
  if (entry <= 0) {
    log_message("[-] Failed to load ELF\n");
    pt_detach(pid, 0);
    return false;
  }

  SCEFunctions fn = {};
  fn.elf_main = reinterpret_cast<int (*)(void *)>(entry);

  struct args {
    int idc;
  };

  args arg{.idc = 1337};

#ifdef __PROSPERO__
  fn.payload_args = reinterpret_cast<void *>(elfldr_payload_args(pid));
#else
  fn.payload_args = reinterpret_cast<void *>(&arg);
#endif

  fn.pthread_create_ptr = reinterpret_cast<int (*)(
      pthread_t *, const pthread_addr_t *, void *(*)(void *), void *)>(
      resolve_pthread_create(pid));

  log_message("[+] Entry: %#lx | Args: %#lx\n", entry,
              (uintptr_t)fn.payload_args);

  uint64_t sz = shellcode_size();

#ifdef __PROSPERO__
  uint64_t bootstrap = pt_mmap(pid, 0, sz, PROT_READ | PROT_WRITE,
                               MAP_ANONYMOUS | MAP_PRIVATE, -1, 0);

  uint64_t fn_mem = pt_mmap(pid, 0, sizeof(fn), PROT_READ | PROT_WRITE,
                            MAP_ANONYMOUS | MAP_PRIVATE, -1, 0);
#else
  uint64_t bootstrap = pt_mmap(pid, 0, sz, PROT_READ | PROT_WRITE | PROT_EXEC,
                               MAP_ANONYMOUS | MAP_PRIVATE, -1, 0);

  uint64_t fn_mem = pt_mmap(pid, 0, sizeof(fn), PROT_READ | PROT_WRITE | PROT_EXEC,
                            MAP_ANONYMOUS | MAP_PRIVATE, -1, 0);
#endif

  if (!bootstrap || !fn_mem) {
    log_message("[-] mmap failed\n");
    pt_detach(pid, 0);
    return false;
  }

#ifdef __PROSPERO__
  kernel_mprotect(pid, bootstrap, sz, PROT_READ | PROT_WRITE | PROT_EXEC);
  kernel_mprotect(pid, fn_mem, sz, PROT_READ | PROT_WRITE | PROT_EXEC);
#endif

  pt_copyin(pid, (void *)&stager, bootstrap, sz);
  pt_copyin(pid, &fn, fn_mem, sizeof(fn));

  log_message("[+] Bootstrap @ %#lx | fn_mem @ %#lx\n", bootstrap, fn_mem);
  log_message("[+] Triggering stager...\n");

  pt_call_stub(pid, bootstrap, fn_mem, 0, 0, 0, 0, 0);
  sleep(1);

  pt_detach(pid, 0);
  log_message("[+] Done\n");
  return true;
}
