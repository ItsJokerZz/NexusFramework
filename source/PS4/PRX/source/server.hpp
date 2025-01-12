#pragma once

#include "includes.hpp"

#define PORT 1337
#define BUFFER_SIZE 1024
#define RESPONSE_OK "HTTP/1.1 200 OK\r\nContent-Type: text/plain\r\nContent-Length: %d\r\n\r\n%s"
#define RESPONSE_404 "HTTP/1.1 404 Not Found\r\nContent-Type: text/plain\r\nContent-Length: 28\r\n\r\n[OCAPI] Web server is alive!"
#define RESPONSE_CONNECT "HTTP/1.1 404 Not Found\r\nContent-Type: text/plain\r\nContent-Length: 15\r\n\r\n[OCAPI] Connect"

extern float version;

extern bool unload,
    connected, attached;

namespace OrbisControl
{
    extern char
        buffer[BUFFER_SIZE],
        response[BUFFER_SIZE];

    extern int
        server_sock,
        client_sock;

    void PrintMsgToUART(const char *msg);

    char *DecodeURL(const char *url);
    void SendResponse(const char *msg);
    void HandleCommand(void (*func)());
    void *HandleClients(void *arg);
    void StartServer();
}
