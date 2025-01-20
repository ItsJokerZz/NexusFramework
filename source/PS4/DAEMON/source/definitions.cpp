#include "../headers/includes.hpp"

std::array<ErrorMessage, ERROR_COUNT> error_messages = {{
    {"Server is running, but no command was passed. Please check."},
    {"Command not found. Please check and try again."},
    {"Not connected to the server. Check your connection."},
    {"Not attached to process. Open an app and attach."},
    {"An unknown error occurred. Please try again later."},
    {"Invalid parameters provided. Please verify and retry."},
}};

bool
    isDaemon = false,
    unload = false,
    connected = false,
    attached = false;

pthread_t
    daemon_thread = -1,
    relay_thread = -1;

extern "C"
{
    int32_t __wrap__init(size_t args, const void *argp)
    {
        struct proc_info info;
        sys_sdk_proc_info(&info);

        auto startServer = [&info](bool isDaemonProcess)
        {
            const char *msg = isDaemonProcess ? "[OCAPI] Daemon server started!" : "[OCAPI] App relay server started!";

            sys_utils::text_notify(222, msg);

            pthread_t *thread = isDaemonProcess ? &daemon_thread : &relay_thread;

            if (*thread == -1)
            {
                if (pthread_create(thread, nullptr, isDaemonProcess ? server::daemon::thread : server::relay::thread, nullptr) != 0)
                {
                    sys_utils::text_notify(222, isDaemonProcess ? "[OCAPI] Failed to create daemon thread!" : "[OCAPI] Failed to create relay thread!");

                    return -1;
                }

                pthread_detach(*thread);
            }
            return 0;
        };

        return startServer(strcmp(info.titleid, DAEMON) == 0);
    }

    int32_t __wrap__fini(size_t args, const void *argp)
    {
        sys_utils::text_notify(222, "[OCAPI] Unloaded!");

        return sceKernelStopUnloadModule(args, 0, nullptr, 0, nullptr, nullptr);
    }
}
