#include <ps4.h>

#include <stdbool.h>

static ScePthread thread;
static bool * loadedFromBIN;
static bool * breakThread;
static void * ( * entry)(void * );

int64_t sceKernelDlsym(int64_t moduleHandle,
  const char * functionName, void * destFuncOffset) {
  return (int64_t) syscall(591, (void * ) moduleHandle, (void * ) functionName, destFuncOffset);
}

void sendNotification(const char * message) {
  sceSysUtilSendSystemNotificationWithText(222, (char * ) message);
}

static int loadModuleAndSymbols(const char * modulePath) {
  int prx_id = sceKernelLoadStartModule(modulePath, 0, NULL, 0, NULL, NULL);
  if (prx_id < 0) return -1;

  if (sceKernelDlsym(prx_id, "loadedFromBIN", (void ** ) & loadedFromBIN) < 0 || loadedFromBIN == NULL) return -1;
  if (sceKernelDlsym(prx_id, "breakThread", (void ** ) & breakThread) < 0 || breakThread == NULL) return -1;

  if (sceKernelDlsym(prx_id, "entry", (void ** ) & entry) < 0 || entry == NULL) return -1;

  * loadedFromBIN = true;
  * breakThread = false;

  return 0;
}

int _main(void) {
  initKernel();
  initLibc();
  jailbreak();
  initSysUtil();
  initPthread();

  if (loadModuleAndSymbols("/data/OrbisControl.prx") < 0) return -1;
  if (scePthreadCreate( & thread, NULL, entry, NULL, "OpenControlAPI") != 0) return -1;

  scePthreadJoin(thread, NULL);

  for (;;) {
    if ( * breakThread) {
      sendNotification("[OCAPI] Unloaded!");
      scePthreadDetach(thread); break;
    } sceKernelUsleep(1000000);
  }

  return 0;
}