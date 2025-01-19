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

                // Check if the request starts with "GET / HTTP/1.1"
                if (request.substr(0, 14) == "GET / HTTP/1.1")
                {
                    send_error_response(NO_COMMAND);
                    return nullptr; // No need to process further if it's a favicon or empty request
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
                    {"GET /load_plugin", cmds::daemon::start_plugin}};
                    {"GET /rw_memory", cmds::daemon::rw_proc_mem}};

                typedef std::map<std::string, std::function<void()>>::const_iterator CommandIter;
                CommandIter it = std::find_if(commands.begin(), commands.end(),
                                              [&request](const std::pair<std::string, std::function<void()>> &pair)
                                              {
                                                  return request.find(pair.first) != std::string::npos;
                                              });

                // If no command was matched, send an error response
                if (it != commands.end())
                    it->second();
                else
                    send_error_response(INVALID_CMD);
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
    }
}
