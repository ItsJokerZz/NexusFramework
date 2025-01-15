#pragma once

#define DAEMON_PORT 1337
#define RELAYS_PORT 8008
#define BUFFER_SIZE 1024

#define RESPONSE_OK "HTTP/1.1 200 OK\r\nContent-Type: text/plain\r\nContent-Length: %d\r\n\r\n%s"
#define RESPONSE_404 "HTTP/1.1 404 Not Found\r\nContent-Type: text/plain\r\nContent-Length: 67\r\n\r\n[OCAPI] The requested command cannot be found, check and try again."
#define RESPONSE_CONNECT "HTTP/1.1 200 OK\r\nContent-Type: text/plain\r\nContent-Length: 79\r\n\r\n[OCAPI] Please connect to enable interaction between the client and the daemon."
#define RESPONSE_ATTACH "HTTP/1.1 200 OK\r\nContent-Type: text/plain\r\nContent-Length: 74\r\n\r\n[OCAPI] Please attach to the apps' process before trying to access memory."

extern float version;
extern int8_t build;

extern bool unload,
    connected, attached;
    