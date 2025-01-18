#include "../headers/includes.hpp"

std::unordered_map<ErrorCode, ErrorMessage> error_messages = {
    {INVALID_CMD, {"The requested command could not be found. Please check and try again."}},
    {NOT_CONNECTED, {"Not connected to the server. Please connect or check your connection."}},
    {NOT_ATTACHED, {"Not attached to process. Open an app, attach, and proceed to try again!"}}};

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
