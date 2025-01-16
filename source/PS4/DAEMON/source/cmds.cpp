#include "../headers/includes.hpp"

namespace cmds
{
    namespace client
    {
        void version()
        {
            std::string message = std::to_string(VERSION);
            send_formatted_response(message.c_str(), server::daemon::client_sock, &connected);
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
            const char* start = strstr(server::daemon::buffer.data(), "type=");
            if (start)
            {
                start += 5;
                char* end = strchr(start, '&');
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
            std::string msg;
            int type = 0;

            // Parse type
            const char* type_start = strstr(server::daemon::buffer.data(), "type=");
            if (type_start) {
                type_start += 5;
                const char* type_end = strchr(type_start, '&');
                if (type_end) {
                    std::string type_str(type_start, type_end);
                    type = std::stoi(type_str);
                    type_start = strchr(type_end + 1, '=');
                    if (type_start) type_start += 1;
                }
            }

            // Parse message
            if (type_start) {
                const char* msg_end = strchr(type_start, ' ');
                if (msg_end) {
                    std::string encoded_msg(type_start, msg_end);
                    char* decoded = decode_url(encoded_msg.c_str());
                    if (decoded) {
                        msg = std::string(decoded);
                        free(decoded);  // Free the decoded string if it was dynamically allocated
                    }
                }
            }

            sys_utils::text_notify(type, msg.empty() ? nullptr : msg.c_str());
            send_formatted_response("done", server::daemon::client_sock, &connected);
        }

        void temp_limit()
        {
            uint8_t temp = 0;
            const char* start = strstr(server::daemon::buffer.data(), "limit=");
            if (start)
            {
                start += 6;
                char* end = strchr(start, '&');
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
            const char* start = strstr(server::daemon::buffer.data(), "type=");
            if (start) {
                start += 5;
                const char* end = strchr(start, '&');
                if (end) {
                    std::string type_str(start, end);
                    type = std::stoi(type_str);
                }
            }

            using BeepType = sys_utils::BeepType;
            switch (static_cast<BeepType>(type)) {
                case BeepType::Stop:
                    sys_utils::beep(BeepType::Stop);
                    break;
                case BeepType::Single:
                    sys_utils::beep(BeepType::Single);
                    break;
                case BeepType::Double:
                    sys_utils::beep(BeepType::Single);
                    sceKernelUsleep(125000);
                    sys_utils::beep(BeepType::Single);
                    break;
                case BeepType::Triple:
                    sys_utils::beep(BeepType::Single);
                    sceKernelUsleep(125000);
                    sys_utils::beep(BeepType::Single);
                    sceKernelUsleep(125000);
                    sys_utils::beep(BeepType::Single);
                    break;
                case BeepType::Continuous:
                    sys_utils::beep(BeepType::Continuous);
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
