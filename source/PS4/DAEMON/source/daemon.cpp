#include "../headers/includes.hpp"

namespace server
{
    namespace daemon
    {
        std::array<char, BUFFER_SIZE> buffer{};
        int daemon_sock, client_sock;

        void *process(void *arg)
        {
            client_sock = *static_cast<int *>(arg);
            std::fill(buffer.begin(), buffer.end(), 0);

            int bytes_received = sceNetRecv(client_sock, buffer.data(), buffer.size() - 1, 0);

            if (bytes_received > 0)
            {
                buffer[bytes_received] = '\0';
                const std::string request(buffer.data());

                static const std::map<std::string, std::function<void()>> commands = {
                    {"GET /connect", cmds::client::connection::connect},
                    {"GET /unload", cmds::client::connection::unload},
                    {"GET /disconnect", []()
                     { handle_command(cmds::client::connection::disconnect, client_sock, connected); }},
                    {"GET /attach", []()
                     { handle_command(cmds::client::connection::attach, client_sock, connected); }},
                    {"GET /version", []()
                     { handle_command(cmds::client::connection::version, client_sock, connected); }},

                    {"GET /get_firmware", []()
                     { handle_command(cmds::client::sys_info::get_fw, client_sock, connected); }},
                    {"GET /get_sys_type", []()
                     { handle_command(cmds::client::sys_info::sys_type, client_sock, connected); }},
                    {"GET /get_temperature", []()
                     { handle_command(cmds::client::sys_info::get_temp, client_sock, connected); }},

                    {"GET /send_notify", []()
                     { handle_command(cmds::client::sys_control::notify, client_sock, connected); }},
                    {"GET /set_temp_limit", []()
                     { handle_command(cmds::client::sys_control::temp_limit, client_sock, connected); }},
                    {"GET /ring_buzzer", []()
                     { handle_command(cmds::client::sys_control::ring_buzzer, client_sock, connected); }},

                    {"GET /get_proc_list", []()
                     { handle_command(cmds::client::process::proc_list, client_sock, connected); }},
                    {"GET /exec_prx", []()
                     { handle_command(cmds::client::process::exec_prx, client_sock, connected); }},
                    {"GET /find_pid_by_name", []()
                     { handle_command(cmds::client::process::find_pid_by_name, client_sock, connected); }},
                    {"GET /load_plugin", []()
                     { handle_command(cmds::client::process::load_plugin, client_sock, connected); }},
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
                    send_error_response(INVALID_CMD, server::daemon::client_sock, &connected);
            }
            sceNetSocketClose(client_sock);

            return nullptr;
        }

        void *thread(void *arg)
        {
            OrbisNetSockaddr server_addr, client_addr;
            socklen_t client_addr_len = sizeof(client_addr);

            while (!unload)
            {
                daemon_sock = sceNetSocket("daemon_sock",
                                           ORBIS_NET_AF_INET, ORBIS_NET_SOCK_STREAM, 0);

                if (daemon_sock < 0)
                {
                    log_message("Daemon failed to create server socket, retrying in %d seconds...", RETRY_DELAY_SECONDS);
                    sceKernelSleep(RETRY_DELAY_SECONDS);
                    continue;
                }

                memset(&server_addr, 0, sizeof(server_addr));
                server_addr.len = sizeof(server_addr);
                server_addr.sa_family = ORBIS_NET_AF_INET;
                *(uint16_t *)server_addr.sa_data = sceNetHtons(DAEMON_PORT);
                memset(server_addr.sa_data + 2, 0, 4);

                if (sceNetBind(daemon_sock, &server_addr, sizeof(server_addr)) < 0)
                {
                    log_message("Daemon failed to bind server socket, retrying in %d seconds...", RETRY_DELAY_SECONDS);
                    sceNetSocketClose(daemon_sock);
                    sceKernelSleep(RETRY_DELAY_SECONDS);

                    continue;
                }

                if (sceNetListen(daemon_sock, 1) < 0)
                {
                    log_message("Daemon failed to listen on server socket, retrying in %d seconds...", RETRY_DELAY_SECONDS);
                    sceNetSocketClose(daemon_sock);
                    sceKernelSleep(RETRY_DELAY_SECONDS);

                    continue;
                }

                log_message("Daemon has started a server listening on port 1337.");

                while (!unload)
                {
                    client_sock = sceNetAccept(daemon_sock, &client_addr, &client_addr_len);
                    if (client_sock < 0)
                    {
                        log_message("Failed to accept client connection");

                        continue;
                    }

                    pthread_t client_thread;
                    pthread_create(&client_thread, NULL, process, &client_sock);
                    pthread_detach(client_thread);
                }

                sceNetSocketClose(daemon_sock);
            }

            return nullptr;
        }

        void start()
        {
            pthread_t daemon_thread;
            pthread_create(&daemon_thread, NULL, thread, NULL);
            pthread_detach(daemon_thread);
        }
    }
}
