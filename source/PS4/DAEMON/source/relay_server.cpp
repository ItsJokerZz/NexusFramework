#include "../headers/includes.hpp"

namespace server
{
    namespace relay
    {
        threadData td;

        void *process(void *arg)
        {
            std::fill(td.buffer.begin(), td.buffer.end(), 0);

            int bytes_received = sceNetRecv(td.client_socket, td.buffer.data(), td.buffer.size() - 1, 0);

            if (bytes_received > 0)
            {
                td.buffer[bytes_received] = '\0';
                const std::string request(td.buffer.data());

                static const std::map<std::string, std::function<void()>> commands = {
                    {"GET /test", []()
                     {
                         send_response("done");
                         if (!attached && strcmp(perform_get_request("attach"), "done") == 0)
                             attached = true;
                     }},

                    // maybe try to find a way to make this work with handle_command,
                    // because the attached bool isnt updating between the relay and daemon
                    {"GET /ping", cmds::daemon::ping},
                    {"GET /attach", cmds::daemon::attach},
                    {"GET /exec_prx", []()
                     { handle_command(cmds::daemon::exec_prx); }},
                    {"GET /load_plugin", []()
                     { handle_command(cmds::daemon::load_plugin); }}};

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
            sceNetSocketClose(td.client_socket);

            return nullptr;
        }

        void *thread(void *arg)
        {
            td.port = 8008;

            while (!unload)
            {
                td.server_socket = sceNetSocket("relay_socket", ORBIS_NET_AF_INET, ORBIS_NET_SOCK_STREAM, 0);

                if (td.server_socket < 0)
                {
                    log_message("Relay failed to create server socket, retrying in %d seconds...", RETRY_DELAY_SECONDS);
                    sceKernelSleep(RETRY_DELAY_SECONDS);

                    continue;
                }

                std::memset(&td.server_addr, 0, sizeof(td.server_addr));
                td.server_addr.len = sizeof(td.server_addr);
                td.server_addr.sa_family = ORBIS_NET_AF_INET;
                *reinterpret_cast<uint16_t *>(td.server_addr.sa_data) = sceNetHtons(td.port);
                std::memset(td.server_addr.sa_data + 2, 0, 4);

                if (sceNetBind(td.server_socket, &td.server_addr, sizeof(td.server_addr)) < 0)
                {
                    log_message("Relay failed to bind server socket, retrying in %d seconds...", RETRY_DELAY_SECONDS);
                    sceNetSocketClose(td.server_socket);
                    sceKernelSleep(RETRY_DELAY_SECONDS);

                    continue;
                }

                if (sceNetListen(td.server_socket, 1) < 0)
                {
                    log_message("Relay failed to listen on server socket, retrying in %d seconds...", RETRY_DELAY_SECONDS);
                    sceNetSocketClose(td.server_socket);
                    sceKernelSleep(RETRY_DELAY_SECONDS);

                    continue;
                }

                log_message("Relay has started a server listening on port 8008.");

                while (!unload)
                {
                    td.client_socket = sceNetAccept(td.server_socket, &td.client_addr, &td.client_addr_len);
                    if (td.client_socket < 0)
                    {
                        log_message("Failed to accept daemon connection");

                        continue;
                    }

                    pthread_create(&td.client_thread, nullptr, process, &td.client_socket);
                    pthread_detach(td.client_thread);
                }

                sceNetSocketClose(td.server_socket);
            }

            return nullptr;
        }

        void start()
        {
            pthread_create(&td.server_thread, nullptr, thread, nullptr);
            pthread_detach(td.server_thread);
        }
    }
}
