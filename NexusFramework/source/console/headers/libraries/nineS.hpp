#pragma once

typedef struct {
  uintptr_t base;
  size_t size;
  intptr_t entry;
} RemoteElfInfo;

RemoteElfInfo inject_elf(pid_t pid, void *elf);
int unload_elf(pid_t pid, RemoteElfInfo info);

int inject_elf_ps5(pid_t pid, void *elf);
