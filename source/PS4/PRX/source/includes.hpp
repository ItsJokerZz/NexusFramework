#pragma once

#include <string>
#include <cstdarg>
#include <stdio.h>
#include <stdint.h>
#include <wchar.h>
#include <fcntl.h>
#include <unistd.h>
#include <sys/uio.h>
#include <sys/stat.h>
#include <sys/socket.h>

#include <orbis/Net.h>
#include <orbis/libkernel.h>
#include <orbis/Sysmodule.h>
#include <orbis/ImeDialog.h>
#include <orbis/UserService.h>
#include <orbis/SystemService.h>

#include <GoldHEN.h>
#include <GoldHEN/Common.h>

#include "system.hpp"
#include "cmds.hpp"
#include "server.hpp"

extern "C"
{
    void sceSysUtilSendSystemNotificationWithText(int type, const char *message);

    int32_t sceSysmoduleLoadModule(OrbisSysModule moduleId),
        sceKernelGetSystemSwVersion(OrbisKernelSwVersion *version);

    uint32_t sceKernelGetCpuTemperature(uint32_t *celsius),
        sceKernelGetSocSensorTemperature(uint32_t *unknown, uint32_t *celsius);
}
