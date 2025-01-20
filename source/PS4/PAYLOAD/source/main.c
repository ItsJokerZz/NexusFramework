#include <ps4.h>
#include <stdbool.h>

static bool *unload;
static int32_t (*_init)(size_t, const void *);
static int32_t (*_fini)(size_t, const void *);

int sceKernelDlsym(int64_t moduleHandle, const char *functionName, void *destFuncOffset) {
  return (int64_t)syscall(591, (void *)moduleHandle, (void *)functionName, destFuncOffset);
}

int _main(void) {
  int prx_id;

  initKernel();
  initLibc();
  jailbreak();

  if (loadModule("/data/GoldHEN/plugins/OrbisControl.prx", &prx_id) != 0)
    return -1;

  if (sceKernelDlsym(prx_id, "unload", (void **)&unload) < 0 || !unload)
    return -1;

  if (sceKernelDlsym(prx_id, "__wrap__init", (void **)&_init) < 0 || !_init)
    return -1;

  if (sceKernelDlsym(prx_id, "__wrap__fini", (void **)&_fini) < 0 || !_fini)
    return -1;

  _init(0, NULL);

  while (!*unload)
    sceKernelSleep(1);

  _fini(prx_id, NULL);

  return 0;
}
