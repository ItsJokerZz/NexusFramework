#include "../headers/includes.hpp"

namespace sys_utils
{
  std::string console_type;

  int sys_proc_list(struct proc_list_entry *procs, uint64_t *num)
  {
    return orbis_syscall(107 + 90, procs, num);
  }

  int find_pid_by_procName(const char *proc_name, int *pid)
  {
    struct proc_list_entry *proc_list;
    uint64_t pnum;

    if (sys_proc_list(NULL, &pnum))
      return 0;

    proc_list = (struct proc_list_entry *)malloc(pnum * sizeof(struct proc_list_entry));

    if (!proc_list)
      return 0;

    if (sys_proc_list(proc_list, &pnum))
    {
      free(proc_list);
      return 0;
    }

    for (size_t i = 0; i < pnum; i++)
      if (strncmp(proc_list[i].p_comm, proc_name, 32) == 0)
      {
        *pid = proc_list[i].pid;
        free(proc_list);
        return 1;
      }

    free(proc_list);
    return 0;
  }

  int find_procName_of_pid(int pid, char *proc_name)
  {
    struct proc_list_entry *proc_list;
    uint64_t pnum;

    if (sys_proc_list(NULL, &pnum))
      return 0;

    proc_list = (struct proc_list_entry *)malloc(pnum * sizeof(struct proc_list_entry));

    if (!proc_list)
      return 0;

    if (sys_proc_list(proc_list, &pnum))
    {
      free(proc_list);

      return 0;
    }

    for (size_t i = 0; i < pnum; i++)
    {
      if (proc_list[i].pid == pid)
      {
        strncpy(proc_name, proc_list[i].p_comm, 32);

        proc_name[31] = '\0';
        free(proc_list);

        return 1;
      }
    }

    free(proc_list);
    return 0;
  }

  int sys_proc_rw(uint64_t pid, uint64_t address, void *data, uint64_t length, uint64_t write)
  {
    return syscall(108 + 90, pid, address, data, length, write);
  }

  const char *get_username(OrbisUserServiceUserId userId)
  {
    static char username[ORBIS_USER_SERVICE_MAX_USER_NAME_LENGTH + 1];
    OrbisUserServiceLoginUserIdList idList = {};

    if (sceUserServiceGetInitialUser(&userId) == 0 && userId != -1)
    {
      if (sceUserServiceGetUserName(userId, username, sizeof(username)) == 0)
        return username;
    }

    if (sceUserServiceGetLoginUserIdList(&idList) == 0)
    {
      if (sceUserServiceGetUserName(idList.userId[0], username, sizeof(username)) == 0)
        return username;
    }

    return "USER";
  }

  const char *get_console_type()
  {
    int32_t cex = sceKernelIsCEX();
    int32_t devKit = sceKernelIsDevKit();
    int32_t testKit = sceKernelIsTestKit();

    if (cex)
      console_type = "CEX";
    if (devKit)
      console_type = "KIT";
    if (testKit)
      console_type = "TEST";

    return console_type.c_str();
  }

  const char *get_fw_version(void)
  {
    static char versionString[0x1C];
    OrbisKernelSwVersion versionInfo;

    if (sceKernelGetSystemSwVersion(&versionInfo) < 0)
      return NULL;

    int major, minor;
    if (sscanf(versionInfo.VersionString, "%02d.%02d", &major, &minor) == 2)
      snprintf(versionString, sizeof(versionString), "%02d.%02d", major, minor);
    else
      return NULL;

    return versionString;
  }

  uint32_t get_cpu_temperature()
  {
    uint32_t celsius;
    sceKernelGetCpuTemperature(&celsius);
    return celsius;
  }

  uint32_t get_soc_temperature()
  {
    uint32_t celsius;
    sceKernelGetSocSensorTemperature(0, &celsius);
    return celsius;
  }

  void text_notify(int type, const char *_msg)
  {
    std::string msg = _msg;
    sceSysUtilSendSystemNotificationWithText(type, msg.c_str());
  }

  void image_notify(const char *IconUri, const char *text)
  {
    if (IconUri == NULL)
      IconUri = "cxml://psnotification/tex_icon_system";

    OrbisNotificationRequest Buffer;
    Buffer.type = NotificationRequest;
    Buffer.unk3 = 0;
    Buffer.useIconImageUri = 1;
    Buffer.targetId = -1;
    strcpy(Buffer.message, text);
    strcpy(Buffer.iconUri, IconUri);
    sceKernelSendNotificationRequest(0, &Buffer, sizeof(Buffer), 0);
  }

  void set_temperature_limit(uint8_t limit)
  {
    int fd = open("/dev/icc_fan", O_RDONLY);
    if (fd < 0)
      return;
    char data[10] = {0x00, 0x00, 0x00, 0x00, 0x00,
                     static_cast<char>(limit),
                     0x00, 0x00, 0x00, 0x00};
    ioctl(fd, 0xC01C8F07, data);
    close(fd);
  }

  void set_power_state(power_state state)
  {
    if (state == DO_NOTHING)
      return;

    orbis_syscall(37, 1, (int)state);
  }

  void ring_buzzer(int type)
  {
    sceKernelIccSetBuzzer(type);
  }

}