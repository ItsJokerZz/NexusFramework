#include "../headers/includes.hpp"

namespace cmds
{
    namespace client
    {
        void Version()
        {
            char message[BUFFER_SIZE];
            snprintf(message, sizeof(message), "%f", VERSION);
            SendFormattedResponse(message, server::daemon::client_sock, &connected);
        }

        void Connect()
        {
            SendFormattedResponse("true", server::daemon::client_sock, &connected);
            connected = true;
        }

        void Unload()
        {
            SendFormattedResponse("done", server::daemon::client_sock, &connected);
            if (server::daemon::daemon_sock >= 0)
            {
                sceNetSocketClose(server::daemon::daemon_sock);
                server::daemon::daemon_sock = -1;
            }
            if (server::daemon::client_sock >= 0)
            {
                sceNetSocketClose(server::daemon::client_sock);
                server::daemon::client_sock = -1;
            }
            unload = true;
        }

        void Disconnect()
        {
            SendFormattedResponse("done", server::daemon::client_sock, &connected);
            if (server::daemon::client_sock >= 0)
            {
                sceNetSocketClose(server::daemon::client_sock);
                server::daemon::client_sock = -1;
            }
            connected = false;
        }

        void Attach()
        {
            attached = false;

            if (isRelayRunning())
            {
                char *response = PerformGETRequest("attach");
                if (response != NULL && strcmp(response, "done") == 0)
                    attached = true;
            }

            SendFormattedResponse("done", server::daemon::client_sock, &connected);
        }

        void GetFW()
        {
            SendFormattedResponse(System::GetFWVersion(), server::daemon::client_sock, &connected);
        }

        void GetTemp()
        {
            char temp[BUFFER_SIZE] = {0};
            char *start = strstr(server::daemon::buffer, "type=");
            if (start)
            {
                start += 5;
                char *end = strchr(start, '&');
                if (end)
                    *end = '\0';
                sscanf(start, "%s", temp);
                start = end ? strchr(end + 1, '=') : NULL;
                if (start)
                    start += 1;
            }

            int tempValue = NULL;
            if (strcmp(temp, "cpu") == 0)
            {
                tempValue = System::GetCPUTemperature();
            }
            else if (strcmp(temp, "soc") == 0)
            {
                tempValue = System::GetSOCTemperature();
            }
            else
                return; // Maybe remove this as well

            char message[BUFFER_SIZE];
            snprintf(message, sizeof(message), "%i", tempValue);
            SendFormattedResponse(message, server::daemon::client_sock, &connected);
        }

        void Notify()
        {
            char *msg = NULL;
            int type = 0;

            char *start = strstr(server::daemon::buffer, "type=");
            if (start)
            {
                start += 5;
                char *end = strchr(start, '&');
                if (end)
                    *end = '\0';
                sscanf(start, "%d", &type);
                start = end ? strchr(end + 1, '=') : NULL;
                if (start)
                    start += 1;
            }

            if (start)
            {
                char *end = strchr(start, ' ');
                if (end)
                    *end = '\0';
                msg = DecodeURL(start);
            }

            System::TextNotify(type, msg ? msg : NULL);

            if (msg)
                free(msg);

            SendFormattedResponse("done", server::daemon::client_sock, &connected);
        }

        void TempLimit()
        {
            uint8_t temp = 0;
            char *start = strstr(server::daemon::buffer, "limit=");
            if (start)
            {
                start += 6;
                char *end = strchr(start, '&');
                if (end)
                    *end = '\0';
                sscanf(start, "%hhu", &temp);
                start = end ? strchr(end + 1, '=') : NULL;
                if (start)
                    start += 1;
            }

            System::SetTemperatureLimit(temp);
            SendFormattedResponse("done", server::daemon::client_sock, &connected);
        }

        void SysType()
        {
            SendFormattedResponse(System::Type(), server::daemon::client_sock, &connected);
        }

        void Beep()
        {
            int type = -1;
            char *start = strstr(server::daemon::buffer, "type=");
            if (start)
            {
                start += 5;
                char *end = strchr(start, '&');
                if (end)
                    *end = '\0';
                sscanf(start, "%d", &type);
                start = end ? strchr(end + 1, '=') : NULL;
                if (start)
                    start += 1;
            }

            switch (type)
            {
            case 0: // stop
                System::Beep(0);
                break;
            case 1: // single
                System::Beep(1);
                break;
            case 2: // double
                System::Beep(1);
                sceKernelUsleep(125000);
                System::Beep(1);
                break;
            case 3: // triple
                System::Beep(1);
                sceKernelUsleep(125000);
                System::Beep(1);
                sceKernelUsleep(125000);
                System::Beep(1);
                break;
            case 4: // continuous
                System::Beep(6);
                break;
            }

            SendFormattedResponse("done", server::daemon::client_sock, &connected);
        }
    }

    namespace daemon
    {
        void Ping()
        {
            SendFormattedResponse("true", server::relay::daemon_sock, &attached);
        }

        void Attach()
        {
            System::TextNotify(222, "[OCAPI] Attached!");

            SendFormattedResponse("done", server::relay::daemon_sock, &attached);
        }
    }
}
