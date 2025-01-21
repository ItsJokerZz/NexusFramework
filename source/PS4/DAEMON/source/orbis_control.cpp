#include "../headers/includes.hpp"

enum
{
  MAIN_ON_STANDBY = 500,
  WORKING = 1000
};

#define SCE_KERNEL_EVF_WAITMODE_OR 0x02

static OrbisKernelEventFlag statemgr = NULL; // Kernel event flag handler

// Function to open the event flag and initialize statemgr
int sceSystemOpenStartMgr()
{
  if ((unsigned int)sceKernelOpenEventFlag(&statemgr, "SceSystemStateMgrInfo") != 0)
    return -1;
  return 0;
}

// Function to get the current system state
int sceSystemStateMgrGetCurrentState()
{
  uint64_t ret = 0;

  // Initialize statemgr if it's not already initialized
  if (!statemgr)
  {
    if (sceSystemOpenStartMgr() == -1)
      return -1;
  }

  // Poll for the event flag
  sceKernelPollEventFlag(statemgr, 0xFFFF, SCE_KERNEL_EVF_WAITMODE_OR, &ret);

  // If system is in WORKING state and certain condition is met, change state to MAIN_ON_STANDBY
  if ((int)ret == WORKING && sceKernelPollEventFlag(statemgr, 0x200000, SCE_KERNEL_EVF_WAITMODE_OR, 0) == 0)
    ret = MAIN_ON_STANDBY;

  // Log if system state is MAIN_ON_STANDBY
  if ((int)ret == MAIN_ON_STANDBY)
    log_message("SceSystemStateMgrGetCurrentState MAIN_ON_STANDBY");

  return (int)ret;
}

// Helper functions for checking system state
bool isRestMode()
{
  return sceSystemStateMgrGetCurrentState() == MAIN_ON_STANDBY;
}

bool isOn()
{
  return true;

  // return sceSystemStateMgrGetCurrentState() == WORKING;
}

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

void *unified_process(void *arg)
{
  if (isDaemon)
  {
    data.sockets.daemon.client = *static_cast<int *>(arg);
    std::fill(data.buffers.daemon.begin(), data.buffers.daemon.end(), 0);

    int bytes_received =
        sceNetRecv(data.sockets.daemon.client, data.buffers.daemon.data(),
                   data.buffers.daemon.size() - 1, 0);

    if (bytes_received > 0)
    {
      data.buffers.daemon[bytes_received] = '\0';
      const std::string request(data.buffers.daemon.data());

      if (request.substr(0, 14) == "GET / HTTP/1.1")
      {
        send_error_response(NO_COMMAND);

        return nullptr;
      }

      static const std::map<std::string, std::function<void()>> commands = {
          {"GET /connect", cmds::client::connection::connect},
          {"GET /unload", cmds::client::connection::unload},
          {"GET /get_proc_list", cmds::client::process::get_proc_list},
          {"GET /disconnect",
           []()
           { handle_command(cmds::client::connection::disconnect); }},
          {"GET /attach",
           []()
           { handle_command(cmds::client::connection::attach); }},
          {"GET /get_prx_version",
           []()
           { handle_command(cmds::client::connection::version); }},
          {"GET /get_fw_version",
           []()
           { handle_command(cmds::client::sys_info::get_fw); }},
          {"GET /get_sys_type",
           []()
           { handle_command(cmds::client::sys_info::sys_type); }},
          {"GET /get_temperature",
           []()
           { handle_command(cmds::client::sys_info::get_temp); }},
          {"GET /get_username",
           []()
           { handle_command(cmds::client::sys_info::get_user); }},
          {"GET /send_notify",
           []()
           { handle_command(cmds::client::sys_control::notify); }},
          {"GET /set_temp_limit",
           []()
           { handle_command(cmds::client::sys_control::temp_limit); }},
          {"GET /set_power_state",
           []()
           {
             handle_command(cmds::client::sys_control::set_power_state);
           }},
          {"GET /ring_buzzer",
           []()
           { handle_command(cmds::client::sys_control::ring_buzzer); }},
          //  {"GET /get_proc_list",
          //   []()
          //   { handle_command(cmds::client::process::get_proc_list); }},
          {"GET /get_pid_by_name",
           []()
           { handle_command(cmds::client::process::find_pid_by_name); }},
          {"GET /get_name_of_pid",
           []()
           { handle_command(cmds::client::process::find_name_of_pid); }},
          {"GET /load_module",
           []()
           { handle_command(cmds::client::process::load_module); }},
          {"GET /load_plugin",
           []()
           { handle_command(cmds::client::process::load_plugin); }},
          {"GET /rw_memory",
           []()
           { handle_command(cmds::client::process::rw_proc_mem); }}};

      typedef std::map<std::string, std::function<void()>>::const_iterator
          CommandIter;
      CommandIter it = std::find_if(
          commands.begin(), commands.end(),
          [&request](
              const std::pair<std::string, std::function<void()>> &pair)
          {
            return request.find(pair.first) != std::string::npos;
          });

      if (it != commands.end())
        it->second();
      else
        send_error_response(INVALID_CMD);
    }

    sceNetSocketClose(data.sockets.daemon.client);
  }
  else
  {
    data.sockets.relay.client = *static_cast<int *>(arg);
    std::fill(data.buffers.relay.begin(), data.buffers.relay.end(), 0);

    int bytes_received =
        sceNetRecv(data.sockets.relay.client, data.buffers.relay.data(),
                   data.buffers.relay.size() - 1, 0);

    if (bytes_received > 0)
    {
      data.buffers.relay[bytes_received] = '\0';
      const std::string request(data.buffers.relay.data());

      if (request.substr(0, 14) == "GET / HTTP/1.1")
      {
        send_error_response(NO_COMMAND);

        return nullptr;
      }

      static const std::map<std::string, std::function<void()>> commands = {
          {"GET /test",
           []()
           {
             send_response("done");
             if (!attached &&
                 strcmp(perform_get_request("attach"), "done") == 0)
               attached = true;
           }},
          {"GET /attach", cmds::daemon::attach_relay},
          {"GET /exec_prx", cmds::daemon::load_module},
          {"GET /load_plugin", cmds::daemon::start_plugin},
          {"GET /rw_memory", cmds::daemon::rw_proc_mem}};

      typedef std::map<std::string, std::function<void()>>::const_iterator
          CommandIter;
      CommandIter it = std::find_if(
          commands.begin(), commands.end(),
          [&request](
              const std::pair<std::string, std::function<void()>> &pair)
          {
            return request.find(pair.first) != std::string::npos;
          });

      if (it != commands.end())
        it->second();
      else
        send_error_response(INVALID_CMD);
    }

    sceNetSocketClose(data.sockets.relay.client);
  }

  return nullptr;
}

void *unified_thread(void *arg)
{
  OrbisNetSockaddr server_addr, client_addr;
  socklen_t client_addr_len = sizeof(client_addr);

  std::string socket_name = "[OCAPI] " + name + " Socket";
  void *socket_data = isDaemon ? (void *)&data.sockets.daemon : (void *)&data.sockets.relay;
  void *thread_data = isDaemon ? (void *)&data.threads.daemon : (void *)&data.threads.relay;

  int server_socket = -1, client_socket = -1;
  int bind_retries = 0, listen_retries = 0, accept_retries = 0;

  std::string buffer;

  while (!unloaded)
  {
    server_socket = sceNetSocket(socket_name.c_str(), ORBIS_NET_AF_INET, ORBIS_NET_SOCK_STREAM, 0);

    if (server_socket < 0)
    {
      buffer = name + " failed to create server socket, retrying in " + std::to_string(RETRY_DELAY_SECONDS) +
               " seconds... Attempt " + std::to_string(bind_retries + 1) + "/" + std::to_string(MAX_RETRY_ATTEMPTS);
      log_message("%s", buffer.c_str());

      if (++bind_retries >= MAX_RETRY_ATTEMPTS)
      {
        buffer = name + " failed to create server socket after " + std::to_string(MAX_RETRY_ATTEMPTS) + " attempts, unloading...";
        log_message("%s", buffer.c_str());
        unloaded = true;
        break;
      }

      sceKernelSleep(RETRY_DELAY_SECONDS);
      continue;
    }

    memset(&server_addr, 0, sizeof(server_addr));
    server_addr.len = sizeof(server_addr);
    server_addr.sa_family = ORBIS_NET_AF_INET;
    *(uint16_t *)server_addr.sa_data = sceNetHtons(port);
    memset(server_addr.sa_data + 2, 0, 4);

    if (sceNetBind(server_socket, &server_addr, sizeof(server_addr)) < 0)
    {
      buffer = name + " failed to bind server socket, retrying in " + std::to_string(RETRY_DELAY_SECONDS) +
               " seconds... Attempt " + std::to_string(bind_retries + 1) + "/" + std::to_string(MAX_RETRY_ATTEMPTS);

      log_message("%s", buffer.c_str());
      if (++bind_retries >= MAX_RETRY_ATTEMPTS)
      {
        buffer = name + " failed to bind server socket after " + std::to_string(MAX_RETRY_ATTEMPTS) + " attempts, unloading...";
        log_message("%s", buffer.c_str());
        unloaded = true;
        break;
      }
      sceNetSocketClose(server_socket);
      sceKernelSleep(RETRY_DELAY_SECONDS);
      continue;
    }

    if (sceNetListen(server_socket, 1) < 0)
    {
      buffer = name + " failed to listen on server socket, retrying in " + std::to_string(RETRY_DELAY_SECONDS) +
               " seconds... Attempt " + std::to_string(listen_retries + 1) + "/" + std::to_string(MAX_RETRY_ATTEMPTS);

      log_message("%s", buffer.c_str());
      if (++listen_retries >= MAX_RETRY_ATTEMPTS)
      {
        buffer = name + " failed to listen on server socket after " + std::to_string(MAX_RETRY_ATTEMPTS) + " attempts, unloading...";
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
      client_socket = sceNetAccept(server_socket, &client_addr, &client_addr_len);

      if (client_socket < 0)
      {
        buffer = name + " failed to accept client connection. Closing server socket and restarting...";
        log_message("%s", buffer.c_str());

        if (++accept_retries >= MAX_RETRY_ATTEMPTS)
        {
          buffer = name + " failed to accept client connection after " + std::to_string(MAX_RETRY_ATTEMPTS) + " attempts, unloading...";
          log_message("%s", buffer.c_str());
          unloaded = true;
          break;
        }

        sceNetSocketClose(server_socket);
        sceKernelSleep(RETRY_DELAY_SECONDS);

        server_socket = sceNetSocket(socket_name.c_str(), ORBIS_NET_AF_INET, ORBIS_NET_SOCK_STREAM, 0);
        if (server_socket < 0)
        {
          buffer = name + " failed to recreate server socket, retrying...";
          log_message("%s", buffer.c_str());
          continue;
        }

        memset(&server_addr, 0, sizeof(server_addr));
        server_addr.len = sizeof(server_addr);
        server_addr.sa_family = ORBIS_NET_AF_INET;
        *(uint16_t *)server_addr.sa_data = sceNetHtons(port);
        memset(server_addr.sa_data + 2, 0, 4);

        if (sceNetBind(server_socket, &server_addr, sizeof(server_addr)) < 0)
        {
          buffer = name + " failed to bind server socket after reset, retrying...";
          log_message("%s", buffer.c_str());
          continue;
        }

        if (sceNetListen(server_socket, 1) < 0)
        {
          buffer = name + " failed to listen on server socket after reset, retrying...";
          log_message("%s", buffer.c_str());
          continue;
        }

        buffer = name + " server socket successfully reset and is listening again.";
        log_message("%s", buffer.c_str());
        continue;
      }

      pthread_t *client_data = isDaemon
                                   ? &((struct serverData::threads::daemon *)thread_data)->client
                                   : &((struct serverData::threads::relay *)thread_data)->client;

      pthread_create(client_data, NULL, unified_process, &client_socket);
      pthread_detach(*client_data);
    }

    sceNetSocketClose(server_socket);
  }

  return nullptr;
}

extern "C" int32_t __wrap__init(size_t args, const void *argp)
{
  struct proc_info info;
  sys_sdk_proc_info(&info);
  isDaemon = (strcmp(info.titleid, DAEMON) == 0);
  port = isDaemon ? DAEMON_PORT : RELAYS_PORT;

  std::string buffer = "[OCAPI] Already loaded!";

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
        if (strstr(bufferRead, "[default]") && strstr(bufferRead, "[default]\n/data/GoldHEN/plugins/ItsJokerZz/OrbisControl.prx\n\n"))
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
  buffer = "[OCAPI] " + name + " server started";
  pthread_t &thread = isDaemon ? data.threads.daemon.server : data.threads.relay.server;
  if (thread == -1 && pthread_create(&thread, nullptr, unified_thread, nullptr) != 0)
  {
    buffer = "[OCAPI] Failed to create " + (std::transform(name.begin(), name.end(), name.begin(), ::tolower), name) + " thread!";
    sys_utils::text_notify(222, buffer.c_str());
    return 1;
  }

  sys_utils::text_notify(222, buffer.c_str());
  pthread_detach(thread);

  if (isDaemon)
  {
    sceKernelLoadStartModule("libSceUserService.sprx", 0, NULL, 0, NULL, NULL);
    sceUserServiceInitialize2();

    while (!unloaded || (DEBUG && isOn() && isRestMode()))
      sceKernelSleep(1);

    buffer = "[OCAPI] Unloaded!";
    sys_utils::text_notify(222, buffer.c_str());
    sceSystemServiceLoadExec("exit", 0);
  }

  return 0;
}