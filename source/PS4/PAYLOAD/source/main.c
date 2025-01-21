#include <ps4.h>

static int32_t (*__init)(size_t, const void *);

int64_t dlsym(int64_t moduleHandle, const char *functionName, void *destFuncOffset) {
  return (int64_t)syscall(591, (void *)moduleHandle, (void *)functionName, destFuncOffset);
}

int _main(void) {
  int prx_id;
  
  initKernel();

  if (loadModule("/user/data/GoldHEN/plugins/ItsJokerZz/OrbisControl.prx", &prx_id) != 0 ||
      dlsym(prx_id, "__wrap__init", (void **)&__init) < 0 || __init == NULL)
    return -1;

  __init(0, NULL);

  return 0;
}
