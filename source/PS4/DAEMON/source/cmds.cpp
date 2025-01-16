#include "../headers/includes.hpp"

namespace cmds
{
    namespace client
    {
        void version()
        {
            char message[BUFFER_SIZE];
            snprintf(message, sizeof(message), "%f", VERSION);
            send_formatted_response(message, server::daemon::client_sock, &connected);
        }

        void connect()
        {
            send_formatted_response("true", server::daemon::client_sock, &connected);
            connected = true;
        }

        void unload()
        {
            send_formatted_response("done", server::daemon::client_sock, &connected);
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
            ::unload = true;
        }

        void disconnect()
        {
            send_formatted_response("done", server::daemon::client_sock, &connected);
            if (server::daemon::client_sock >= 0)
            {
                sceNetSocketClose(server::daemon::client_sock);
                server::daemon::client_sock = -1;
            }
            connected = false;
        }

        void attach()
        {
            attached = false;

            if (is_relay_running())
            {
                char* response = perform_get_request("attach");
                if (response != NULL && strcmp(response, "done") == 0)
                    attached = true;
            }

            send_formatted_response("done", server::daemon::client_sock, &connected);
        }

        void get_fw()
        {
            send_formatted_response(sys_utils::get_fw_version(), server::daemon::client_sock, &connected);
        }

        void get_temp()
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
                tempValue = sys_utils::get_cpu_temperature();
            }
            else if (strcmp(temp, "soc") == 0)
            {
                tempValue = sys_utils::get_soc_temperature();
            }
            else
                return; // Maybe remove this as well

            char message[BUFFER_SIZE];
            snprintf(message, sizeof(message), "%i", tempValue);
            send_formatted_response(message, server::daemon::client_sock, &connected);
        }

        void notify()
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
                char* end = strchr(start, ' ');
                if (end)
                    *end = '\0';
                msg = decode_url(start);
            }

            sys_utils::text_notify(type, msg ? msg : NULL);

            if (msg)
                free(msg);

            send_formatted_response("done", server::daemon::client_sock, &connected);
        }

        void temp_limit()
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

            sys_utils::set_temperature_limit(temp);
            send_formatted_response("done", server::daemon::client_sock, &connected);
        }

        void sys_type()
        {
            send_formatted_response(sys_utils::get_console_type(), server::daemon::client_sock, &connected);
        }

        void beep()
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
                sys_utils::beep(0);
                break;
            case 1: // single
                sys_utils::beep(1);
                break;
            case 2: // double
                sys_utils::beep(1);
                sceKernelUsleep(125000);
                sys_utils::beep(1);
                break;
            case 3: // triple
                sys_utils::beep(1);
                sceKernelUsleep(125000);
                sys_utils::beep(1);
                sceKernelUsleep(125000);
                sys_utils::beep(1);
                break;
            case 4: // continuous
                sys_utils::beep(6);
                break;
            }

            send_formatted_response("done", server::daemon::client_sock, &connected);
        }
    }

    namespace daemon
    {
        void ping()
        {
            send_formatted_response("true", server::relay::daemon_sock, &attached);
        }

        void attach()
        {
            sys_utils::text_notify(222, "[OCAPI] Attached!");
            send_formatted_response("done", server::relay::daemon_sock, &attached);
        }
    }
}
