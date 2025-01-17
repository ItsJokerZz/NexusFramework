#include "../headers/includes.hpp"

namespace server
{
    namespace relay
    {
        std::array<char, BUFFER_SIZE> buffer{};
        int relay_sock, daemon_sock;

        void *process(void *arg)
        {
            daemon_sock = *static_cast<int *>(arg);
            std::fill(buffer.begin(), buffer.end(), 0);

            int bytes_received = sceNetRecv(daemon_sock, buffer.data(), buffer.size() - 1, 0);

            if (bytes_received > 0)
            {
                buffer[bytes_received] = '\0';
                const std::string request(buffer.data());

                static const std::map<std::string, std::function<void()>> commands = {
                    {"GET /test", []()
                     {
                         send_formatted_response("done", server::relay::daemon_sock, &attached);
                         if (!attached && strcmp(perform_get_request("attach"), "done") == 0)
                             attached = true;
                     }},

                    {"GET /a", []()
                     {
                         send_formatted_response("done", server::relay::daemon_sock, &attached);

                         uint64_t pnum;
                         if (sys_proc_list(NULL, &pnum))
                         {
                             log_message("Failed to get process count");
                             return;
                         }

                         struct proc_list_entry* proc_list = (struct proc_list_entry*)malloc(pnum * sizeof(struct proc_list_entry));
                         if (!proc_list)
                         {
                             log_message("Failed to allocate memory for process list");
                             return;
                         }

                         if (sys_proc_list(proc_list, &pnum))
                         {
                             log_message("Failed to retrieve process list");
                             free(proc_list);
                             return;
                         }

                         for (uint64_t i = 0; i < pnum; i++)
                         {
                             log_message("Process %d: %s", proc_list[i].pid, proc_list[i].p_comm);
                         }

                         free(proc_list);
                     }},

                    // maybe try to find a way to make this work with handle_command,
                    // because the attached bool isnt updating between the relay and daemon
                    {"GET /ping", cmds::daemon::ping},
                    {"GET /attach", cmds::daemon::attach},
                    {"GET /exec_prx", cmds::daemon::exec_prx}};

                // Replace auto with explicit type for C++11 compatibility
                typedef std::map<std::string, std::function<void()>>::const_iterator CommandIter;
                CommandIter it = std::find_if(commands.begin(), commands.end(),
                                              [&request](const std::pair<std::string, std::function<void()>> &pair)
                                              {
                                                  return request.find(pair.first) != std::string::npos;
                                              });

                if (it != commands.end())
                {
                    it->second();
                }
                else
                {
                    sceNetSend(daemon_sock, RESPONSE_404, strlen(RESPONSE_404), 0);
                }
            }

            sceNetSocketClose(daemon_sock);

            return nullptr;
        }

        void *thread(void *arg)
        {
            OrbisNetSockaddr server_addr, daemon_addr;
            socklen_t daemon_addr_len = sizeof(daemon_addr);

            while (!unload)
            {
                relay_sock = sceNetSocket("relay_sock",
                                          ORBIS_NET_AF_INET, ORBIS_NET_SOCK_STREAM, 0);

                if (relay_sock < 0)
                {
                    log_message("Relay failed to create server socket, retrying in %d seconds...", RETRY_DELAY_SECONDS);
                    sceKernelSleep(RETRY_DELAY_SECONDS);
                    continue;
                }

                std::memset(&server_addr, 0, sizeof(server_addr));
                server_addr.len = sizeof(server_addr);
                server_addr.sa_family = ORBIS_NET_AF_INET;
                *reinterpret_cast<uint16_t *>(server_addr.sa_data) = sceNetHtons(RELAYS_PORT);
                std::memset(server_addr.sa_data + 2, 0, 4);

                if (sceNetBind(relay_sock, &server_addr, sizeof(server_addr)) < 0)
                {
                    log_message("Relay failed to bind server socket, retrying in %d seconds...", RETRY_DELAY_SECONDS);
                    sceNetSocketClose(relay_sock);
                    sceKernelSleep(RETRY_DELAY_SECONDS);
                    continue;
                }

                if (sceNetListen(relay_sock, 1) < 0)
                {
                    log_message("Relay failed to listen on server socket, retrying in %d seconds...", RETRY_DELAY_SECONDS);
                    sceNetSocketClose(relay_sock);
                    sceKernelSleep(RETRY_DELAY_SECONDS);
                    continue;
                }

                log_message("Relay has started a server listening on port 8008.");

                while (!unload)
                {
                    daemon_sock = sceNetAccept(relay_sock, &daemon_addr, &daemon_addr_len);
                    if (daemon_sock < 0)
                    {
                        log_message("Failed to accept daemon connection");
                        continue;
                    }

                    pthread_t daemon_thread;
                    pthread_create(&daemon_thread, nullptr, process, &daemon_sock);
                    pthread_detach(daemon_thread);
                }

                sceNetSocketClose(relay_sock);
            }

            return nullptr;
        }

        void start()
        {
            pthread_t relay_thread;
            pthread_create(&relay_thread, nullptr, thread, nullptr);
            pthread_detach(relay_thread);
        }
    }
}
