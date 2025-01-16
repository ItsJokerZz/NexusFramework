#include "../headers/includes.hpp"

namespace server
{
    namespace relay
    {
        char buffer[BUFFER_SIZE];
        int relay_sock, daemon_sock;

        void *process(void *arg)
        {
            daemon_sock = *(int *)arg;

            memset(buffer, 0, sizeof(buffer));

            int bytes_received = sceNetRecv(daemon_sock, buffer, sizeof(buffer) - 1, 0);

            if (bytes_received > 0)
            {
                buffer[bytes_received] = '\0';

                if (strstr(buffer, "GET /test") != NULL)
                {
                    send_formatted_response("done", server::relay::daemon_sock, &attached);

                    if (!attached && strcmp(perform_get_request("attach"), "done") == 0)
                        attached = true;
                }
                else if (strstr(buffer, "GET /ping") != NULL)
                    cmds::daemon::ping();
                else if (strstr(buffer, "GET /attach") != NULL)
                    cmds::daemon::attach();
                else
                    sceNetSend(daemon_sock, RESPONSE_404, strlen(RESPONSE_404), 0);
            }
            sceNetSocketClose(daemon_sock);

            return NULL;
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

                memset(&server_addr, 0, sizeof(server_addr));
                server_addr.len = sizeof(server_addr);
                server_addr.sa_family = ORBIS_NET_AF_INET;
                *(uint16_t *)server_addr.sa_data = sceNetHtons(RELAYS_PORT);
                memset(server_addr.sa_data + 2, 0, 4);

                // *(uint32_t *)(relay_addr.sa_data + 2) = sceNetHtonl(0x7F000001); // Bind to localhost (127.0.0.1)

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
                    pthread_create(&daemon_thread, NULL, process, &daemon_sock);
                    pthread_detach(daemon_thread);
                }

                sceNetSocketClose(relay_sock);
            }

            return NULL;
        }

        void start()
        {
            pthread_t relay_thread;
            pthread_create(&relay_thread, NULL, thread, NULL);
            pthread_detach(relay_thread);
        }
    }
}
