#include "../headers/includes.hpp"

namespace sys_utils
{
  std::string console_type;

  int sys_proc_list(struct proc_list_entry *procs, uint64_t *num)
  {
    return orbis_syscall(107 + 90, procs, num);
  }

  int find_process_pid(const char *proc_name, int *pid)
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

  int32_t get_system_language_id()
  {
    int32_t languageID = -1;
    sceSystemServiceParamGetInt(ORBIS_SYSTEM_SERVICE_PARAM_ID_LANG, &languageID);
    return languageID;
  }

  const char *get_system_language()
  {
    static const char *languages[] = {
        "jp", "en-US", "fr", "es", "de", "it", "nl", "pt-PT",
        "ru", "ko", "zh-TW", "zh-CN", "fi", "sv", "da", "no",
        "pl", "pt-BR", "en-GB", "tr", "es-LA", "ar", "fr-CA", "cs",
        "hu", "el", "ro", "th", "vi", "id"};

    int32_t langID = get_system_language_id();
    if (langID >= ORBIS_SYSTEM_PARAM_LANG_JAPANESE &&
        langID <= ORBIS_SYSTEM_PARAM_LANG_INDONESIAN)
    {
      return languages[langID];
    }
    return "NULL";
  }

  const char *get_fw_version()
  {
    static char versionString[0x1C];
    OrbisKernelSwVersion versionInfo;
    if (sceKernelGetSystemSwVersion(&versionInfo) < 0)
      return NULL;
    strncpy(versionString, versionInfo.VersionString, 5);
    versionString[5] = '\0';
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

  void ring_buzzer(int type)
  {
    sceKernelIccSetBuzzer(type);
  }
}
