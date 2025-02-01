#include "../headers/includes.hpp"

enum
{
  MAIN_ON_STANDBY = 500,
  WORKING = 1000
};

static OrbisKernelEventFlag statemgr = NULL;

int sceSystemOpenStartMgr()
{
  if (sceKernelOpenEventFlag(&statemgr, "SceSystemStateMgrInfo") != 0)
    return -1;
  return 0;
}

int sceSystemStateMgrGetCurrentState()
{
  uint64_t ret = 0;
  if (!statemgr)
  {
    if (sceSystemOpenStartMgr() == -1)
      return -1;
  }
  sceKernelPollEventFlag(statemgr, 0xFFFF, SCE_KERNEL_EVF_WAITMODE_OR, &ret);
  if ((int)ret == WORKING &&
      sceKernelPollEventFlag(statemgr, 0x200000, SCE_KERNEL_EVF_WAITMODE_OR, 0) == 0)
    ret = MAIN_ON_STANDBY;
  if ((int)ret == MAIN_ON_STANDBY)
    log_message("SceSystemStateMgrGetCurrentState MAIN_ON_STANDBY");
  return (int)ret;
}

bool isRestMode()
{
  return sceSystemStateMgrGetCurrentState() == MAIN_ON_STANDBY;
}

bool isOn()
{
  return true;
  // return sceSystemStateMgrGetCurrentState() == WORKING;
}

void handle_request(const std::string &request)
{
  static const std::map<std::string, std::function<void()>> daemon_commands = {
      {"POST /test", cmds::client::process::write_proc_mem},
      {"GET /version", cmds::client::connection::version},
      {"GET /connect", cmds::client::connection::connect},
      {"GET /unload", cmds::client::connection::unload},
      {"GET /disconnect", []()
       { handle_command(cmds::client::connection::disconnect); }},
      {"GET /attach", []()
       { handle_command(cmds::client::connection::attach); }},
      {"GET /get_fw_version", []()
       { handle_command(cmds::client::sys_info::get_fw); }},
      {"GET /get_sys_type", []()
       { handle_command(cmds::client::sys_info::sys_type); }},
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
       { handle_command(cmds::client::process::alloc_proc_mem); }},
      {"GET /start_plugin", []()
       { handle_command(cmds::client::process::start_plugin); }},
      {"GET /load_module", []()
       { handle_command(cmds::client::process::load_module); }}};

  static const std::map<std::string, std::function<void()>> relay_commands = {
      {"GET /attach_relay", cmds::daemon::attach_relay},
      {"GET /read_memory", cmds::daemon::read_memory},
      {"POST /write_memory", cmds::daemon::write_memory},
      {"GET /alloc_memory", cmds::daemon::alloc_memory},
      {"GET /free_memory", cmds::daemon::free_memory},
      {"GET /start_plugin", cmds::daemon::start_plugin},
      {"GET /load_module", cmds::daemon::load_module},
      {"GET /test", []()
       {
         send_response("done");
         if (!attached && perform_http_request("attach") == "done")
           attached = true;
       }}};

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

  int bytes_received = sceNetRecv(data.sockets.client, data.buffer.data(), data.buffer.size() - 1, 0);
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

  sceNetSocketAbort(data.sockets.client, 0);
  sceNetSocketClose(data.sockets.client);

  return nullptr;
}

void *unified_thread(void *arg)
{
  int retries = 0;

  std::string message,
      socket_name = "[OrbisControl] " + name + " Socket";

  auto create_server_socket = [&]() -> int
  {
    int sock = sceNetSocket(socket_name.c_str(), ORBIS_NET_AF_INET, ORBIS_NET_SOCK_STREAM, 0);
    if (sock < 0)
    {
      message = name + " failed to create server socket, retrying in " +
                std::to_string(RETRY_DELAY_SECONDS) + " seconds... Attempt " +
                std::to_string(retries + 1) + "/" + std::to_string(MAX_RETRY_ATTEMPTS);
      log_message("%s", message.c_str());
    }
    return sock;
  };

  auto bind_server_socket = [&]() -> bool
  {
    memset(&data.sockets.server_addr, 0, sizeof(data.sockets.server_addr));
    data.sockets.server_addr.len = sizeof(data.sockets.server_addr);
    data.sockets.server_addr.sa_family = ORBIS_NET_AF_INET;
    *(uint16_t *)data.sockets.server_addr.sa_data = sceNetHtons(port);
    if (DEBUG)
      memset(data.sockets.server_addr.sa_data + 2, 0, 4);
    else
      *(uint32_t *)(data.sockets.server_addr.sa_data + 2) =
          sceNetHtonl(isDaemon ? 0x00000000 : 0x7F000001);
    return sceNetBind(data.sockets.server, &data.sockets.server_addr,
                      sizeof(data.sockets.server_addr)) >= 0;
  };

  auto listen_server_socket = [&]() -> bool
  {
    return sceNetListen(data.sockets.server, 1) >= 0;
  };

  while (!unloaded)
  {
    data.sockets.server = create_server_socket();
    if (data.sockets.server < 0)
    {
      if (++retries >= MAX_RETRY_ATTEMPTS)
      {
        message = name + " failed to create server socket after " +
                  std::to_string(MAX_RETRY_ATTEMPTS) + " attempts, unloading...";
        log_message("%s", message.c_str());
        unloaded = true;
        break;
      }
      sceKernelSleep(RETRY_DELAY_SECONDS);
      continue;
    }

    if (!bind_server_socket() || !listen_server_socket())
    {
      message = name + " failed to bind or listen on server socket, retrying in " +
                std::to_string(RETRY_DELAY_SECONDS) + " seconds... Attempt " +
                std::to_string(retries + 1) + "/" + std::to_string(MAX_RETRY_ATTEMPTS);
      log_message("%s", message.c_str());
      if (++retries >= MAX_RETRY_ATTEMPTS)
      {
        message = name + " failed to bind or listen after " +
                  std::to_string(MAX_RETRY_ATTEMPTS) + " attempts, unloading...";
        log_message("%s", message.c_str());
        unloaded = true;
        break;
      }
      sceNetSocketAbort(data.sockets.server, 0);
      sceNetSocketClose(data.sockets.server);
      sceKernelSleep(RETRY_DELAY_SECONDS);
      continue;
    }

    message = name + " has started a server listening on port " + std::to_string(port) + ".";
    log_message("%s", message.c_str());

    while (!unloaded)
    {
      data.sockets.client = sceNetAccept(data.sockets.server, &data.sockets.client_addr,
                                         &data.sockets.client_addr_len);
      if (data.sockets.client < 0)
      {
        message = name + " failed to accept client connection. Restarting server socket...";
        log_message("%s", message.c_str());
        if (++retries >= MAX_RETRY_ATTEMPTS)
        {
          message = name + " failed to accept client connection after " +
                    std::to_string(MAX_RETRY_ATTEMPTS) + " attempts, unloading...";
          log_message("%s", message.c_str());
          unloaded = true;
          break;
        }
        sceNetSocketAbort(data.sockets.server, 0);
        sceNetSocketClose(data.sockets.server);
        sceKernelSleep(RETRY_DELAY_SECONDS);
        break; // break inner loop to recreate server socket
      }

      pthread_t client_thread;
      pthread_create(&client_thread, nullptr, unified_process, &data.sockets.client);
      pthread_detach(client_thread);
    }
    sceNetSocketAbort(data.sockets.server, 0);
    sceNetSocketClose(data.sockets.server);
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
      if (!DEBUG)
        text_notify(222, message.c_str());
      else
        unloaded = true; // for testing purposes until i can get the socket to rebind efficiently on wakeup. 

      return 1;
    }

    mkdir("/update/PS4UPDATE.PUP", 0777);
    mkdir("/update/PS4UPDATE.PUP.net.temp", 0777);

    const char *file = "/user/data/GoldHEN/plugins.ini";
    int fd = open(file, O_RDONLY);
    bool contentFound = false;
    if (fd != -1)
    {
      char readBuffer[1024];
      ssize_t bytesRead;
      while ((bytesRead = read(fd, readBuffer, sizeof(readBuffer))) > 0)
      {
        readBuffer[bytesRead] = '\0';
        if (strstr(readBuffer, "[default]\n/data/GoldHEN/plugins/ItsJokerZz/OrbisControl.prx\n\n"))
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
      //  jailbreak_backup jb;
      //  sys_sdk_jailbreak(&jb);

        pthread_t log_thread;
        pthread_create(&log_thread, NULL,
                       telnet_server, NULL);

        pthread_detach(log_thread);
        */

    jailbreak_backup jb;
    bool wasRestMode = false;

    while (!unloaded)
    {
      if (isRestMode())
        wasRestMode = true;
      else if (wasRestMode)
      {
        if (!DEBUG)
          unloaded = true;
        sys_sdk_jailbreak(&jb);
        text_notify(222, "re-jailbroke; for safety");
        wasRestMode = false;
        unloaded = !DEBUG;
      }
      sceKernelSleep(1);
    }

    unloaded = true;

    log_message("[OrbisControl] Unloaded!");
    text_notify(222, "[OrbisControl] Unloaded!");
    sceSystemServiceLoadExec("exit", 0);
  }

  return 0;
}