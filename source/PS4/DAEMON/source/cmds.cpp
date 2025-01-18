#include "../headers/includes.hpp"

namespace cmds
{
    namespace client
    {
        namespace connection
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
                    char *response = perform_get_request("attach");
                    if (response != NULL && strcmp(response, "done") == 0)
                        attached = true;
                }

                send_formatted_response("done", server::daemon::client_sock, &connected);
            }

        }

        namespace sys_info
        {
            void sys_type()
            {
                send_formatted_response(sys_utils::get_console_type(), server::daemon::client_sock, &connected);
            }

            void get_fw()
            {
                send_formatted_response(sys_utils::get_fw_version(), server::daemon::client_sock, &connected);
            }

            void get_temp()
            {
                char temp[BUFFER_SIZE] = {0};
                const char *start = strstr(server::daemon::buffer.data(), "type=");
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

        }

        namespace sys_control
        {
            void notify()
            {
                std::string msg;
                int type = 0;

                // Parse type
                const char *type_start = strstr(server::daemon::buffer.data(), "type=");
                if (type_start)
                {
                    type_start += 5;
                    const char *type_end = strchr(type_start, '&');
                    if (type_end)
                    {
                        std::string type_str(type_start, type_end);
                        type = std::stoi(type_str);
                        type_start = strchr(type_end + 1, '=');
                        if (type_start)
                            type_start += 1;
                    }
                }

                // Parse message
                if (type_start)
                {
                    const char *msg_end = strchr(type_start, ' ');
                    if (msg_end)
                    {
                        std::string encoded_msg(type_start, msg_end);
                        char *decoded = decode_url(encoded_msg.c_str());
                        if (decoded)
                        {
                            msg = std::string(decoded);
                            free(decoded); // Free the decoded string if it was dynamically allocated
                        }
                    }
                }

                sys_utils::text_notify(type, msg.empty() ? nullptr : msg.c_str());
                send_formatted_response("done", server::daemon::client_sock, &connected);
            }

            void temp_limit()
            {
                uint8_t temp = 0;
                const char *start = strstr(server::daemon::buffer.data(), "limit=");
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

            void beep()
            {
                int type = -1;
                const char *start = strstr(server::daemon::buffer.data(), "type=");
                if (start)
                {
                    start += 5;
                    const char *end = strchr(start, '&');
                    if (end)
                    {
                        std::string type_str(start, end);
                        type = std::stoi(type_str);
                    }
                }

                using BeepType = sys_utils::BeepType;
                switch (static_cast<BeepType>(type))
                {
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

        void exec_prx()
        {
            const char *prx_start = strstr(server::daemon::buffer.data(), "path=");
            if (!prx_start)
            {
                send_formatted_response("failed", server::daemon::client_sock, &connected);
                return;
            }

            prx_start += 5;
            const char *end = strchr(prx_start, ' ');
            if (!end)
            {
                end = prx_start + strlen(prx_start);
            }

            std::string prx_path(prx_start, end);

            if (is_relay_running())
            {
                // If relay is running, just send the path
                char request[BUFFER_SIZE];
                snprintf(request, sizeof(request), "exec_prx?path=%s", prx_path.c_str());

                char *response = perform_get_request(request);
                if (response)
                    send_formatted_response(response, server::daemon::client_sock, &connected);
            }
            else
            {
                char notify_msg[BUFFER_SIZE];
                snprintf(notify_msg, sizeof(notify_msg), "[OCAPI] PRX Loaded: %s", prx_path.c_str());
                sys_utils::text_notify(222, notify_msg);
                send_formatted_response(prx_path.c_str(), server::daemon::client_sock, &connected);
            }
        }

        void proc_list()
        {
            uint64_t num = 0;
            struct proc_list_entry *procs = nullptr;

            std::string log_string = "{\n"
                                     "    \"DATA\": {\n";

            if (sys_proc_list(nullptr, &num) != 0 || num == 0)
                return;

            procs = (struct proc_list_entry *)malloc(sizeof(struct proc_list_entry) * num);
            if (!procs)
            {
                send_formatted_response(log_string.c_str(), server::daemon::client_sock, &connected);
                return;
            }

            if (sys_proc_list(procs, &num) != 0)
            {
                free(procs);
                return;
            }

            // Use a vector to sort the processes by their PID
            std::vector<std::pair<int, std::string>> sorted_procs;
            for (uint64_t i = 0; i < num; i++)
            {
                if (procs[i].p_comm[sizeof(procs[i].p_comm) - 1] != '\0')
                    procs[i].p_comm[sizeof(procs[i].p_comm) - 1] = '\0';

                sorted_procs.push_back({procs[i].pid, procs[i].p_comm});
            }

            // Sort by the PID (first element of the pair)
            std::sort(sorted_procs.begin(), sorted_procs.end());

            // Construct the JSON output with sorted processes
            for (size_t i = 0; i < sorted_procs.size(); i++)
            {
                log_string += "        \"" + std::to_string(sorted_procs[i].first) + "\": \"" + sorted_procs[i].second + "\"";

                if (i < sorted_procs.size() - 1)
                {
                    log_string += ",\n"; // Add a comma for all but the last item
                }
            }

            log_string += "\n"
                          "    }\n"
                          "}";

            send_formatted_response(log_string.c_str(), server::daemon::client_sock, &connected);

            log_message("%s", log_string.c_str());

            free(procs);
        }

        void find_pid_by_name()
        {
            int procID;

            const char *name = strstr(server::daemon::buffer.data(), "name=");
            if (!name)
            {
                send_formatted_response("failed", server::daemon::client_sock, &connected);
                return;
            }

            name += 5;
            const char *end = strchr(name, ' ');
            if (!end)
                end = name + strlen(name);

            std::string proc_name(name, end);

            find_process_pid(proc_name.c_str(), &procID);

            send_formatted_response(std::to_string(procID).c_str(), server::daemon::client_sock, &connected);
        }

        void load_plugin()
        {
            if (is_relay_running())
            {
                if (perform_get_request("load_plugin") != NULL)
                {
                    // Directly pass the address of 'attached' to handle_command without lambda
                    handle_command([]
                                   {
                                       // Lambda body can remain empty or you can add logic if needed
                                   },
                                   server::daemon::client_sock, attached); // Pass 'attached' as a pointer to handle_command
                }
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

        void exec_prx()
        {
            const char *path = nullptr;
            const char *start = strstr(server::relay::buffer.data(), "path=");

            if (start)
            {
                start += 5;
                const char *end = strchr(start, ' ');
                if (end)
                {
                    std::string encoded_path(start, end);
                    path = decode_url(encoded_path.c_str());
                }
            }

            if (!path)
            {
                log_message("No path provided for SPRX");
                return;
            }

            struct proc_info info;
            sys_sdk_proc_info(&info);
            int pid = info.pid;

            int32_t result = sceKernelLoadStartModule(path, 0, 0, 0, NULL, NULL);
            if (result == 0x80020002)
            {
                log_message("SPRX %s not found", path);
                free((void *)path);
                return;
            }
            else if (result < 0)
            {
                log_message("Error loading SPRX %s! Error code 0x%08x (%i)", path, result, result);
                free((void *)path);
                return;
            }

            char response[BUFFER_SIZE];
            snprintf(response, sizeof(response), "%i,%d,%s", pid, result, path);
            send_formatted_response(response, server::relay::daemon_sock, &attached);

            int32_t ret;
            int32_t (*module_start_ret)(size_t, const void *);
            int32_t (*module_stop_ret)(size_t, const void *);

            ret = sceKernelDlsym(result, "module_start", (void **)&module_start_ret);
            log_message("module_start Dlsym 0x%08x @ %p", ret, module_start_ret);

            ret = sceKernelDlsym(result, "module_stop", (void **)&module_stop_ret);
            log_message("module_stop Dlsym 0x%08x @ %p", ret, module_stop_ret);

            if (module_start_ret && module_stop_ret)
            {
                log_message("Starting SPRX...");
                int32_t prx_ret = module_start_ret(0, nullptr);
                log_message("module_start returned with 0x%08x", prx_ret);

                if (prx_ret || prx_ret < 0)
                {
                    log_message("SPRX returned non-zero, stopping module...");
                    prx_ret = module_stop_ret(0, nullptr);
                    log_message("module_stop returned with 0x%08x", prx_ret);
                }
                else if (prx_ret == 0)
                    log_message("module_start exit successful 0x%08x", prx_ret);
            }
            else
                log_message("Unable to find module_start or module_stop!");

            char notify_msg[BUFFER_SIZE];
            snprintf(notify_msg, sizeof(notify_msg), "[OCAPI] SPRX Loaded:\n%s", path);
            sys_utils::text_notify(222, notify_msg);

            free((void *)path);
        }

        void load_plugin()
        {
            const char *plugin = nullptr;
            const char *start = strstr(server::relay::buffer.data(), "plugin=");

            if (start)
            {
                start += 7;
                const char *end = strchr(start, ' ');
                if (end)
                {
                    std::string encoded_plugin(start, end);
                    plugin = decode_url(encoded_plugin.c_str());
                }
            }

            if (!plugin)
            {
                log_message("No plugin provided to load");
                return;
            }

            struct proc_info info;
            sys_sdk_proc_info(&info);
            int pid = info.pid;

            std::string path = std::string("/data/GoldHEN/plugins/") + plugin;
            int32_t result = sceKernelLoadStartModule(path.c_str(), 0, 0, 0, NULL, NULL);

            if (result == 0x80020002)
            {
                log_message("Plugin %s not found", plugin);
                free((void *)plugin);
                return;
            }
            else if (result < 0)
            {
                log_message("Error loading Plugin %s! Error code 0x%08x (%i)", plugin, result, result);
                free((void *)plugin);
                return;
            }

            char response[BUFFER_SIZE];
            snprintf(response, sizeof(response), "%i,%d,%s", pid, result, plugin);
            send_formatted_response(response, server::relay::daemon_sock, &attached);

            int32_t ret;
            int32_t (*plugin_load_ret)(void);
            int32_t (*plugin_unload_ret)(void);

            ret = sceKernelDlsym(result, "plugin_load", (void **)&plugin_load_ret);
            log_message("plugin_load Dlsym 0x%08x @ %p", ret, plugin_load_ret);

            ret = sceKernelDlsym(result, "plugin_unload", (void **)&plugin_unload_ret);
            log_message("plugin_unload Dlsym 0x%08x @ %p", ret, plugin_unload_ret);

            if (plugin_load_ret && plugin_unload_ret)
            {
                log_message("Starting plugin...");
                int32_t prx_ret = plugin_load_ret();
                log_message("plugin_load returned with 0x%08x", prx_ret);

                if (prx_ret || prx_ret < 0)
                {
                    log_message("Plugin returned non-zero, stopping module...");
                    prx_ret = plugin_unload_ret();
                    log_message("plugin_unload returned with 0x%08x", prx_ret);
                }
                else if (prx_ret == 0)
                    log_message("plugin_load exit successful 0x%08x", prx_ret);
            }
            else
                log_message("Unable to find plugin_load or plugin_unload!");

            char notify_msg[BUFFER_SIZE];
            snprintf(notify_msg, sizeof(notify_msg), "[OCAPI] Plugin Loaded: %s", plugin);
            sys_utils::text_notify(222, notify_msg);

            free((void *)plugin);
        }

    }
}
