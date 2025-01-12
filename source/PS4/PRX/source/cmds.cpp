#include "cmds.hpp"

namespace OrbisControl
{
    namespace CMDS
    {
        void Version()
        {
            char message[BUFFER_SIZE];
            int message_length = snprintf(message, sizeof(message), "%f", version);
            if (message_length >= sizeof(message))
            {
                message_length = sizeof(message) - 1;
                message[message_length] = '\0';
            }
            snprintf(response, sizeof(response), RESPONSE_OK, message_length, message);
            SendResponse(response);
        }

        void Connect()
        {
            snprintf(response, sizeof(response),
                     RESPONSE_OK, 4, "true.");
            SendResponse(response);
            connected = true;
        }

        void Disconnect()
        {
            snprintf(response, sizeof(response),
                     RESPONSE_OK, 4, "done.");
            SendResponse(response);

            if (client_sock >= 0)
            {
                sceNetSocketClose(client_sock);
                client_sock = -1;
            }

            connected = false;
        }

        void Unload()
        {
            snprintf(response, sizeof(response),
                     RESPONSE_OK, 4, "done.");
            SendResponse(response);

            if (server_sock >= 0)
            {
                sceNetSocketClose(server_sock);
                server_sock = -1;
            }

            if (client_sock >= 0)
            {
                sceNetSocketClose(client_sock);
                client_sock = -1;
            }

            unload = true;
        }

        void GetFW()
        {
            char message[BUFFER_SIZE];
            int message_length = snprintf(message, sizeof(message), "%s", System::GetFWVersion());
            if (message_length >= sizeof(message))
            {
                message_length = sizeof(message) - 1;
                message[message_length] = '\0';
            }
            snprintf(response, sizeof(response), RESPONSE_OK, message_length, message);
            SendResponse(response);
        }

        void GetTemp()
        { // make all other char*/string args this way
            char temp[BUFFER_SIZE] = {0};

            char *start = strstr(buffer, "type=");
            if (start)
            {
                start += 5; // Move past "type="
                char *end = strchr(start, '&');
                if (end)
                    *end = '\0';
                sscanf(start, "%s", temp); // Remove '&' to correctly scan the string
                start = end ? strchr(end + 1, '=') : NULL;
                if (start)
                    start += 1; // Move past '='
            }

            char message[BUFFER_SIZE];
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
                return; // maybe remove

            int message_length = snprintf(message, sizeof(message), "%i", tempValue);
            if (message_length >= sizeof(message))
            {
                message_length = sizeof(message) - 1;
                message[message_length] = '\0';
            }

            snprintf(response, sizeof(response), RESPONSE_OK, message_length, message);
            SendResponse(response);
        }

        void Notify()
        {
            char *msg = NULL;
            int type = 0;

            char *start = strstr(buffer, "type=");
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

            char message[BUFFER_SIZE];
            int message_length = snprintf(message, sizeof(message), "done.");
            if (message_length >= sizeof(message))
            {
                message_length = sizeof(message) - 1;
                message[message_length] = '\0';
            }

            snprintf(response, sizeof(response), RESPONSE_OK, message_length, message);
            SendResponse(response);
            System::TextNotify(type, msg ? msg : "msg");

            if (msg)
            {
                free(msg);
            }
        }

        void TempLimit()
        {
            uint8_t temp = 0;
            char *start = strstr(buffer, "limit=");
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

            char message[BUFFER_SIZE];
            int message_length = snprintf(message, sizeof(message), "done.");
            if (message_length >= sizeof(message))
            {
                message_length = sizeof(message) - 1;
                message[message_length] = '\0';
            }

            snprintf(response, sizeof(response), RESPONSE_OK, message_length, message);
            SendResponse(response);

            System::SetTemperatureLimit(temp);
        }

        void SysType()
        {
            char message[BUFFER_SIZE];
            int message_length = snprintf(message, sizeof(message), "%s", System::Type());
            if (message_length >= sizeof(message))
            {
                message_length = sizeof(message) - 1;
                message[message_length] = '\0';
            }
            snprintf(response, sizeof(response), RESPONSE_OK, message_length, message);
            SendResponse(response);
        }

        void Beep()
        {
            int type = -1;
            char *start = strstr(buffer, "type=");
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

            char message[BUFFER_SIZE] = {0};
            int message_length = snprintf(message, sizeof(message), "done.");
            if (message_length >= sizeof(message))
            {
                message_length = sizeof(message) - 1;
                message[message_length] = '\0';
            }
            snprintf(response, sizeof(response), RESPONSE_OK, message_length, message);
            SendResponse(response);

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
        }

        void test()
        {
            char exec[] = "eboot.bin";
            char sprx[] = "/data/OrbisControl_game.prx";

            int handle = sys_sdk_proc_prx_load(exec, sprx);
            OrbisControl::PrintMsgToUART(("Handle: " + std::to_string(handle)).c_str());

            if (handle >= 0)
            {
                void (*entryFunc)() = NULL;

                int32_t res = sceKernelDlsym(handle, "entry", (void **)&entryFunc);
                OrbisControl::PrintMsgToUART(("Dlsym result: " + std::to_string(res)).c_str());

                if (res == 0 && entryFunc != NULL)
                    entryFunc();
                else
                    OrbisControl::PrintMsgToUART("Failed to find or resolve 'entry' function in PRX.");
            }
            else
                OrbisControl::PrintMsgToUART("Failed to load PRX.");

            attached = true;
        }
    }
}