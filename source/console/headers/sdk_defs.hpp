#pragma once

#include <stddef.h>
#include <stdint.h>
#include <sys/types.h>

#define KERNEL_PRIO_FIFO_HIGHEST 0x100
#define KERNEL_PRIO_FIFO_NORMAL 0x2BC
#define KERNEL_PRIO_FIFO_LOWEST 0x2FF

#define SYSTEM_SERVICE_PARAM_ID_SYSTEM_NAME 6
#define USER_SERVICE_MAX_USER_NAME_LENGTH 16

#define SCE_NOTIFICATION_LOCAL_USER_ID_SYSTEM 0xFE

#define SCE_MONOTONIC 4

#define NET_CTL_INFO_ETHER_ADDR 2
#define NET_CTL_INFO_IP_ADDRESS 14

struct app_info
{
  int32_t AppId;

  uint8_t _padding0[12];

  char TitleId[10];

  uint8_t _padding1[6];
  uint8_t _padding2[8];
  uint8_t _padding3[4];
  uint8_t _padding4[4];
  uint8_t _padding5[16];

  uint8_t _padding6[64];
};

struct notify_request
{
  int type;

  char pad0[sizeof(int32_t[10])];
  bool use_icon;
  char message[1024];
  char icon_uri[1024];
  char pad2[1024];
};

struct launch_app_params
{
  int size;
  int user_id;
  int app_option;
  uint64_t crash_report;
  int check_flag;
};

struct launch_uri_params
{
  unsigned int size;
  int user_id;
};

struct kernel_sw_info
{
  uint64_t pad0;

  char version_string[0x1C];
  uint32_t version;

  uint64_t pad1;
};

struct user_service_init_params
{
  uint32_t priority;
};

struct process_stats
{
  int32_t lo_data;
  uint32_t td_tid;
  timespec user_cpu_usage_time;
  timespec system_cpu_usage_time;
};

typedef struct SceNetEtherAddr
{
  uint8_t data[6];
} SceNetEtherAddr;

typedef union SceNetCtlInfo
{
  SceNetEtherAddr ether_addr;
  char ip_address[16];
  uint8_t _pad[256];
} SceNetCtlInfo;

extern "C"
{
  int get_page_table_stats(int vm, unsigned long long Table, int *totalOut,
                           int *AvailableOut);

  bool sceKernelIsTestKit(void);
  bool sceKernelIsDevKit(void);

  int sceKernelGetProcessName(int pid, char *name);
  void sceKernelSetProcessName(const char *name);
  int sceKernelGetAppInfo(pid_t pid, app_info *info);
  int sceKernelGetThreadName(int id, char *out);

  int sceKernelLoadStartModule(const char *name, size_t argc, const void *argv,
                               unsigned int flags, int option, int ret);
  int sceKernelDlsym(int handle, const char *symbol, void **address);
  int sceKernelMprotect(const void *addr, size_t size, int prot);

  int sceKernelGetCpuTemperature(int *out);
  int sceKernelGetSocSensorTemperature(int unk, int *out);
  int sceKernelGetCpuFrequency(void);
  int sceKernelGetCpuUsage(struct process_stats *out, int *size);

  int sceKernelGetHwSerialNumber(char *out);
  int sceKernelGetHwModelName(char *out);
  void sceKernelGetIdPs(void *ret);
  int sceKernelGetOpenPsIdForSystem(void *ret);
  int sceKernelClockGettime(clockid_t clockId, struct timespec *outTime);
  int sceKernelGetSystemSwVersion(kernel_sw_info *sw);
  int sceKernelGetProsperoSystemSwVersion(kernel_sw_info *sw);

  void sceKernelIccSetBuzzer(int type);

  int sceKernelSendNotificationRequest(int device, notify_request *request,
                                       size_t size, bool blocking);

#ifdef __PROSPERO__
  int sceNotificationSend(int userId, bool isLogged, const char *toast);
#endif

  int sceSystemServiceParamGetString(int paramId, char *buf, size_t bufSize);

  int sceSystemServiceGetAppIdOfBigApp(void);

  int sceSystemServiceLaunchApp(const char *titleId, const char *args[],
                                launch_app_params *params);

  int sceLncUtilLaunchApp(const char *titleId, const char *args[],
                          launch_app_params *params);

#ifdef __PROSPERO__
  int sceSystemServiceGetAppIdOfRunningBigApp(void);
#endif

  int sceNetCtlInit(void);
  int sceNetCtlGetInfo(int size, SceNetCtlInfo *info);

  int sceUserServiceInitialize(user_service_init_params *params);
  int sceUserServiceGetForegroundUser(int *userId);
  int sceUserServiceGetUserName(int userId, char *outName, size_t nameSize);

  int sceSystemStateMgrReboot();
  int sceSystemStateMgrTurnOff();
  int sceSystemStateMgrEnterStandby();

  void sceKernelIccIndicatorBootDone(void);
  void sceKernelIccIndicatorShutdown(void);
  void sceKernelIccIndicatorStandby(void);
  void sceKernelIccIndicatorStandbyBoot(void);
  void sceKernelIccIndicatorStandbyShutdown(void);
}

inline int (*sceShellUIUtilLaunchByUri)(const char *uri,
                                        launch_uri_params *param) = nullptr;
