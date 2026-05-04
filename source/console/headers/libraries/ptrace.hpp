#pragma once

intptr_t pt_resolve(pid_t pid, const char *nid, int handle = -1);

int pt_attach(pid_t pid);
int pt_detach(pid_t pid, int sig);

int pt_step(pid_t pid);
int pt_continue(pid_t pid, int sig);

int pt_getint(pid_t pid, intptr_t addr);
int pt_setint(pid_t pid, intptr_t addr, int val);

char pt_getchar(pid_t pid, intptr_t addr);
short pt_getshort(pid_t pid, intptr_t addr);
long pt_getlong(pid_t pid, intptr_t addr);

int pt_setchar(pid_t pid, intptr_t addr, char val);
int pt_setshort(pid_t pid, intptr_t addr, short val);
int pt_setlong(pid_t pid, intptr_t addr, long val);

int pt_copyin(pid_t pid, const void *buf, intptr_t addr, size_t len);
int pt_copyout(pid_t pid, intptr_t addr, void *buf, size_t len);

int pt_getregs(pid_t pid, struct reg *r);
int pt_setregs(pid_t pid, const struct reg *r);

long pt_call_single_step(pid_t pid, intptr_t addr, ...);
long pt_call_continue(pid_t pid, intptr_t addr, ...);
long pt_call_trampoline(pid_t pid, intptr_t addr, uint64_t a1, uint64_t a2,
                        uint64_t a3, uint64_t a4, uint64_t a5, uint64_t a6);
long pt_call_stub(pid_t pid, intptr_t addr, uint64_t a1, uint64_t a2,
                  uint64_t a3, uint64_t a4, uint64_t a5, uint64_t a6);
                  

long pt_syscall(pid_t pid, int sysno, ...);

intptr_t pt_mmap(pid_t pid, intptr_t addr, size_t len, int prot, int flags,
                 int fd, off_t off);

int pt_msync(pid_t pid, intptr_t addr, size_t len, int flags);
int pt_munmap(pid_t pid, intptr_t addr, size_t len);
int pt_mprotect(pid_t pid, intptr_t addr, size_t len, int prot);

int pt_socket(pid_t pid, int domain, int type, int protocol);
int pt_setsockopt(pid_t pid, int fd, int level, int optname, intptr_t optval,
                  socklen_t optlen);
int pt_close(pid_t pid, int fd);
int pt_bind(pid_t pid, int sockfd, intptr_t addr, uint32_t addrlen);
ssize_t pt_recvmsg(pid_t pid, int fd, intptr_t msg, int flags);

int pt_dup2(pid_t pid, int oldfd, int newfd);
int pt_rdup(pid_t pid, pid_t other_pid, int fd);
int pt_pipe(pid_t pid, intptr_t pipefd);

int pt_errno(pid_t pid);

intptr_t pt_sceKernelGetProcParam(pid_t pid);

int pt_dynlib_get_proc_param(pid_t pid, intptr_t param_ptr, intptr_t size_ptr);
int pt_dynlib_process_needed_and_relocate(pid_t pid);
