#pragma once

#define DAEMON "NPXS21002"
#define DAEMON_PORT 1337
#define RELAYS_PORT 5000
#define RPC_PORT 7000

#define RETRY_DELAY_SECONDS 30
#define RETRY_DELAY_MINUTES 5

#define BUFFER_SIZE 4096

#define RESPONSE_OK "HTTP/1.1 200 OK\r\nContent-Type: text/plain\r\nContent-Length: %d\r\n\r\n%s"

enum ErrorCode
{
    NO_COMMAND,
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

enum power_state
{
    DO_NOTHING = -1,
    POWER_OFF = 31,
    RESTART = 30,
    RESTMODE = 1,
};


extern int32_t module_start(int64_t args, const void *argp);
extern int32_t plugin_load(int64_t args, const void *argp);
extern "C" int32_t __wrap__init(size_t args, const void *argp);
