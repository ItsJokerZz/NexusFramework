#pragma once

#define log_message(fmt, ...)                                                                    \
    do                                                                                           \
    {                                                                                            \
        char _msg_buffer[512];                                                                   \
        auto now = std::chrono::system_clock::to_time_t(std::chrono::system_clock::now());       \
        struct tm *_time_info = std::localtime(&now);                                            \
        int estern_offset = 5;                                                                   \
        _time_info->tm_hour -= estern_offset;                                                    \
        if (_time_info->tm_hour < 0)                                                             \
        {                                                                                        \
            _time_info->tm_hour += 24;                                                           \
            if (_time_info->tm_mday > 1)                                                         \
                _time_info->tm_mday -= 1;                                                        \
            else                                                                                 \
            {                                                                                    \
                _time_info->tm_mday = 31;                                                        \
                _time_info->tm_mon -= 1;                                                         \
            }                                                                                    \
        }                                                                                        \
        char _time_buffer[80];                                                                   \
        std::strftime(_time_buffer, sizeof(_time_buffer), "%m/%d/%Y @ %I:%M:%S% p", _time_info); \
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

enum power_state
{
    DO_NOTHING = -1,
    POWER_OFF = 31,
    RESTART = 30,
    RESTMODE = 1,
};

extern std::string extract_param(const char *key, std::array<char, BUFFER_SIZE> buffer);
extern char *perform_get_request(const char *command);
extern bool is_relay_running();
extern char *decode_url(const char *url);
extern std::string generate_json(const std::unordered_map<std::string, nlohmann::json> &data_entries);

void send_response(const char *message);
void send_response(const nlohmann::json &response_data);
void send_error_response(ErrorCode error_code);
void send_error_response(const std::string &message);

void handle_command(void (*func)());