#include "../headers/includes.hpp"

std::string console_type;

std::string get_local_ip()
{
  int sock = socket(PF_INET, SOCK_DGRAM, 0);
  if (sock == -1)
    return "";

  sockaddr_in remote_addr{};
  remote_addr.sin_family = AF_INET;
  remote_addr.sin_addr.s_addr = inet_addr("8.8.8.8");
  remote_addr.sin_port = htons(53);

  if (connect(sock, reinterpret_cast<sockaddr *>(&remote_addr), sizeof(remote_addr)) == -1)
  {
    close(sock);
    return "";
  }

  sockaddr_in local_addr{};
  socklen_t addrlen = sizeof(local_addr);
  if (getsockname(sock, reinterpret_cast<sockaddr *>(&local_addr), &addrlen) == -1)
  {
    close(sock);
    return "";
  }

  close(sock);

  char buf[INET_ADDRSTRLEN];
  if (inet_ntop(AF_INET, &local_addr.sin_addr, buf, INET_ADDRSTRLEN) == nullptr)
    return "";

  return std::string(buf);
}

std::string get_title_id()
{
  DIR *dir = opendir("/mnt/sandbox/");
  if (!dir)
    return HOME_MENU;

  std::string titleID = "";
  std::regex titleRegex("(?!NPXS)([a-zA-Z0-9]{4}[0-9]{5})");
  struct dirent *entry;
  while ((entry = readdir(dir)) != nullptr)
  {
    std::string dirName(entry->d_name);
    std::smatch match;
    if (std::regex_search(dirName, match, titleRegex) && match.size() > 1)
    {
      titleID = match.str(1);
      break;
    }
  }
  closedir(dir);

  return titleID.empty() ? HOME_MENU : titleID;
}

std::string get_title_name()
{
  std::string titleID = get_title_id();

  if (titleID == HOME_MENU)
    return "User Interface (UI)";

  std::string path = "/system_data/priv/appmeta/" + titleID + "/param.sfo";

  FILE *file = fopen(path.c_str(), "rb");
  if (!file)
  {
    log_message("Failed to open SFO file at %s", path.c_str());
    return "";
  }

  fseek(file, 0, SEEK_END);
  long fileSize = ftell(file);
  fseek(file, 0, SEEK_SET);

  std::vector<u8> sfoData(fileSize);
  fread(sfoData.data(), 1, fileSize, file);
  fclose(file);

  SfoReader sfoReader(sfoData);
  return sfoReader.GetValueFor<std::string>("TITLE");
}

std::string find_exec_by_titleID()
{
  std::string titleID = get_title_id();
  std::string path = "/mnt/sandbox/" + titleID + "_000/app0/";
  DIR *dir = opendir(path.c_str());
  if (!dir)
    return "";

  struct dirent *entry;
  while ((entry = readdir(dir)) != nullptr)
  {
    std::string fileName(entry->d_name);
    if (fileName.find(".bin") != std::string::npos ||
        fileName.find(".elf") != std::string::npos)
    {
      int pid = find_pid_by_procName(fileName.c_str());

      closedir(dir);

      return std::string(find_procName_of_pid(pid).c_str());
    }
  }

  closedir(dir);
  return "";
}

std::string get_game_info(const std::string &returnType)
{
  static const std::vector<std::string> validGameTypes = {
      "SLES", "SCES", "SCED", "SLUS", "SCUS", "SLPS", "SCAJ", "SLKA", "SLPM",
      "SCPS", "CF00", "SCKA", "ALCH", "CPCS", "SLAJ", "KOEI", "ARZE", "TCPS",
      "SCCS", "PAPX", "SRPM", "GUST", "WLFD", "ULKS", "VUGJ", "HAKU", "ROSE",
      "CZP2", "ARP2", "PKP2", "SLPN", "NMP2", "MTP2", "SCPM", "PBPX"};

  std::string titleID = get_title_id();
  std::string gameName = get_title_name();
  std::string executable = find_exec_by_titleID();
  std::string imagePath = titleID == HOME_MENU
                              ? ""
                              : "/user/appmeta/" + titleID + "/icon0.png";

  std::string imageFtp = titleID == HOME_MENU
                             ? ""
                             : "ftp://" + get_local_ip() + ":2121/user/appmeta/" + titleID + "/icon0.png";

  std::string gameType = (titleID.rfind("CUSA", 0) == 0)
                             ? "PS4"
                         : (std::find(validGameTypes.begin(),
                                      validGameTypes.end(),
                                      titleID.substr(0, 4)) != validGameTypes.end())
                             ? "PS1/PS2"
                             : "Homebrew";

  int pid = find_pid_by_procName(executable.c_str());
  if (returnType == "pid")
    return pid == -1 ? "" : std::to_string(pid);
  if (returnType == "titleId")
    return titleID;
  if (returnType == "name")
    return gameName;
  if (returnType == "exec")
    return executable;
  if (returnType == "imgPath")
    return imagePath;
  if (returnType == "imgFtp")
    return imagePath;
  if (returnType == "type")
    return gameType;

  return "";
}

std::string find_procName_of_pid(int pid)
{
  struct proc_list_entry *proc_list = nullptr;
  uint64_t pnum;

  if (get_proc_list(nullptr, &pnum) || !(proc_list = (struct proc_list_entry *)malloc(pnum * sizeof(struct proc_list_entry))))
    return ""; // Return an empty string on failure

  if (get_proc_list(proc_list, &pnum))
  {
    free(proc_list);
    return ""; // Return an empty string if unable to get the process list
  }

  for (size_t i = 0; i < pnum; ++i)
  {
    if (proc_list[i].pid == pid)
    {
      std::string proc_name(proc_list[i].p_comm, 32); // Create a string from the first 32 characters
      free(proc_list);
      return proc_name;
    }
  }

  free(proc_list);
  return ""; // Return an empty string if no matching process is found
}

int find_pid_by_procName(const char *proc_name)
{
  struct proc_list_entry *proc_list = nullptr;
  uint64_t pnum;

  if (get_proc_list(nullptr, &pnum) || !(proc_list = (struct proc_list_entry *)malloc(pnum * sizeof(struct proc_list_entry))))
    return -1; // Return -1 to indicate failure

  if (get_proc_list(proc_list, &pnum))
  {
    free(proc_list);
    return -1;
  }

  for (size_t i = 0; i < pnum; ++i)
  {
    if (strncmp(proc_list[i].p_comm, proc_name, 32) == 0)
    {
      int pid = proc_list[i].pid;
      free(proc_list);
      return pid; // Return the pid found
    }
  }

  free(proc_list);
  return -1; // Return -1 if no matching process is found
}

int get_proc_list(struct proc_list_entry *procs, uint64_t *num)
{
  return orbis_syscall(107 + 90, procs, num);
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
    if (sceUserServiceGetUserName(idList.userId[0], username,
                                  sizeof(username)) == 0)
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
  return "CEX";
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
  if (sceKernelPollEventFlag(flag, 0xFFFF, SCE_KERNEL_EVF_WAITMODE_OR,
                             &state) != 0)
    return false;

  return (state == 1000 &&
          sceKernelPollEventFlag(flag, 0x200000, SCE_KERNEL_EVF_WAITMODE_OR,
                                 0) == 0) ||
         state == 500;
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

  char data[10] = {0x00, 0x00, 0x00, 0x00, 0x00, static_cast<char>(limit),
                   0x00, 0x00, 0x00, 0x00};
  ioctl(fd, 0xC01C8F07, data);
  close(fd);
}

void set_power_state(power_state state)
{
  if (state != DO_NOTHING)
    orbis_syscall(37, 1, static_cast<int>(state));
}

void ring_buzzer(int type) { sceKernelIccSetBuzzer(type); }
