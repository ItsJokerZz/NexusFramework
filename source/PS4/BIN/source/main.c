#include <ps4.h>
#include <stdbool.h>

static ScePthread thread;
static bool *unload;
static void *(*entry)(void *);

int64_t sceKernelDlsym(int64_t moduleHandle, const char *functionName, void *destFuncOffset) {
  return (int64_t)syscall(591, (void *)moduleHandle, (void *)functionName, destFuncOffset);
}

void sendNotification(const char *message) {
  sceSysUtilSendSystemNotificationWithText(222, (char *)message);
}

static int loadModuleAndSymbols(const char *modulePath, int *prx_id) {
  if (loadModule(modulePath, prx_id) != 0)
    return -1;

  if (sceKernelDlsym(*prx_id, "unload", (void **)&unload) < 0 || unload == NULL)
    return -1;

  if (sceKernelDlsym(*prx_id, "entry", (void **)&entry) < 0 || entry == NULL)
    return -1;

  *unload = false;

  return 0;
}

int _main(void) {
  initKernel();
  initLibc();
  jailbreak();
  initSysUtil();
  initPthread();

  int prx_id;
  if (loadModuleAndSymbols("/data/OrbisControl.prx", &prx_id) < 0)
    return -1;

  if (scePthreadCreate(&thread, NULL, entry, NULL, "OpenControlAPI") != 0)
    return -1;

  scePthreadJoin(thread, NULL);

  for (;;) {
    if (*unload) {
      sendNotification("[OCAPI] Unloaded!");
      unloadModule(prx_id);
      scePthreadDetach(thread);

      break;
    }
    sceKernelUsleep(1000000);
  }

  return 0;
}
