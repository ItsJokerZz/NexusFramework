#pragma once

#define DAEMON "NPXS21002"
#define LOCAHOST "127.0.0.1"
#define DAEMON_PORT 1337
#define RELAYS_PORT 5000
#define RPC_PORT 7000
#define BUFFER_SIZE 4096

#define RETRY_DELAY_SECONDS 5
#define MAX_RETRY_ATTEMPTS 10

#define RESPONSE_OK                                                 \
  "HTTP/1.1 200 OK\r\nContent-Type: text/plain\r\nContent-Length: " \
  "%d\r\n\r\n%s"

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

extern bool DEBUG, isDaemon, unloaded, connected, attached;

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

    OrbisNetSockaddr server_addr = {};
    OrbisNetSockaddr client_addr = {};
    socklen_t client_addr_len = {};
    
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
      pthread_t server = -1;
      pthread_t client = -1;
    } daemon;

    struct relay
    {
      pthread_t server = -1;
      pthread_t client = -1;
    } relay;

  } threads;
};

extern serverData data;

extern uint16_t port;
extern std::string name;