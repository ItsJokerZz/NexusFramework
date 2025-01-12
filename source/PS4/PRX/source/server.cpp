#include "includes.hpp"

float version = 0.24;
int build = 20;

bool unload = false,
     connected = false,
     attached = false;

namespace OrbisControl
{
    char buffer[BUFFER_SIZE],
        response[BUFFER_SIZE];

    int server_sock,
        client_sock;

    void PrintMsgToUART(const char *msg)
    {
        char buffer[256];
        sprintf(buffer, "[OCAPI %.2fb%d] %s\n", version, build, msg);
        sceKernelDebugOutText(0, buffer);
    }

    char *DecodeURL(const char *url)
    {
        size_t len = strlen(url);
        char *decoded =
            (char *)malloc(len + 1); // Allocate enough memory for the decoded string
        if (decoded == NULL)
            return NULL;

        char *d = decoded;
        for (const char *s = url; *s; ++s)
        {
            if (*s == '%')
            {
                if (isxdigit(s[1]) && isxdigit(s[2]))
                {
                    int value;
                    sscanf(s + 1, "%2x", &value);
                    *d++ = (char)value;
                    s += 2; // Skip the next two characters
                }
                else
                    *d++ = '%'; // Invalid encoding, copy '%' and current character
            }
            else if (*s == '+')
                *d++ = ' '; // Replace '+' with space
            else
                *d++ = *s;
        }
        *d = '\0'; // Null-terminate the decoded string
        return decoded;
    }

    void SendResponse(const char *msg)
    {
        ssize_t bytes_sent = sceNetSend(client_sock, msg, strlen(msg), 0);
        if (bytes_sent < 0 || bytes_sent < strlen(msg))
            connected = false;
    }

    void HandleCommand(void (*func)())
    {
        if (connected)
            func();
        else
            SendResponse(RESPONSE_CONNECT);
    }

    void *HandleClients(void *arg)
    {
        client_sock = *(int *)arg;

        memset(buffer, 0, sizeof(buffer));

        int bytes_received = sceNetRecv(client_sock, buffer, sizeof(buffer) - 1, 0);

        if (bytes_received > 0)
        {
            buffer[bytes_received] = '\0';

            if (strstr(buffer, "GET /connect") != NULL)
                CMDS::Connect();

            else if (strstr(buffer, "GET /unload") != NULL)
                CMDS::Unload();

            else if (strstr(buffer, "GET /test") != NULL)
                CMDS::test();

            else if (strstr(buffer, "GET /disconnect") != NULL)
                HandleCommand(CMDS::Disconnect);
            else if (strstr(buffer, "GET /version") != NULL)
                HandleCommand(CMDS::Version);
            else if (strstr(buffer, "GET /fw") != NULL)
                HandleCommand(CMDS::GetFW);
            else if (strstr(buffer, "GET /sysType") != NULL)
                HandleCommand(CMDS::SysType);
            else if (strstr(buffer, "GET /temp") != NULL)
                HandleCommand(CMDS::GetTemp);
            else if (strstr(buffer, "GET /notify") != NULL)
                HandleCommand(CMDS::Notify);
            else if (strstr(buffer, "GET /setTempLimit") != NULL)
                HandleCommand(CMDS::TempLimit);
            else if (strstr(buffer, "GET /beep") != NULL)
                HandleCommand(CMDS::Beep);
            else
                sceNetSend(client_sock, RESPONSE_404, strlen(RESPONSE_404), 0);
        }
        sceNetSocketClose(client_sock);
        return NULL;
    }

    void StartServer()
    {
        OrbisNetSockaddr server_addr, client_addr;
        socklen_t client_addr_len = sizeof(client_addr);

        while (!unload)
        {
            server_sock = sceNetSocket("server_sock", ORBIS_NET_AF_INET, ORBIS_NET_SOCK_STREAM, 0);
            if (server_sock < 0)
            {
                PrintMsgToUART("Failed to create server socket, retrying...");
                sceKernelUsleep(1000000); // Sleep for 1 second before retrying
                continue;
            }

            memset(&server_addr, 0, sizeof(server_addr));
            server_addr.len = sizeof(server_addr);
            server_addr.sa_family = ORBIS_NET_AF_INET;
            *(uint16_t *)server_addr.sa_data = sceNetHtons(PORT);
            memset(server_addr.sa_data + 2, 0, 4);

            if (sceNetBind(server_sock, &server_addr, sizeof(server_addr)) < 0)
            {
                PrintMsgToUART("Failed to bind server socket, retrying...");
                sceNetSocketClose(server_sock);
                sceKernelUsleep(1000000); // Sleep for 1 second before retrying
                continue;
            }

            if (sceNetListen(server_sock, 1) < 0)
            {
                PrintMsgToUART("Failed to listen on server socket, retrying...");
                sceNetSocketClose(server_sock);
                sceKernelUsleep(1000000); // Sleep for 1 second before retrying
                continue;
            }

            PrintMsgToUART("Server listening on port 1337");

            while (!unload)
            {
                client_sock = sceNetAccept(server_sock, &client_addr, &client_addr_len);
                if (client_sock < 0)
                {
                    PrintMsgToUART("Failed to accept client connection");
                    continue;
                }

                pthread_t client_thread;
                pthread_create(&client_thread, NULL, HandleClients, &client_sock);
                pthread_detach(client_thread);
            }

            sceNetSocketClose(server_sock);
        }
    }
}

extern "C" void entry()
{
    struct proc_info info;
    sys_sdk_proc_info(&info);

    if (std::strcmp(info.titleid, "NPXS21002") == 0)
        OrbisControl::StartServer();

    if (std::strcmp(info.titleid, "NPXS21002") == 1)
    {
        OrbisControl::PrintMsgToUART("Attached!");
        System::TextNotify(222, "Attached!");
    }
}

attr_public const char *g_pluginName = "OrbisControl";
attr_public const char *g_pluginDesc = "Clone of CCAPI for PS3, but for PS4.";
attr_public const char *g_pluginAuth = "ItsJokerZz";
attr_public uint32_t g_pluginVersion = 0x00000100;

int32_t attr_public plugin_load(int32_t argc, const char *argv[])
{
    final_printf("[GoldHEN] %s Plugin Started.\n", g_pluginName);
    final_printf("[GoldHEN] <%s\\Ver.0x%08x> %s\n", g_pluginName, g_pluginVersion, __func__);
    final_printf("[GoldHEN] Plugin Author(s): %s\n", g_pluginAuth);
    char msg[128] = {0};
    snprintf(msg, sizeof(msg), "Hello from %s", g_pluginName);
    NotifyStatic(TEX_ICON_SYSTEM, msg);

    entry();
    return 0;
}


s32 attr_module_hidden module_start(s64 argc, const void *args)
{
    final_printf("[GoldHEN] %s Plugin Started.\n", g_pluginName);
    final_printf("[GoldHEN] <%s\\Ver.0x%08x> %s\n", g_pluginName, g_pluginVersion, __func__);
    final_printf("[GoldHEN] Plugin Author(s): %s\n", g_pluginAuth);
    char msg[128] = {0};
    snprintf(msg, sizeof(msg), "Hello from %s", g_pluginName);
    NotifyStatic(TEX_ICON_SYSTEM, msg);

    entry();
    return 0;
}

int32_t attr_public plugin_unload(int32_t argc, const char *argv[])
{
    final_printf("[GoldHEN] <%s\\Ver.0x%08x> %s\n", g_pluginName, g_pluginVersion, __func__);
    final_printf("[GoldHEN] %s Plugin Ended.\n", g_pluginName);
    return 0;
}

s32 attr_module_hidden module_stop(s64 argc, const void *args)
{
    final_printf("[GoldHEN] <%s\\Ver.0x%08x> %s\n", g_pluginName, g_pluginVersion, __func__);
    final_printf("[GoldHEN] %s Plugin Ended.\n", g_pluginName);
    return 0;
}
