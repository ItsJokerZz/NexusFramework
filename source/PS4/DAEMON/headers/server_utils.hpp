#pragma once

#define debug_log(fmt, ...) \
  if (DEBUG)             \
  log_message("[%s:%d->%s] ", fmt, __FILE__, __LINE__, __FUNCTION__, ##__VA_ARGS__)

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

extern void log_message(const char *fmt, ...);

extern std::string extract_param(const char *key,
                                 std::array<char, BUFFER_SIZE> buffer);
extern char *perform_get_request(const char *command);
extern char *decode_url(const char *url);

extern std::string generate_json(
    const std::unordered_map<std::string,
                             nlohmann::json> &data_entries);

extern bool is_port_open(int port);

void send_response(const char *message);
void send_response(const nlohmann::json &response_data);
void send_error_response(ErrorCode error_code);
void send_error_response(const std::string &message);