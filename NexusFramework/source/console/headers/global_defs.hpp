#pragma once

#define NAME "NexusFramework"

#define LOG_FILE "/data/" NAME ".log"

inline auto start_time = std::chrono::steady_clock::now();

inline bool unloaded = false, connected = false;

struct serverData {
  struct {
    int server = -1, client = -1;

    struct sockaddr_in server_addr, client_addr;

    socklen_t client_addr_len = sizeof(struct sockaddr_in);
  } sockets;

  struct {
    pthread_t server, client;
  } threads;

  std::string buffer;
};

inline serverData server;

#define UNUSED(x) (void)(x)
