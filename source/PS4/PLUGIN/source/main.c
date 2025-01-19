#include <orbis/libkernel.h>

#include <GoldHEN.h>

attr_public const char *g_pluginName = "OrbisControl";
attr_public const char *g_pluginDesc = "OCAPI app relay launcher.";
attr_public const char *g_pluginAuth = "ItsJokerZz";
attr_public uint32_t g_pluginVersion = 0x00000100;

uint32_t module_handle = -1;

int32_t attr_public plugin_load(int32_t argc, const char *argv[])
{
    // Load the module and store the handle
    module_handle = sceKernelLoadStartModule("/data/OrbisControl.prx", 0, NULL, 0, NULL, NULL);

    if (module_handle == 0)
    {
        NotifyStatic(TEX_ICON_SYSTEM, "OrbisControl.prx NOT FOUND.");
        return 1;
    }

    NotifyStatic(TEX_ICON_SYSTEM, "[OCAPI] App Relay Started");
    return 0;
}

int32_t attr_public plugin_unload(int32_t argc, const char *argv[])
{
    int32_t result = 0; // Variable to store the result of the unload operation

    // If the module was loaded, stop and unload it
    if (module_handle != 0)
    {
        result = sceKernelStopUnloadModule(module_handle, 0, NULL, 0, NULL, NULL);

        if (result < 0)
        {
            NotifyStatic(TEX_ICON_SYSTEM, "[OCAPI] Failed to stop/unload App Relay");
        }
        else
        {
            NotifyStatic(TEX_ICON_SYSTEM, "[OCAPI] App Relay Stopped");
        }
    }

    return result;
}
