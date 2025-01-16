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
                    cmds::client::Connect();

                else if (strstr(buffer, "GET /unload") != NULL)
                    cmds::client::Unload();

                else if (strstr(buffer, "GET /disconnect") != NULL)
                    HandleCommand(cmds::client::Disconnect, client_sock, connected);
                else if (strstr(buffer, "GET /attach") != NULL)
                    HandleCommand(cmds::client::Attach, client_sock, connected);

                else if (strstr(buffer, "GET /version") != NULL)
                    HandleCommand(cmds::client::Version, client_sock, connected);
                else if (strstr(buffer, "GET /fw") != NULL)
                    HandleCommand(cmds::client::GetFW, client_sock, connected);
                else if (strstr(buffer, "GET /sysType") != NULL)
                    HandleCommand(cmds::client::SysType, client_sock, connected);
                else if (strstr(buffer, "GET /temp") != NULL)
                    HandleCommand(cmds::client::GetTemp, client_sock, connected);
                else if (strstr(buffer, "GET /notify") != NULL)
                    HandleCommand(cmds::client::Notify, client_sock, connected);
                else if (strstr(buffer, "GET /setTempLimit") != NULL)
                    HandleCommand(cmds::client::TempLimit, client_sock, connected);
                else if (strstr(buffer, "GET /beep") != NULL)
                    HandleCommand(cmds::client::Beep, client_sock, connected);
                else
                    sceNetSend(client_sock, RESPONSE_404, strlen(RESPONSE_404), 0);
            }
            sceNetSocketClose(client_sock);

            return NULL;
        }

        void start()
        {
            OrbisNetSockaddr server_addr, client_addr;
            socklen_t client_addr_len = sizeof(client_addr);

            while (!unload)
            {
                daemon_sock = sceNetSocket("daemon_sock",
                                           ORBIS_NET_AF_INET, ORBIS_NET_SOCK_STREAM, 0);

                if (daemon_sock < 0)
                {
                    PrintMsgToUART("Daemon failed to create server socket, retrying...");
                    sceKernelSleep(1);
                    continue;
                }

                memset(&server_addr, 0, sizeof(server_addr));
                server_addr.len = sizeof(server_addr);
                server_addr.sa_family = ORBIS_NET_AF_INET;
                *(uint16_t *)server_addr.sa_data = sceNetHtons(DAEMON_PORT);
                memset(server_addr.sa_data + 2, 0, 4);

                if (sceNetBind(daemon_sock, &server_addr, sizeof(server_addr)) < 0)
                {
                    PrintMsgToUART("Daemon failed to bind server socket, retrying...");
                    sceNetSocketClose(daemon_sock);
                    sceKernelSleep(1);
                    continue;
                }

                if (sceNetListen(daemon_sock, 1) < 0)
                {
                    PrintMsgToUART("Daemon failed to listen on server socket, retrying...");

                    sceNetSocketClose(daemon_sock);
                    sceKernelSleep(1);
                    continue;
                }

                PrintMsgToUART("Daemon has started a server listening on port 1337.");

                while (!unload)
                {
                    client_sock = sceNetAccept(daemon_sock, &client_addr, &client_addr_len);
                    if (client_sock < 0)
                    {
                        PrintMsgToUART("Failed to accept client connection");
                        continue;
                    }

                    pthread_t client_thread;
                    pthread_create(&client_thread, NULL, process, &client_sock);
                    pthread_detach(client_thread);
                }

                sceNetSocketClose(daemon_sock);
            }
        }
    }
}
