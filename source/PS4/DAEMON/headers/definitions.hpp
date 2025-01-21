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

extern bool isDaemon, unloaded, connected, attached;
extern "C" int32_t __wrap__init(size_t args, const void *argp);

struct serverData
{
    struct sockets
    {
        struct daemon
        {
            int server = -1;
            int client = -1;
        } daemon;

        struct relay
        {
            int server = -1;
            int client = -1;
        } relay;

    } sockets;

    struct buffers
    {
        std::array<char, BUFFER_SIZE> relay = {};
        std::array<char, BUFFER_SIZE> daemon = {};
    } buffers;

    struct threads
    {
        struct daemon
        {
            pthread_t main = -1;
            pthread_t client = -1;
        } daemon;

        struct relay
        {
            pthread_t main = -1;
            pthread_t client = -1;
        } relay;

    } threads;
};

extern serverData data;
