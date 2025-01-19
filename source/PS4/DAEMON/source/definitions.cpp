#include "../headers/includes.hpp"

std::array<ErrorMessage, ERROR_COUNT> error_messages = {{
    {"Server is running, but no command was passed. Please check."},
    {"Command not found. Please check and try again."},
    {"Not connected to the server. Check your connection."},
    {"Not attached to process. Open an app and attach."},
    {"An unknown error occurred. Please try again later."},
    {"Invalid parameters provided. Please verify and retry."},
}};

bool isDaemon = false,
     unload = false,
     connected = false,
     attached = false;

int32_t module_start(int64_t args, const void *argp)
{
    pthread_t thread;

    struct proc_info info;
    sys_sdk_proc_info(&info);

    if (strcmp(info.titleid, DAEMON) == 0)
        pthread_create(&thread, nullptr, server::daemon::thread, nullptr);
    else
        pthread_create(&thread, nullptr, server::relay::thread, nullptr);

    pthread_detach(thread);

    return 0;
}

extern "C" int32_t __wrap__init(size_t args, const void *argp)
{
    return module_start(args, argp);
}
