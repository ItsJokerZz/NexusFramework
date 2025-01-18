#pragma once

#define DAEMON_PORT 1337
#define RELAYS_PORT 8008
#define BUFFER_SIZE 4096
#define RETRY_DELAY_SECONDS 30

#define RESPONSE_OK "HTTP/1.1 200 OK\r\nContent-Type: text/plain\r\nContent-Length: %d\r\n\r\n%s"


enum ErrorCode
{
    INVALID_CMD = 0,
    NOT_CONNECTED = 1,
    NOT_ATTACHED = 2,
    ERROR_COUNT
};

struct ErrorMessage
{
    const char *message;
};

namespace std
{
    template <>
    struct hash<ErrorCode>
    {
        size_t operator()(const ErrorCode &code) const
        {
            return hash<int>()(static_cast<int>(code)); // Convert enum to int and hash it
        }
    };
}

// Declare error_messages as an unordered_map with ErrorCode keys and ErrorMessage values
extern std::unordered_map<ErrorCode, ErrorMessage> error_messages;

extern bool unload, connected, attached;

extern int32_t module_start(int64_t args, const void *argp);
extern "C" int32_t __wrap__init(size_t args, const void *argp);
