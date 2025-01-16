#include <stdbool.h>
#include <sys/socket.h>

#include <orbis/Net.h>
#include <orbis/Http.h>
#include <orbis/libkernel.h>

#include <libjbc.h>
#include <GoldHEN.h>

attr_public const char *g_pluginName = "OrbisControl";
attr_public const char *g_pluginDesc = "OCAPI app relay launcher.";
attr_public const char *g_pluginAuth = "ItsJokerZz";
attr_public uint32_t g_pluginVersion = 0x00000100;

int32_t attr_public plugin_load(int32_t argc, const char *argv[])
{

    if (sceKernelLoadStartModule("/data/OrbisControl.prx", 0, 0, 0, NULL, NULL) < 0)
    {
        NotifyStatic(TEX_ICON_SYSTEM, "OrbisControl.prx NOT FOUND.");

        return 1;
    }

    NotifyStatic(TEX_ICON_SYSTEM, "[OCAPI] App Relay Started");

    return 0;
}

int32_t attr_public plugin_unload(int32_t argc, const char *argv[])
{
    return 0;
}
