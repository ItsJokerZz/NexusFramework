#include "../headers/includes.hpp"

namespace server
{
    namespace daemon
    {
        std::array<char, BUFFER_SIZE> buffer{};
        int daemon_sock = -1, client_sock = -1;

        void *process(void *arg)
        {
            client_sock = *static_cast<int *>(arg);
            std::fill(buffer.begin(), buffer.end(), 0);

            int bytes_received = sceNetRecv(client_sock, buffer.data(), buffer.size() - 1, 0);

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
    }
}
