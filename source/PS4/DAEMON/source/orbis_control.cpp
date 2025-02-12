#include "../headers/includes.hpp"

void handle_request(const std::string &request)
{
  static const std::map<std::string, std::function<void()>> daemon_commands = {
      {"POST /test", cmds::client::process::write_proc_mem},

      {"GET /status", cmds::client::connection::status},
      {"GET /setup", cmds::client::connection::setup},
      {"GET /version", cmds::client::connection::version},
      {"GET /connect", cmds::client::connection::connect},
      {"GET /unload", cmds::client::connection::unload},
      {"GET /disconnect", []()
       { handle_command(cmds::client::connection::disconnect); }},
      {"GET /attach", []()
       { handle_command(cmds::client::connection::attach); }},

      {"GET /get_console_name", []()
       { handle_command(cmds::client::sys_info::get_name); }},
      {"GET /get_fw_version", []()
       { handle_command(cmds::client::sys_info::get_fw); }},
      {"GET /get_sys_type", []()
       { handle_command(cmds::client::sys_info::get_sys_type); }},
      {"GET /get_disk_info", []()
       { handle_command(cmds::client::sys_info::get_disk_info); }},
      {"GET /get_temperature", []()
       { handle_command(cmds::client::sys_info::get_temp); }},
      {"GET /get_username", []()
       { handle_command(cmds::client::sys_info::get_user); }},

      {"GET /get_proc_list", []()
       { handle_command(cmds::client::process::get_proc_list); }},
      {"GET /get_proc_info", []()
       { handle_command(cmds::client::process::get_proc_info); }},
      {"GET /get_pid_by_name", []()
       { handle_command(cmds::client::process::find_pid_by_name); }},
      {"GET /get_name_of_pid", []()
       { handle_command(cmds::client::process::find_name_of_pid); }},

      {"GET /set_temp_limit", []()
       { handle_command(cmds::client::sys_control::temp_limit); }},
      {"GET /set_power_state", []()
       { handle_command(cmds::client::sys_control::set_power_state); }},
      {"GET /ring_buzzer", []()
       { handle_command(cmds::client::sys_control::ring_buzzer); }},
      {"GET /send_notify", []()
       { handle_command(cmds::client::sys_control::notify); }},

      {"GET /read_memory", []()
       { handle_command(cmds::client::process::read_proc_mem); }},
      {"GET /write_memory", []()
       { handle_command(cmds::client::process::write_proc_mem); }},
      {"GET /alloc_memory", []()
       { handle_command(cmds::client::process::alloc_proc_mem); }},
      {"GET /free_memory", []()
       { handle_command(cmds::client::process::free_proc_mem); }},
      {"GET /start_plugin", []()
       { handle_command(cmds::client::process::start_plugin); }},
      {"GET /load_module", []()
       {
         handle_command(cmds::client::process::load_module);
       }},
      {"GET /unload_module", []()
       {
         handle_command(cmds::client::process::unload_module);
       }}};

  static const std::map<std::string, std::function<void()>> relay_commands = {
      {"GET /attach_relay", cmds::daemon::attach_relay},
      
      {"GET /read_memory", cmds::daemon::read_memory},
      {"POST /write_memory", cmds::daemon::write_memory},
      
      {"GET /alloc_memory", cmds::daemon::alloc_memory},
      {"GET /free_memory", cmds::daemon::free_memory},
      
      {"GET /start_plugin", cmds::daemon::start_plugin},
      
      {"GET /load_module", cmds::daemon::load_module},
      {"GET /unload_module", cmds::daemon::unload_module}};

  const auto &commands = isDaemon ? daemon_commands : relay_commands;
  auto it = std::find_if(commands.begin(), commands.end(),
                         [&request](const std::pair<std::string, std::function<void()>> &pair)
                         {
                           return request.find(pair.first) != std::string::npos;
                         });

  if (it != commands.end())
    it->second();
  else
    send_error_response(INVALID_CMD);
}

void *unified_process(void *arg)
{
  std::fill(data.buffer.begin(), data.buffer.end(), 0);

  int bytes_received = recv(data.sockets.client, data.buffer.data(), data.buffer.size() - 1, 0);
  if (bytes_received > 0)
  {
    data.buffer[bytes_received] = '\0';
    std::string request(data.buffer.data());
    if (request.substr(0, 14) == "GET / HTTP/1.1" ||
        request.substr(0, 15) == "POST / HTTP/1.1")
      send_error_response(NO_COMMAND);
    else
      handle_request(request);
  }

  shutdown(data.sockets.client, SHUT_RDWR);
  close(data.sockets.client);
  return nullptr;
}

void *unified_thread(void *arg)
{
  std::string message,
      socket_name = "[OrbisControl] " + name + " Socket";

  while (!unloaded)
  {
    data.sockets.server = socket(AF_INET, SOCK_STREAM, 0);
    if (data.sockets.server < 0)
    {
      log_message("%s failed to create socket", name.c_str());
      sceKernelSleep(1);
      continue;
    }

    // Set socket options to ensure rebinding works
    int opt = 1;
    setsockopt(data.sockets.server, SOL_SOCKET, SO_REUSEADDR, &opt, sizeof(opt));
    setsockopt(data.sockets.server, SOL_SOCKET, SO_REUSEPORT, &opt, sizeof(opt));

    // Force close any existing connection on this port
    struct sockaddr_in addr;
    memset(&addr, 0, sizeof(addr));
    addr.sin_family = AF_INET;
    addr.sin_port = htons(port);
    addr.sin_addr.s_addr = htonl(INADDR_ANY);

    // Try to bind immediately
    if (bind(data.sockets.server, (struct sockaddr *)&addr, sizeof(addr)) < 0)
    {
      log_message("%s failed to bind", name.c_str());
      close(data.sockets.server);
      sceKernelSleep(1);
      continue;
    }

    // Set the actual address after successful bind
    memset(&data.sockets.server_addr, 0, sizeof(data.sockets.server_addr));
    data.sockets.server_addr.sin_family = AF_INET;
    data.sockets.server_addr.sin_port = htons(port);
    if (DEBUG)
      data.sockets.server_addr.sin_addr.s_addr = 0;
    else
      data.sockets.server_addr.sin_addr.s_addr = htonl(isDaemon ? INADDR_ANY : INADDR_LOOPBACK);

    if (listen(data.sockets.server, 5) < 0)
    {
      log_message("%s failed to listen", name.c_str());
      close(data.sockets.server);
      sceKernelSleep(1);
      continue;
    }

    message = name + " has started a server listening on port " + std::to_string(port) + ".";
    log_message("%s", message.c_str());

    while (!unloaded)
    {
      data.sockets.client = accept(data.sockets.server,
                                   (struct sockaddr *)&data.sockets.client_addr,
                                   &data.sockets.client_addr_len);
      if (data.sockets.client < 0)
      {
        if (errno != EINTR && errno != EAGAIN)
        {
          log_message("%s accept failed, restarting server", name.c_str());
          break;
        }
        sceKernelSleep(1);
        continue;
      }

      pthread_t client_thread;
      pthread_create(&client_thread, nullptr, unified_process, &data.sockets.client);
      pthread_detach(client_thread);
    }

    shutdown(data.sockets.server, SHUT_RDWR);
    close(data.sockets.server);
    sceKernelSleep(1);
  }

  return nullptr;
}

void *telnet_server(void *arg)
{
  int server_socket = -1, client_socket = -1;
  struct sockaddr_in server_addr, client_addr;
  socklen_t client_addr_len = sizeof(client_addr);
  char buffer[1024];
  int port = 3333;

  // Create server socket
  server_socket = socket(AF_INET, SOCK_STREAM, 0);
  if (server_socket < 0)
  {
    perror("Failed to create server socket");
    return NULL;
  }

  // Setup server address
  memset(&server_addr, 0, sizeof(server_addr));
  server_addr.sin_family = AF_INET;
  server_addr.sin_addr.s_addr = INADDR_ANY; // Listen on all interfaces
  server_addr.sin_port = htons(port);

  // Bind the socket
  if (bind(server_socket, (struct sockaddr *)&server_addr,
           sizeof(server_addr)) < 0)
  {
    perror("Failed to bind socket");
    close(server_socket);
    return NULL;
  }

  // Start listening for client connections
  if (listen(server_socket, 1) < 0)
  {
    perror("Failed to listen on socket");
    close(server_socket);
    return NULL;
  }

  log_message("Telnet server listening on port %d...\n", port);

  while (!unloaded)
  {
    // Accept a client connection
    client_socket = accept(server_socket, (struct sockaddr *)&client_addr,
                           &client_addr_len);
    if (client_socket < 0)
    {
      perror("Failed to accept client connection");
      continue;
    }

    log_message("Client connected: %s\n", inet_ntoa(client_addr.sin_addr));

    // Send kernel log data or any other output to the client
    int logDevice =
        sceKernelOpen("/dev/klog", O_RDONLY, 0); // Open the kernel log device
    if (logDevice < 0)
    {
      perror("Failed to open kernel log device");
      close(client_socket);
      continue;
    }

    while (true)
    {
      int bytesRead = sceKernelRead(logDevice, buffer, sizeof(buffer) - 1);

      if (bytesRead > 0)
      {
        buffer[bytesRead] = '\0'; // Null-terminate the string

        // Split the buffer into lines and send them to the client one by one
        char *line = strtok(buffer, "\n"); // Tokenize by newline
        while (line != NULL)
        {
          send(client_socket, line, strlen(line), 0); // Send the line
          send(client_socket, "\r\n", 2, 0);          // Send newline after each line
          line = strtok(NULL, "\n");                  // Get next line
        }
      }

      // If the client disconnects or other condition, break the loop
      if (unloaded || bytesRead <= 0)
      {
        break;
      }
      usleep(100000); // Sleep a bit before reading more data
    }

    // Close the client socket after the communication ends
    close(client_socket);
    log_message("Client disconnected.\n");
  }

  // Close the server socket when done
  close(server_socket);
  return NULL;
}

void *send_udp_signal(void *arg)
{
  const char *message = "UDP_SEARCH_KEY*";

  while (true)
  {
    int sock = socket(AF_INET, SOCK_DGRAM, 0);
    if (sock < 0)
    {
      sceKernelSleep(10);
      continue;
    }

    int broadcast = 1;
    setsockopt(sock, SOL_SOCKET, SO_BROADCAST, &broadcast, sizeof(broadcast));

    sockaddr_in target_addr{};
    target_addr.sin_family = AF_INET;
    target_addr.sin_port = htons(13337);
    inet_pton(AF_INET, "255.255.255.255", &target_addr.sin_addr);

    ssize_t sent_bytes = sendto(sock, message, strlen(message), 0,
                                (struct sockaddr *)&target_addr, sizeof(target_addr));

    close(sock);
    sceKernelSleep(10);
  }

  return nullptr;
}

extern "C" int32_t __wrap__init(size_t args, const void *argp)
{
  struct proc_info info = {};
  sys_sdk_proc_info(&info);

  isDaemon = (strcmp(info.titleid, DAEMON_APP) == 0);
  port = isDaemon ? DAEMON_PORT : RELAYS_PORT;

  std::string message = "OrbisControl Already loaded!";

  if (isDaemon)
  {
    if (is_port_open(port))
    {
      text_notify(222, message.c_str());

      return 1;
    }

    mkdir("/update/PS4UPDATE.PUP", 0777);
    mkdir("/update/PS4UPDATE.PUP.net.temp", 0777);

    const char *file = "/user/data/GoldHEN/plugins.ini";
    int fd = open(file, O_RDONLY);
    std::string pluginEntry = "[default]\n/data/GoldHEN/plugins/ItsJokerZz/OrbisControl.prx\n\n";
    bool contentFound = false;

    if (fd != -1)
    {
      char readBuffer[1024];

      while (read(fd, readBuffer, sizeof(readBuffer) - 1) > 0)
      {
        if (strstr(readBuffer, pluginEntry.c_str()))
        {
          contentFound = true;
          break;
        }
      }

      close(fd);
    }

    if (!contentFound)
    {
      fd = open(file, O_WRONLY | O_APPEND | O_CREAT, S_IRUSR | S_IWUSR);
      if (fd != -1)
      {
        std::string pluginEntry = "[default]\n/data/GoldHEN/plugins/ItsJokerZz/OrbisControl.prx\n\n";
        write(fd, pluginEntry.c_str(), pluginEntry.length());
        log_message("Added OrbisControl to %s", file);
        close(fd);
      }
    }
  }

  name = isDaemon ? "Daemon" : "Relay";
  message = "[OrbisControl]\n" + name + " started";

  if (pthread_create(&data.threads.server, nullptr, unified_thread, nullptr) != 0)
    return 1;
  pthread_detach(data.threads.server);

  text_notify(222, message.c_str());

  if (isDaemon)
  {
    sceKernelLoadStartModule("libSceUserService.sprx", 0, NULL, 0, NULL, NULL);
    sceUserServiceInitialize2();

    /* klog server
       pthread_t log_thread;
       pthread_create(&log_thread, NULL, telnet_server, NULL);
       pthread_detach(log_thread);
    */

    pthread_t udpSearch_t;
    pthread_create(&udpSearch_t, NULL, send_udp_signal, NULL);
    pthread_detach(udpSearch_t);

    while (!unloaded)
      sceKernelSleep(1);

    unloaded = true;
    log_message("Unload signal has been received, unloading!");
    text_notify(222, "[OrbisControl] Unloaded!");
    sceSystemServiceLoadExec("exit", 0);
  }

  return 0;
}