#pragma once

#define DAEMON_PORT 1337
#define RELAYS_PORT 8008
#define BUFFER_SIZE 4096
#define RETRY_DELAY_SECONDS 30

#define RESPONSE_OK "HTTP/1.1 200 OK\r\nContent-Type: text/plain\r\nContent-Length: %d\r\n\r\n%s"

enum ErrorCode
{
    INVALID_CMD,
    NOT_CONNECTED,
    NOT_ATTACHED,
    UNKNOWN_ERROR,
    INVALID_ARGS,
    ERROR_COUNT
};

struct ErrorMessage
{
    const char *message;
};

extern std::array<ErrorMessage, ERROR_COUNT> error_messages;

extern bool unload, connected, attached;

extern int32_t module_start(int64_t args, const void *argp);
extern "C" int32_t __wrap__init(size_t args, const void *argp);
