#include "../headers/includes.hpp"

float version = 0.27;
int8_t build = 13;

bool isDaemon = false,
     unload = false,
     connected = false,
     attached = false;

extern "C" void entry()
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
}