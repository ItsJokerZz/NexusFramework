#include <ps4.h>
#include <stdbool.h>

static int32_t (*_init)(size_t, const void *);
static bool *unload;

int64_t sceKernelDlsym(int64_t moduleHandle, const char *functionName, void *destFuncOffset) {
  return (int64_t)syscall(591, (void *)moduleHandle, (void *)functionName, destFuncOffset);
}

int _main(void) {
  int prx_id;

  initKernel();
  initLibc();
  jailbreak();
  initSysUtil();
  initPthread();

  if (loadModule("/data/GoldHEN/plugins/OrbisControl.prx", &prx_id) != 0)
    return -1;

  if (sceKernelDlsym(prx_id, "__wrap__init", (void **)&_init) < 0 || _init == NULL)
    return -1;

  if (sceKernelDlsym(prx_id, "unload", (void **)&unload) < 0 || unload == NULL)
    return -1;

  _init(0, NULL);

  while (!*unload) {
    sceKernelSleep(1);
  }

  unloadModule(prx_id);

  sceSysUtilSendSystemNotificationWithText(222, "[OCAPI] Unloaded!");

  return 0;
}
