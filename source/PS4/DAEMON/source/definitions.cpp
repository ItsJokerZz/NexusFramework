#include "../headers/includes.hpp"

std::array<ErrorMessage, ERROR_COUNT> error_messages = {{
    {"Server is running, but no command was passed. Please check."},
    {"Command not found. Please check and try again."},
    {"Not connected to the server. Check your connection."},
    {"Not attached to process. Open an app and attach."},
    {"An unknown error occurred. Please try again later."},
    {"Invalid parameters provided. Please verify and retry."},
}};

bool isDaemon = false, unload = false,
     connected = false, attached = false;

pthread_t daemon_thread = -1, relay_thread = -1;

int32_t module_start(int64_t args, const void *argp)
{
    sys_utils::text_notify(222, "[OCAPI] Daemon server started!");

    if (daemon_thread == -1)
    {
        if (pthread_create(&daemon_thread, nullptr, server::daemon::thread, nullptr) != 0)
        {
            sys_utils::text_notify(222, "[OCAPI] Failed to create daemon thread!");

            return -1;
        }

        pthread_detach(daemon_thread);
    }

    return 0;
}

int32_t plugin_load(int64_t args, const void *argp)
{
    sys_utils::text_notify(222, "[OCAPI] App relay server started!");

    if (relay_thread == -1)
    {
        if (pthread_create(&relay_thread, nullptr, server::relay::thread, nullptr) != 0)
        {
            sys_utils::text_notify(222, "[OCAPI] Failed to create relay thread!");

            return -1;
        }

        pthread_detach(relay_thread);
    }

    return 0;
}

extern "C"
{
    int32_t __wrap__init(size_t args, const void *argp)
    {
        struct proc_info info;
        sys_sdk_proc_info(&info);

        if (strcmp(info.titleid, DAEMON) == 0)
            return module_start(args, argp);
        else
            return plugin_load(args, argp);
    }

    int32_t __wrap__fini(size_t args, const void *argp)
    {
        sys_utils::text_notify(222, "_fini(size_t, const void *);");

        auto stopUnloadModule = [](size_t args, const void *argp)
        {
            sys_utils::text_notify(222, "[OCAPI] Unloaded!");

            return sceKernelStopUnloadModule(args, 0, NULL, 0, NULL, NULL);
        };

        return stopUnloadModule(args, argp);
    }
}
