#include "../headers/includes.hpp"

std::string console_type;

std::string get_title_id()
{
  DIR *dir = opendir("/mnt/sandbox");
  if (!dir)
    return "User Interface (UI)"; // Return default if directory can't be opened.

  std::string title_id = "User Interface (UI)";
  struct dirent *entry;

  while ((entry = readdir(dir)) != nullptr)
  {
    std::string entry_name = entry->d_name;
    std::regex pattern("^[A-Za-z0-9]{4}[0-9]{5}$");

    if (std::regex_match(entry_name, pattern) && entry_name.substr(0, 4) != "NPXS")
    {
      title_id = entry_name;
      break;
    }
  }

  closedir(dir);
  return title_id;
}

int get_proc_list(struct proc_list_entry *procs, uint64_t *num)
{
  return orbis_syscall(107 + 90, procs, num);
}

int find_pid_by_procName(const char *proc_name, int *pid)
{
  struct proc_list_entry *proc_list = nullptr;
  uint64_t pnum;

  if (get_proc_list(nullptr, &pnum) || !(proc_list = (struct proc_list_entry *)malloc(pnum * sizeof(struct proc_list_entry))))
    return 0;

  if (get_proc_list(proc_list, &pnum))
  {
    free(proc_list);
    return 0;
  }

  for (size_t i = 0; i < pnum; ++i)
  {
    if (strncmp(proc_list[i].p_comm, proc_name, 32) == 0)
    {
      *pid = proc_list[i].pid;
      free(proc_list);
      return 1;
    }
  }

  free(proc_list);
  return 0;
}

int find_procName_of_pid(int pid, char *proc_name)
{
  struct proc_list_entry *proc_list = nullptr;
  uint64_t pnum;

  if (get_proc_list(nullptr, &pnum) || !(proc_list = (struct proc_list_entry *)malloc(pnum * sizeof(struct proc_list_entry))))
    return 0;

  if (get_proc_list(proc_list, &pnum))
  {
    free(proc_list);
    return 0;
  }

  for (size_t i = 0; i < pnum; ++i)
  {
    if (proc_list[i].pid == pid)
    {
      strncpy(proc_name, proc_list[i].p_comm, 32);
      proc_name[31] = '\0'; // Ensure null-termination
      free(proc_list);
      return 1;
    }
  }

  free(proc_list);
  return 0;
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
  if (sceKernelIsDevKit())
    return "KIT";
  if (sceKernelIsTestKit())
    return "TEST";
  return "CEX"; // Default to "CEX"
}

const char *get_fw_version(void)
{
  static char versionString[0x1C];
  OrbisKernelSwVersion versionInfo;

  if (sceKernelGetSystemSwVersion(&versionInfo) < 0)
    return nullptr;

  int major, minor;
  if (sscanf(versionInfo.VersionString, "%02d.%02d", &major, &minor) == 2)
    snprintf(versionString, sizeof(versionString), "%02d.%02d", major, minor);

  return versionString[0] ? versionString : nullptr;
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

bool has_entered_restmode()
{
  OrbisKernelEventFlag flag;
  if (sceKernelOpenEventFlag(&flag, "SceSystemStateMgrInfo") != 0)
    return false;

  uint64_t state;
  if (sceKernelPollEventFlag(flag, 0xFFFF, SCE_KERNEL_EVF_WAITMODE_OR, &state) != 0)
    return false;

  return (state == 1000 && sceKernelPollEventFlag(flag, 0x200000, SCE_KERNEL_EVF_WAITMODE_OR, 0) == 0) || state == 500;
}

void text_notify(int type, const char *_msg)
{
  sceSysUtilSendSystemNotificationWithText(type, _msg);
}

void image_notify(const char *IconUri, const char *text)
{
  if (IconUri == nullptr)
    IconUri = "cxml://psnotification/tex_icon_system";

  OrbisNotificationRequest Buffer = {};
  Buffer.type = NotificationRequest;
  Buffer.unk3 = 0;
  Buffer.useIconImageUri = 1;
  Buffer.targetId = -1;
  strncpy(Buffer.message, text, sizeof(Buffer.message));
  strncpy(Buffer.iconUri, IconUri, sizeof(Buffer.iconUri));

  sceKernelSendNotificationRequest(0, &Buffer, sizeof(Buffer), 0);
}

void set_temperature_limit(uint8_t limit)
{
  int fd = open("/dev/icc_fan", O_RDONLY);
  if (fd < 0)
    return;

  char data[10] = {0x00, 0x00, 0x00, 0x00, 0x00, static_cast<char>(limit), 0x00, 0x00, 0x00, 0x00};
  ioctl(fd, 0xC01C8F07, data);
  close(fd);
}

void set_power_state(power_state state)
{
  if (state != DO_NOTHING)
    orbis_syscall(37, 1, static_cast<int>(state));
}

void ring_buzzer(int type)
{
  sceKernelIccSetBuzzer(type);
}
