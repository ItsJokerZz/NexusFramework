#pragma once

#define DAEMON "NPXS21002"
#define DAEMON_PORT 1337
#define RELAYS_PORT 8008

#define RETRY_DELAY_SECONDS 30
#define CONNECTION_RETRY_MINUTES 5

#define BUFFER_SIZE 4096

#define RESPONSE_OK "HTTP/1.1 200 OK\r\nContent-Type: text/plain\r\nContent-Length: %d\r\n\r\n%s"

struct threadData
{
    std::array<char, BUFFER_SIZE> buffer{};
    pthread_t server_thread = 0, client_thread = 0;
    OrbisNetSockaddr server_addr{}, client_addr{};

    socklen_t client_addr_len = sizeof(OrbisNetSockaddr);
    int port = -1, server_socket = -1, client_socket = -1;
    std::map<std::string, std::function<void()>> commands{};
};

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
