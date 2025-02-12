#pragma once

#define DAEMON_APP "NPXS21002" /* ScePartyDaemon */
#define LOCALHOST "127.0.0.1"
#define DAEMON_PORT 1337
#define RELAYS_PORT 5000
#define RPC_PORT 7000
#define BUFFER_SIZE 16384     // 16kb
#define HOME_MENU "NPXS21001" /* SceShellUI */

#define RETRY_DELAY_SECONDS 5
#define MAX_RETRY_ATTEMPTS 12

#define RESPONSE_OK "HTTP/1.1 200 OK\r\nContent-Type: text/plain\r\nContent-Length: %d\r\n\r\n%s"
#define RESPONSE_OK_FILE "HTTP/1.1 200 OK\r\nContent-Type: application/octet-stream\r\nContent-Length: %ld\r\nContent-Disposition: attachment; filename=\"%s\"\r\n\r\n"

#define SCE_KERNEL_EVF_WAITMODE_OR 0x02

#define SYS_PROC_ALLOC 1
#define SYS_PROC_FREE 2

enum ErrorCode
{
  _DEBUGGING,
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

extern std::array<ErrorMessage, ERROR_COUNT>
    error_messages;

extern bool DEBUG, isDaemon, unloaded,
    connected, attached, unload_on_rest;

struct serverData
{
  struct
  {
    int server = -1, client = -1;
    struct sockaddr_in server_addr = {},
                      client_addr = {};
    socklen_t client_addr_len = sizeof(struct sockaddr_in);
  } sockets;

  struct
  {
    pthread_t server = -1,
              client = -1;
  } threads;

  std::array<char,
             BUFFER_SIZE>
      buffer = {};
};

extern serverData data;

extern uint16_t port;
extern std::string name;