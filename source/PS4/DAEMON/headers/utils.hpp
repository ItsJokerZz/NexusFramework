#pragma once

struct proc_list_entry
{
    char p_comm[32];
    int pid;
} __attribute__((packed));

#define log_message(fmt, ...)                                                                    \
    do                                                                                           \
    {                                                                                            \
        char _msg_buffer[512];                                                                   \
        time_t _raw_time;                                                                        \
        struct tm *_time_info;                                                                   \
        char _time_buffer[80];                                                                   \
        time(&_raw_time);                                                                        \
        _time_info = localtime(&_raw_time);                                                      \
        strftime(_time_buffer, sizeof(_time_buffer), "%m/%d/%Y @ %I:%M:%S%p", _time_info);       \
        snprintf(_msg_buffer, sizeof(_msg_buffer), "[OCAPI %.2fb%d] %s: (%s:%d->%s) " fmt "\n",  \
                 VERSION, BUILD, _time_buffer, __FILE__, __LINE__, __FUNCTION__, ##__VA_ARGS__); \
        sceKernelDebugOutText(0, _msg_buffer);                                                   \
    } while (0)

asm("orbis_syscall:\n"
    "movq $0, %rax\n"
    "movq %rcx, %r10\n"
    "syscall\n"
    "jb err\n"
    "retq\n"
    "err:\n"
    "pushq %rax\n"
    "callq __error\n"
    "popq %rcx\n"
    "movl %ecx, 0(%rax)\n"
    "movq $0xFFFFFFFFFFFFFFFF, %rax\n"
    "movq $0xFFFFFFFFFFFFFFFF, %rdx\n"
    "retq\n");
int orbis_syscall(int num, ...);

extern int sys_proc_list(struct proc_list_entry *procs, uint64_t *num);
extern int find_process_pid(const char *proc_name, int *pid);
extern char *perform_get_request(const char *cmd);
extern bool is_relay_running();
extern char *decode_url(const char *url);
void send_response(const char *msg, int socket, bool &toggle);
void send_formatted_response(const char *message, int socket, bool *toggle);
void handle_command(void (*func)(), int socket, bool &toggle);
