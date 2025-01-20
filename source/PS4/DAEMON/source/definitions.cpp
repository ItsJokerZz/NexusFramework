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
bool unload = false, connected = false, attached = false;

extern "C"
{

    extern "C"
    {
        int32_t __wrap__init(size_t, const void *)
        {
            struct proc_info info;
            sys_sdk_proc_info(&info);

            bool isDaemon = (strcmp(info.titleid, DAEMON) == 0);
            
            pthread_t *thread = isDaemon ? &daemon_thread : &relay_thread;
            void *(*thread_func)(void *) = isDaemon
                                               ? (void *(*)(void *))server::daemon::thread
                                               : (void *(*)(void *))server::relay::thread;

            if (*thread == -1 && pthread_create(thread, nullptr, thread_func, nullptr) == 0)
                pthread_detach(*thread);

            std::string msg = isDaemon
                                  ? "[OCAPI] Daemon server started!"
                                  : "[OCAPI] Relay server started";

            sys_utils::text_notify(222, msg.c_str());

            return 0;
        }
    }

    int32_t __wrap__fini(size_t, const void *)
    {
        sys_utils::text_notify(222, "Unloaded!");
        sceKernelStopUnloadModule(0, 0, NULL, 0, NULL, NULL);

        return 0;
    }
}
