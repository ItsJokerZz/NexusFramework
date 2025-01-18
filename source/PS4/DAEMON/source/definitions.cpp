#include "../headers/includes.hpp"

std::array<ErrorMessage, ERROR_COUNT> error_messages = {{
    {"The requested command could not be found. Please check and try again."},  // INVALID_CMD
    {"Not connected to the server. Please connect or check your connection."},  // NOT_CONNECTED
    {"Not attached to process. Open an app, attach, and proceed to try again."}, // NOT_ATTACHED
    {"An unknown error has occured performing the current command, try again."} // UNKNOWN_ERROR
}};

bool isDaemon = false,
     unload = false,
     connected = false,
     attached = false;

int32_t module_start(int64_t args, const void *argp)
{
    struct proc_info info;
    sys_sdk_proc_info(&info);

    std::string titleId = info.titleid;

    if (titleId == "NPXS21002")
        isDaemon = true;

    if (isDaemon)
        server::daemon::start();
    else
        server::relay::start();

    return 0;
}

extern "C" int32_t __wrap__init(size_t args, const void *argp)
{
    return module_start(args, argp);
}
