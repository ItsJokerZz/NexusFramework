#include <orbis/libkernel.h>

#include <GoldHEN.h>

attr_public const char *g_pluginName = "OrbisControl";
attr_public const char *g_pluginDesc = "OCAPI app relay launcher.";
attr_public const char *g_pluginAuth = "ItsJokerZz";
attr_public uint32_t g_pluginVersion = 0x00000100;

int32_t attr_public plugin_load(int32_t argc, const char *argv[])
{
    int32_t prx_handle = 0;
    static void (*entry)(void);

    int32_t result = sceKernelLoadStartModule("/data/OrbisControl.prx", 0, 0, 0, NULL, NULL);
    if (result < 0)
        return -1;

    prx_handle = result;

    if (sceKernelDlsym(prx_handle, "entry", (void **)&entry) < 0 || entry == NULL)
        return -1;

    if (entry != NULL)
        entry();

    NotifyStatic(TEX_ICON_SYSTEM, "[OCAPI] App Relay Started");

    return 0;
}

int32_t attr_public plugin_unload(int32_t argc, const char *argv[])
{
    return 0;
}

s32 attr_module_hidden module_start(s64 argc, const void *args)
{
    return 0;
}

s32 attr_module_hidden module_stop(s64 argc, const void *args)
{
    return 0;
}
