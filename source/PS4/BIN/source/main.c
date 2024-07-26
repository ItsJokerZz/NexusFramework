#include <ps4.h>
#include <stdbool.h>

static ScePthread thread;
static bool* loadedFromBIN;
static void* (*entry)(void*);

int64_t sceKernelDlsym(int64_t moduleHandle, const char* functionName, void *destFuncOffset) {
    return (int64_t)syscall(591, (void*)moduleHandle, (void*)functionName, destFuncOffset);
}

void sendNotification(const char* message) {
    sceSysUtilSendSystemNotificationWithText(222, (char*)message);
}

static int loadModuleAndSymbols(const char* modulePath) {
    int prx_id = sceKernelLoadStartModule(modulePath, 0, NULL, 0, NULL, NULL);
    if (prx_id < 0) return -1;

    if (sceKernelDlsym(prx_id, "loadedFromBIN", (void**)&loadedFromBIN) < 0 || loadedFromBIN == NULL) return -1;
    *loadedFromBIN = true;

    if (sceKernelDlsym(prx_id, "entry", (void**)&entry) < 0 || entry == NULL) return -1;

    return 0;
}

int _main(void) {
    initKernel();
    initLibc();
    jailbreak();
    initSysUtil();
    initPthread();

    if (loadModuleAndSymbols("/data/ItsJokerZz/OCAPI.sprx") < 0) return -1;

    if (scePthreadCreate(&thread, NULL, entry,
    NULL, "OpenControlAPI") != 0) return -1;

    scePthreadJoin(thread, NULL); return 0;
}
