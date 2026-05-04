#include "headers.hpp"

extern bool g_is_library_request;

namespace threads
{
  void *API(void *)
  {
    constexpr useconds_t SLEEP_SHORT = 50'000;
    constexpr useconds_t SLEEP_LONG = 100'000;

    auto close_socket = [](int &sock)
    {
      if (sock >= 0)
      {
        close(sock);
        sock = -1;
      }
    };

    while (!unloaded)
    {
      int &server_fd = server.sockets.server;
      server_fd = socket(AF_INET, SOCK_STREAM, 0);
      if (server_fd < 0)
      {
        usleep(SLEEP_SHORT);
        continue;
      }

      auto set_option = [&](int level, int optname, int value)
      {
        return setsockopt(server_fd, level, optname, &value, sizeof(value)) == 0;
      };

      if (!set_option(SOL_SOCKET, SO_REUSEADDR, 1) ||
          !set_option(SOL_SOCKET, SO_REUSEPORT, 1))
      {
        close_socket(server_fd);
        usleep(SLEEP_SHORT);
        continue;
      }

      server.sockets.server_addr.sin_family = AF_INET;
      server.sockets.server_addr.sin_port = htons(DAEMON_PORT);
      server.sockets.server_addr.sin_addr.s_addr = INADDR_ANY;

      if (bind(server_fd,
               reinterpret_cast<sockaddr *>(&server.sockets.server_addr),
               sizeof(server.sockets.server_addr)) < 0)
      {
        close_socket(server_fd);
        usleep(SLEEP_SHORT);
        continue;
      }

      if (listen(server_fd, SOMAXCONN) < 0)
      {
        close_socket(server_fd);
        usleep(SLEEP_SHORT);
        continue;
      }

      log_message("API server started on port %d.", DAEMON_PORT);

      while (!unloaded)
      {
        server.sockets.client_addr_len = sizeof(server.sockets.client_addr);
        int &client_fd = server.sockets.client;
        client_fd = accept(
            server_fd, reinterpret_cast<sockaddr *>(&server.sockets.client_addr),
            &server.sockets.client_addr_len);

        if (client_fd < 0)
        {
          if (errno != EINTR && errno != EAGAIN)
            break;
          usleep(SLEEP_SHORT);
          continue;
        }

        int flag = 1;
        setsockopt(client_fd, IPPROTO_TCP, TCP_NODELAY, &flag, sizeof(flag));

        g_is_library_request = false;

        handle_client();
        close_socket(client_fd);
      }

      if (server_fd >= 0)
      {
        shutdown(server_fd, SHUT_RDWR);
        close_socket(server_fd);
      }
      usleep(SLEEP_LONG);
    }

    return nullptr;
  }
}