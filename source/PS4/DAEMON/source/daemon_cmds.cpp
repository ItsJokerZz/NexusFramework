#include "../headers/includes.hpp"

namespace cmds
{
    namespace daemon
    {
        void ping_relay()
        {
            send_response("true");
        }

        void attach_relay()
        {
            attached = true;

            sys_utils::text_notify(222, "[OCAPI] Attached!");
            send_response("done");
        }

        void load_module()
        {
            const char *path = nullptr;
            const char *start = strstr(data.buffers.relay.data(), "path=");

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
            send_response(response);

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

        void start_plugin()
        {
            const char *plugin = nullptr;
            const char *start = strstr(data.buffers.relay.data(), "plugin=");

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
                send_error_response("failed");
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
                send_error_response("failed");
                return;
            }
            else if (result < 0)
            {
                log_message("Error loading Plugin %s! Error code 0x%08x (%i)", plugin, result, result);
                free((void *)plugin);
                send_error_response("failed");
                return;
            }

            char response[BUFFER_SIZE];
            snprintf(response, sizeof(response), "%i,%d,%s", pid, result, plugin);
            send_response(response);

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

        void rw_proc_mem()
        {
            char buffer[128];

            std::pair<const char *, const char *>
                games[] = {
                    {reinterpret_cast<const char *>(0xB06FDA), "IW6SP"},
                    {reinterpret_cast<const char *>(0xC251D7), "IW6MP"}};

            for (const auto &game : games)
            {
                char logBuffer[256];

                proc_rw args;
                args.address = reinterpret_cast<uint64_t>(game.first);
                args.data = static_cast<void *>(buffer);
                args.length = sizeof(buffer);
                args.write_flags = 0;

                if (sys_sdk_proc_rw(&args) == 0)
                {
                    snprintf(logBuffer, sizeof(logBuffer), "%s -> IsMultiplayer%sfound!", game.second,
                             std::strcmp(buffer, "IsMultiplayer") == 0 ? " " : " NOT ");
                    sys_utils::text_notify(222, logBuffer);
                }
                else
                {
                    snprintf(logBuffer, sizeof(logBuffer), "Error reading memory for game %s", game.second);
                    sys_utils::text_notify(222, logBuffer);
                }
            }
        }

    }
}