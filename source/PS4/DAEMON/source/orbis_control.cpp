#include "../headers/includes.hpp"

void handle_command(void (*func)())
{
  bool *toggle = isDaemon ? &connected : &attached;

  if (*toggle)
    func();
  else
    send_error_response(toggle == &connected
                            ? NOT_CONNECTED
                            : NOT_ATTACHED); // Modifying toggle
}

void handle_request(const std::string &request, bool isDaemon)
{
  static const std::map<std::string, std::function<void()>> daemon_commands = {
      {"GET /connect", cmds::client::connection::connect},
      {"GET /unload", cmds::client::connection::unload},
      {"GET /get_proc_list", cmds::client::process::get_proc_list},
      {"GET /disconnect", []()
       { handle_command(cmds::client::connection::disconnect); }},
      {"GET /attach", []()
       { handle_command(cmds::client::connection::attach); }},
      {"GET /get_prx_version", []()
       { handle_command(cmds::client::connection::version); }},
      {"GET /get_fw_version", []()
       { handle_command(cmds::client::sys_info::get_fw); }},
      {"GET /get_sys_type", []()
       { handle_command(cmds::client::sys_info::sys_type); }},
      {"GET /get_temperature", []()
       { handle_command(cmds::client::sys_info::get_temp); }},
      {"GET /get_username", []()
       { handle_command(cmds::client::sys_info::get_user); }},
      {"GET /send_notify", []()
       { handle_command(cmds::client::sys_control::notify); }},
      {"GET /set_temp_limit", []()
       { handle_command(cmds::client::sys_control::temp_limit); }},
      {"GET /set_power_state", []()
       { handle_command(cmds::client::sys_control::set_power_state); }},
      {"GET /ring_buzzer", []()
       { handle_command(cmds::client::sys_control::ring_buzzer); }},
      {"GET /get_pid_by_name", []()
       { handle_command(cmds::client::process::find_pid_by_name); }},
      {"GET /get_name_of_pid", []()
       { handle_command(cmds::client::process::find_name_of_pid); }},
      {"GET /load_module", []()
       { handle_command(cmds::client::process::load_module); }},
      {"GET /load_plugin", []()
       { handle_command(cmds::client::process::load_plugin); }},
      {"GET /rw_memory", []()
       { handle_command(cmds::client::process::rw_proc_mem); }}};

  static const std::map<std::string, std::function<void()>> relay_commands = {
      {"GET /test", []()
       {
         send_response("done");
         if (!attached && strcmp(perform_get_request("attach"), "done") == 0)
           attached = true;
       }},
      {"GET /attach", cmds::daemon::attach_relay},
      {"GET /exec_prx", cmds::daemon::load_module},
      {"GET /load_plugin", cmds::daemon::start_plugin},
      {"GET /rw_memory", cmds::daemon::rw_proc_mem}};

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
  int client_socket = *static_cast<int *>(arg);

  if (isDaemon)
  {
    data.sockets.daemon.client = client_socket;
    std::fill(data.buffers.daemon.begin(), data.buffers.daemon.end(), 0);

    int bytes_received = sceNetRecv(data.sockets.daemon.client,
                                    data.buffers.daemon.data(),
                                    data.buffers.daemon.size() - 1, 0);

    if (bytes_received > 0)
    {
      data.buffers.daemon[bytes_received] = '\0';
      std::string request(data.buffers.daemon.data());

      if (request.substr(0, 14) == "GET / HTTP/1.1")
      {
        send_error_response(NO_COMMAND);
      }
      else
      {
        handle_request(request, true);
      }
    }

    sceNetSocketClose(data.sockets.daemon.client);
  }
  else
  {
    data.sockets.relay.client = client_socket;
    std::fill(data.buffers.relay.begin(), data.buffers.relay.end(), 0);

    int bytes_received = sceNetRecv(data.sockets.relay.client,
                                    data.buffers.relay.data(),
                                    data.buffers.relay.size() - 1, 0);

    if (bytes_received > 0)
    {
      data.buffers.relay[bytes_received] = '\0';
      std::string request(data.buffers.relay.data());

      if (request.substr(0, 14) == "GET / HTTP/1.1")
      {
        send_error_response(NO_COMMAND);
      }
      else
      {
        handle_request(request, false);
      }
    }

    sceNetSocketClose(data.sockets.relay.client);
  }

  return nullptr;
}

void *unified_thread(void *arg)
{
  std::string buffer;
  std::string socket_name = "[OrbisControl] " + name + " Socket";

  int server_socket = -1, client_socket = -1, retries = 0;

  auto create_server_socket = [&]() -> int
  {
    server_socket = sceNetSocket(socket_name.c_str(), ORBIS_NET_AF_INET, ORBIS_NET_SOCK_STREAM, 0);
    if (server_socket < 0)
    {
      buffer = name + " failed to create server socket, retrying in " + std::to_string(RETRY_DELAY_SECONDS) +
               " seconds... Attempt " + std::to_string(retries + 1) + "/" + std::to_string(MAX_RETRY_ATTEMPTS);
      log_message("%s", buffer.c_str());
    }
    return server_socket;
  };

  auto bind_server_socket = [&]() -> bool
  {
    memset(&data.sockets.server_addr, 0, sizeof(data.sockets.server_addr));
    data.sockets.server_addr.len = sizeof(data.sockets.server_addr);
    data.sockets.server_addr.sa_family = ORBIS_NET_AF_INET;
    *(uint16_t *)data.sockets.server_addr.sa_data = sceNetHtons(port);
    memset(data.sockets.server_addr.sa_data + 2, 0, 4);

    return sceNetBind(server_socket, &data.sockets.server_addr, sizeof(data.sockets.server_addr)) >= 0;
  };

  auto listen_server_socket = [&]() -> bool
  {
    return sceNetListen(server_socket, 1) >= 0;
  };

  while (!unloaded)
  {
    server_socket = create_server_socket();

    if (server_socket < 0)
    {
      if (++retries >= MAX_RETRY_ATTEMPTS)
      {
        buffer = name + " failed to create server socket after " + std::to_string(MAX_RETRY_ATTEMPTS) + " attempts, unloading...";
        log_message("%s", buffer.c_str());
        unloaded = true;
        break;
      }
      sceKernelSleep(RETRY_DELAY_SECONDS);
      continue;
    }

    if (!bind_server_socket() || !listen_server_socket())
    {
      buffer = name + " failed to bind or listen on server socket, retrying in " + std::to_string(RETRY_DELAY_SECONDS) +
               " seconds... Attempt " + std::to_string(retries + 1) + "/" + std::to_string(MAX_RETRY_ATTEMPTS);

      log_message("%s", buffer.c_str());

      if (++retries >= MAX_RETRY_ATTEMPTS)
      {
        buffer = name + " failed to bind or listen after " + std::to_string(MAX_RETRY_ATTEMPTS) + " attempts, unloading...";
        log_message("%s", buffer.c_str());
        unloaded = true;
        break;
      }

      sceNetSocketClose(server_socket);
      sceKernelSleep(RETRY_DELAY_SECONDS);
      continue;
    }

    buffer = name + " has started a server listening on port " + std::to_string(port) + ".";
    log_message("%s", buffer.c_str());

    while (!unloaded)
    {
      client_socket = sceNetAccept(server_socket, &data.sockets.client_addr, &data.sockets.client_addr_len);

      if (client_socket < 0)
      {
        buffer = name + " failed to accept client connection. Closing server socket and restarting...";
        log_message("%s", buffer.c_str());

        if (++retries >= MAX_RETRY_ATTEMPTS)
        {
          buffer = name + " failed to accept client connection after " + std::to_string(MAX_RETRY_ATTEMPTS) + " attempts, unloading...";
          log_message("%s", buffer.c_str());
          unloaded = true;
          break;
        }

        sceNetSocketClose(server_socket);
        sceKernelSleep(RETRY_DELAY_SECONDS);
        server_socket = create_server_socket();
        if (server_socket < 0)
        {
          buffer = name + " failed to recreate server socket, retrying...";
          log_message("%s", buffer.c_str());
          continue;
        }

        if (!bind_server_socket() || !listen_server_socket())
        {
          buffer = name + " failed to reset server socket, retrying...";
          log_message("%s", buffer.c_str());
          continue;
        }

        buffer = name + " server socket successfully reset and is listening again.";
        log_message("%s", buffer.c_str());
        continue;
      }

      void *thread = isDaemon ? (void *)&data.threads.daemon : (void *)&data.threads.relay;
      pthread_t *client_data = isDaemon
                                   ? &((struct serverData::threads::daemon *)thread)->client
                                   : &((struct serverData::threads::relay *)thread)->client;

      pthread_create(client_data, NULL, unified_process, &client_socket);
      pthread_detach(*client_data);
    }

    sceNetSocketClose(server_socket);
  }

  return nullptr;
}

extern "C" int32_t __wrap__init(size_t args, const void *argp)
{
  debug_log("%s", "TEST DEBUG MESSAGE");

  struct proc_info info;
  sys_sdk_proc_info(&info);

  isDaemon = (strcmp(info.titleid, DAEMON) == 0);
  port = isDaemon ? DAEMON_PORT : RELAYS_PORT;

  std::string buffer = "[OrbisControl] Already loaded!";

  if (isDaemon)
  {
    if (is_port_open(port))
    {
      if (!DEBUG)
        sys_utils::text_notify(222, buffer.c_str());
      else
        unloaded = true; // for testing purposes
      return 1;
    }

    const char *file = "/user/data/GoldHEN/plugins.ini";
    int fd = open(file, O_RDONLY);
    bool contentFound = false;

    if (fd != -1)
    {
      char bufferRead[1024];
      ssize_t bytesRead;
      while ((bytesRead = read(fd, bufferRead, sizeof(bufferRead))) > 0)
      {
        bufferRead[bytesRead] = '\0';
        if (strstr(bufferRead,
                   "[default]\n/data/GoldHEN/plugins/ItsJokerZz/OrbisControl.prx\n\n"))
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
        buffer = "[default]\n/data/GoldHEN/plugins/ItsJokerZz/OrbisControl.prx\n\n";
        write(fd, buffer.c_str(), buffer.length());
        log_message("Added OrbisControl to %s", file);
        close(fd);
      }
    }
  }

  name = isDaemon ? "Daemon" : "Relay";
  buffer = "[OrbisControl] " + name + " server started";
  pthread_t &thread = isDaemon ? data.threads.daemon.server : data.threads.relay.server;
  if (thread == -1 && pthread_create(&thread, nullptr, unified_thread, nullptr) != 0)
  {
    buffer = "[OrbisControl] Failed to create " + name + " thread!";
    sys_utils::text_notify(222, buffer.c_str());
    return 1;
  }

  sys_utils::text_notify(222, buffer.c_str());
  pthread_detach(thread);

  if (isDaemon)
  {
    sceKernelLoadStartModule("libSceUserService.sprx", 0, NULL, 0, NULL, NULL);
    sceUserServiceInitialize2();

    /* UNLOAD AT STARTUP / ON RESTMODE */
    while (!unloaded)
    {
      if (DEBUG && sys_utils::has_entered_restmode())
        unloaded = true;

      sceKernelSleep(1);
    }

    buffer = "[OrbisControl] Unloaded!";
    sys_utils::text_notify(222, buffer.c_str());
    sceSystemServiceLoadExec("exit", 0);
  }

  return 0;
}
