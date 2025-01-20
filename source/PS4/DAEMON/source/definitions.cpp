#include "../headers/includes.hpp"

std::array<ErrorMessage, ERROR_COUNT> error_messages = {{
    {"Server is running, but no command was passed. Please check."},
    {"Command not found. Please check and try again."},
    {"Not connected to the server. Check your connection."},
    {"Not attached to process. Open an app and attach."},
    {"An unknown error occurred. Please try again later."},
    {"Invalid parameters provided. Please verify and retry."},
}};

pthread_t daemon_thread = -1, relay_thread = -1;

bool isDaemon = false, unloaded = false,
     connected = false, attached = false;

extern "C"
{
    int32_t __wrap__init(size_t args, const void *argp)
    {
        struct proc_info info;
        sys_sdk_proc_info(&info);

        isDaemon = (strcmp(info.titleid, DAEMON) == 0);
        pthread_t *thread = isDaemon ? &daemon_thread : &relay_thread;
        void *(*thread_func)(void *) = isDaemon
                                           ? (void *(*)(void *))server::daemon::thread
                                           : (void *(*)(void *))server::relay::thread;

        sys_utils::text_notify(222, (std::string("[OCAPI] ") +
                                     (isDaemon ? "Daemon server started!" : "Relay server started"))
                                        .c_str());

        if (*thread == -1)
        {
            if (pthread_create(thread, nullptr, thread_func, nullptr) != 0)
            {
                sys_utils::text_notify(222, (std::string("[OCAPI] Failed to create ") +
                                             (isDaemon ? "daemon" : "relay") + " thread!")
                                                .c_str());

                return 1;
            }
            pthread_detach(*thread);
        }

        return 0;
    }

    int32_t __wrap__fini(size_t args, const void *argp)
    {
        if (isDaemon)
        {
            sys_utils::text_notify(222, "[OCAPI] Unloaded!");
            sceSystemServiceLoadExec("exit", 0);
        }

        return 0;
    }
}
