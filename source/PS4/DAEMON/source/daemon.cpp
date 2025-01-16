#include "../headers/includes.hpp"

namespace server
{
    namespace daemon
    {
        char buffer[BUFFER_SIZE];
        int daemon_sock, client_sock;

        void *process(void *arg)
        {
            client_sock = *(int *)arg;

            memset(buffer, 0, sizeof(buffer));

            int bytes_received = sceNetRecv(client_sock, buffer, sizeof(buffer) - 1, 0);

            if (bytes_received > 0)
            {
                buffer[bytes_received] = '\0';

                if (strstr(buffer, "GET /connect") != NULL)
                    cmds::client::connect();

                else if (strstr(buffer, "GET /unload") != NULL)
                    cmds::client::unload();

                else if (strstr(buffer, "GET /disconnect") != NULL)
                    handle_command(cmds::client::disconnect, client_sock, connected);
                else if (strstr(buffer, "GET /attach") != NULL)
                    handle_command(cmds::client::attach, client_sock, connected);

                else if (strstr(buffer, "GET /version") != NULL)
                    handle_command(cmds::client::version, client_sock, connected);
                else if (strstr(buffer, "GET /fw") != NULL)
                    handle_command(cmds::client::get_fw, client_sock, connected);
                else if (strstr(buffer, "GET /sysType") != NULL)
                    handle_command(cmds::client::sys_type, client_sock, connected);
                else if (strstr(buffer, "GET /temp") != NULL)
                    handle_command(cmds::client::get_temp, client_sock, connected);
                else if (strstr(buffer, "GET /notify") != NULL)
                    handle_command(cmds::client::notify, client_sock, connected);
                else if (strstr(buffer, "GET /setTempLimit") != NULL)
                    handle_command(cmds::client::temp_limit, client_sock, connected);
                else if (strstr(buffer, "GET /beep") != NULL)
                    handle_command(cmds::client::beep, client_sock, connected);
                else
                    sceNetSend(client_sock, RESPONSE_404, strlen(RESPONSE_404), 0);
            }
            sceNetSocketClose(client_sock);

            return NULL;
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

            return NULL;
        }

        void start()
        {
            pthread_t daemon_thread;
            pthread_create(&daemon_thread, NULL, thread, NULL);
            pthread_detach(daemon_thread);
        }
    }
}
