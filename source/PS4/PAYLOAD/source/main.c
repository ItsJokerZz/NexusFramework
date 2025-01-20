#include <ps4.h>

static int32_t (*__wrap__init)(size_t, const void *);

int64_t sceKernelDlsym(int64_t moduleHandle, const char *functionName, void *destFuncOffset) {
  return (int64_t)syscall(591, (void *)moduleHandle, (void *)functionName, destFuncOffset);
}

int _main(void) {
  initKernel();
  initLibc();

  int prx_id;

  if (loadModule("/user/data/GoldHEN/plugins/OrbisControl.prx", &prx_id) != 0)
    return -1;

  if (sceKernelDlsym(prx_id, "__wrap__init", (void **)&__wrap__init) < 0 || __wrap__init == NULL)
    return -1;

  __wrap__init(0, NULL);

  return 0;
}
