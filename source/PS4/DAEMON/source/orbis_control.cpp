#include "../headers/includes.hpp"

void handle_command(void (*func)())
{
    int socket = (data.sockets.daemon.client != -1) ? data.sockets.daemon.client : data.sockets.relay.client;
    bool *toggle = (data.sockets.daemon.client != -1) ? &connected : &attached;

    if (*toggle)
        func();
    else
        send_error_response(toggle == &connected ? NOT_CONNECTED : NOT_ATTACHED); // Modifying toggle
}

void *unified_process(void *arg)
{
    if (isDaemon)
    {
        data.sockets.daemon.client = *static_cast<int *>(arg);
        std::fill(data.buffers.daemon.begin(), data.buffers.daemon.end(), 0);

        int bytes_received = sceNetRecv(data.sockets.daemon.client, data.buffers.daemon.data(), data.buffers.daemon.size() - 1, 0);

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
                {"GET /get_proc_list", []()
                 { handle_command(cmds::client::process::get_proc_list); }},
                {"GET /get_pid_by_name", []()
                 { handle_command(cmds::client::process::find_pid_by_name); }},
                {"GET /get_name_of_pid", []()
                 { handle_command(cmds::client::process::find_name_of_pid); }},
                {"GET /load_module", []()
                 { handle_command(cmds::client::process::load_module); }},
                {"GET /load_plugin", []()
                 { handle_command(cmds::client::process::load_plugin); }},
                {"GET /rw_memory", []()
                 { handle_command(cmds::client::process::rw_proc_mem); }},
            };

            typedef std::map<std::string, std::function<void()>>::const_iterator CommandIter;
            CommandIter it = std::find_if(commands.begin(), commands.end(),
                                          [&request](const std::pair<std::string, std::function<void()>> &pair)
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

        int bytes_received = sceNetRecv(data.sockets.relay.client, data.buffers.relay.data(), data.buffers.relay.size() - 1, 0);

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
                {"GET /test", []()
                 {
                     send_response("done");
                     if (!attached && strcmp(perform_get_request("attach"), "done") == 0)
                         attached = true;
                 }},
                {"GET /ping", cmds::daemon::ping_relay},
                {"GET /attach", cmds::daemon::attach_relay},
                {"GET /exec_prx", cmds::daemon::load_module},
                {"GET /load_plugin", cmds::daemon::start_plugin},
                {"GET /rw_memory", cmds::daemon::rw_proc_mem}};

            typedef std::map<std::string, std::function<void()>>::const_iterator CommandIter;
            CommandIter it = std::find_if(commands.begin(), commands.end(),
                                          [&request](const std::pair<std::string, std::function<void()>> &pair)
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
    uint16_t port = isDaemon ? DAEMON_PORT : RELAYS_PORT;

    const char *socket_name = isDaemon ? "daemon.server" : "relay.server";
    void *socket_data = isDaemon ? (void *)&data.sockets.daemon : (void *)&data.sockets.relay;
    void *thread_data = isDaemon ? (void *)&data.threads.daemon : (void *)&data.threads.relay;
    int server_socket = -1, client_socket = -1, bind_retries = 0, listen_retries = 0, accept_retries = 0;

    bool retrying = false;

    while (!unloaded)
    {
        server_socket = sceNetSocket(socket_name, ORBIS_NET_AF_INET, ORBIS_NET_SOCK_STREAM, 0);

        if (server_socket < 0)
        {
            log_message("%s failed to create server socket, retrying in %d seconds... Attempt %d/%d",
                        isDaemon ? "Daemon" : "Relay", RETRY_DELAY_SECONDS, bind_retries + 1, MAX_RETRY_ATTEMPTS);

            if (++bind_retries >= MAX_RETRY_ATTEMPTS)
            {
                log_message("%s failed to create server socket after %d attempts, unloading...", isDaemon ? "Daemon" : "Relay", MAX_RETRY_ATTEMPTS);
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
            log_message("%s failed to bind server socket, retrying in %d seconds... Attempt %d/%d",
                        isDaemon ? "Daemon" : "Relay", RETRY_DELAY_SECONDS, bind_retries + 1, MAX_RETRY_ATTEMPTS);
            if (++bind_retries >= MAX_RETRY_ATTEMPTS)
            {
                log_message("%s failed to bind server socket after %d attempts, unloading...", isDaemon ? "Daemon" : "Relay", MAX_RETRY_ATTEMPTS);
                unloaded = true;
                break;
            }
            sceNetSocketClose(server_socket);
            sceKernelSleep(RETRY_DELAY_SECONDS);
            continue;
        }

        if (sceNetListen(server_socket, 1) < 0)
        {
            log_message("%s failed to listen on server socket, retrying in %d seconds... Attempt %d/%d",
                        isDaemon ? "Daemon" : "Relay", RETRY_DELAY_SECONDS, listen_retries + 1, MAX_RETRY_ATTEMPTS);
            if (++listen_retries >= MAX_RETRY_ATTEMPTS)
            {
                log_message("%s failed to listen on server socket after %d attempts, unloading...", isDaemon ? "Daemon" : "Relay", MAX_RETRY_ATTEMPTS);
                unloaded = true;
                break;
            }
            sceNetSocketClose(server_socket);
            sceKernelSleep(RETRY_DELAY_SECONDS);
            continue;
        }

        log_message("%s has started a server listening on port %d.", isDaemon ? "Daemon" : "Relay", port);

        while (!unloaded)
        {
            client_socket = sceNetAccept(server_socket, &client_addr, &client_addr_len);

            if (client_socket < 0)
            {
                log_message("Failed to accept client connection. Closing server socket and restarting...");

                if (++accept_retries >= MAX_RETRY_ATTEMPTS)
                {
                    log_message("Failed to accept client connection after %d attempts, unloading...", MAX_RETRY_ATTEMPTS);
                    unloaded = true;
                    break;
                }

                sceNetSocketClose(server_socket);
                sceKernelSleep(RETRY_DELAY_SECONDS);

                server_socket = sceNetSocket(socket_name, ORBIS_NET_AF_INET, ORBIS_NET_SOCK_STREAM, 0);
                if (server_socket < 0)
                {
                    log_message("Failed to recreate server socket, retrying...");
                    continue;
                }

                memset(&server_addr, 0, sizeof(server_addr));
                server_addr.len = sizeof(server_addr);
                server_addr.sa_family = ORBIS_NET_AF_INET;
                *(uint16_t *)server_addr.sa_data = sceNetHtons(port);
                memset(server_addr.sa_data + 2, 0, 4);

                if (sceNetBind(server_socket, &server_addr, sizeof(server_addr)) < 0)
                {
                    log_message("Failed to bind server socket after reset, retrying...");
                    continue;
                }

                if (sceNetListen(server_socket, 1) < 0)
                {
                    log_message("Failed to listen on server socket after reset, retrying...");
                    continue;
                }

                log_message("Server socket successfully reset and is listening again.");
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
    serverData data;
    struct proc_info info;
    sys_sdk_proc_info(&info);

    isDaemon = (strcmp(info.titleid, DAEMON) == 0);

    sys_utils::text_notify(222, (std::string("[OCAPI] ") +
                                 (isDaemon ? "Daemon server started!" : "Relay server started"))
                                    .c_str());

    pthread_t &thread = isDaemon ? data.threads.daemon.main : data.threads.relay.main;

    if (thread == -1 && pthread_create(&thread, nullptr, unified_thread, nullptr) != 0)
    {
        sys_utils::text_notify(222, (std::string("[OCAPI] Failed to create ") +
                                     (isDaemon ? "daemon" : "relay") + " thread!")
                                        .c_str());

        return 1;
    }

    pthread_detach(thread);

    if (isDaemon)
    {
        sceKernelLoadStartModule("libSceUserService.sprx", 0, NULL, 0, NULL, NULL);
        sceUserServiceInitialize2();

        while (!unloaded)
            sceKernelSleep(1);

        sys_utils::text_notify(222, "[OCAPI] Unloaded!");

        sceSystemServiceLoadExec("exit", 0);
    }

    return 0;
}
