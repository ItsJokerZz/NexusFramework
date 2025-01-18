#include "../headers/includes.hpp"

std::array<ErrorMessage, ERROR_COUNT> error_messages = {{
    {"The requested command could not be found. Please check and try again."},   // INVALID_CMD (0)
    {"Not connected to the server. Please connect or check your connection."},   // NOT_CONNECTED (1)
    {"Not attached to process. Open an app, attach, and proceed to try again."}, // NOT_ATTACHED (2)
    {"An unknown error has occured performing the current command, try again."}, // UNKNOWN_ERROR (3)
    {"Invalid or missing paramaters passed, please check the documentation."}    // INVALID_ARGS (4)
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

    if (titleId == DAEMON)
    {
        sceSysmoduleLoadModuleInternal(ORBIS_SYSMODULE_INTERNAL_USER_SERVICE);
        sceKernelLoadStartModule("libSceUserService.sprx", 0, NULL, 0, NULL, NULL);

        sceUserServiceInitialize2();

        isDaemon = true;
    }

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
