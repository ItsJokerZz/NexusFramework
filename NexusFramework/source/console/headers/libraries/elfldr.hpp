/* Copyright (C) 2024 John Törnblom

This program is free software; you can redistribute it and/or modify it
under the terms of the GNU General Public License as published by the
Free Software Foundation; either version 3, or (at your option) any
later version.

This program is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
GNU General Public License for more details.

You should have received a copy of the GNU General Public License
along with this program; see the file COPYING. If not, see
<http://www.gnu.org/licenses/>.  */

#pragma once

#define LOG_PUTS(s) \
  {                 \
    puts(s);        \
    klog_puts(s);   \
  }

#define LOG_PRINTF(s, ...)       \
  {                              \
    printf(s, __VA_ARGS__);      \
    klog_printf(s, __VA_ARGS__); \
  }

#define LOG_PERROR(s)                                                      \
  {                                                                        \
    printf("%s:%d:%s: %s\n", __FILE__, __LINE__, s, strerror(errno));      \
    klog_printf("%s:%d:%s: %s\n", __FILE__, __LINE__, s, strerror(errno)); \
  }

#define LOG_PT_PERROR(pid, s)                                                 \
  {                                                                           \
    printf("%s:%d:%s: %s\n", __FILE__, __LINE__, s, strerror(pt_errno(pid))); \
    klog_printf("%s:%d:%s: %s\n", __FILE__, __LINE__, s,                      \
                strerror(pt_errno(pid)));                                     \
  }

/**
 * Find the id of a process with the given name.
 **/
pid_t elfldr_find_pid(const char *name);

/**
 * Spawn a new process that executes the given ELF file.
 **/
pid_t elfldr_spawn(int stdio, char *const argv[], uint8_t *elf,
                   size_t elf_size);

/**
 * Load an ELF into the address space of a process with the given pid.
 **/
intptr_t elfldr_load(pid_t pid, uint8_t *elf);

/**
 * Create payload args in the address space of the process with the given pid.
 **/
intptr_t elfldr_payload_args(pid_t pid);

/**
 * Execute an ELF file in a given process.
 **/
int elfldr_exec(pid_t pid, int stdio, uint8_t *elf);

/**
 * Read an ELF from the given socket.
 **/
int elfldr_read(int fd, uint8_t **elf, size_t *elf_size);

int elfldr_sanity_check(uint8_t *elf, size_t elf_size);

int elfldr_raise_privileges(pid_t pid);