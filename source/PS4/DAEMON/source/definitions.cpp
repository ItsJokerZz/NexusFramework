#include "../headers/includes.hpp"

std::array<ErrorMessage, ERROR_COUNT> error_messages = {{
    {"The requested command could not be found. Please check and try again."},   // INVALID_CMD (0)
    {"Not connected to the server. Please connect or check your connection."},   // NOT_CONNECTED (1)
    {"Not attached to process. Open an app, attach, and proceed to try again."}, // NOT_ATTACHED (2)
    {"An unknown error has occured performing the current command, try again."}, // UNKNOWN_ERROR (3)
    {"Invalid paramaters passed, please check documentation, and try again."}    // INVALID_ARGS (4)
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
