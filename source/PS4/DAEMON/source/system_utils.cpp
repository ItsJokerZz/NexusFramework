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

    if (connect(sock, reinterpret_cast<sockaddr *>(&remote_addr),
                sizeof(remote_addr)) == -1)
    {
        close(sock);
        return "";
    }

    sockaddr_in local_addr{};
    socklen_t addrlen = sizeof(local_addr);
    if (getsockname(sock, reinterpret_cast<sockaddr *>(&local_addr), &addrlen) ==
        -1)
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

std::string get_apps_titleid()
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

std::string get_apps_name()
{
    std::string titleID = get_apps_titleid();

    if (titleID == HOME_MENU)
        return "User Interface (UI)";

    return parse_apps_sfo_param("TITLE");
}

std::string get_apps_version() { return parse_apps_sfo_param("VERSION"); }

std::string get_apps_minFW()
{
    char versionString[10];
    std::string app_info = parse_apps_sfo_param("PUBTOOLINFO");
    std::regex regex("sdk_ver=(\\d{8})");
    std::smatch match;

    if (std::regex_search(app_info, match, regex) && match.size() > 1)
    {
        std::string sdk_version = match.str(1);
        int major = std::stoi(sdk_version.substr(0, 2));
        int minor = std::stoi(sdk_version.substr(2, 2));

        snprintf(versionString, sizeof(versionString), "%02d.%02d", major, minor);

        return std::string(versionString);
    }

    return "";
}

std::string get_apps_region()
{
    char content_id[10];
    std::string app_info = parse_apps_sfo_param("CONTENT_ID");
    std::regex regex("^(\\w{2})\\d+");
    std::smatch match;

    if (std::regex_search(app_info, match, regex) && match.size() > 1)
    {
        std::string prefix = match.str(1);
        std::string region;
        if (prefix == "UP")
            region = "USA";
        else if (prefix == "JP")
            region = "JAP";
        else if (prefix == "AS")
            region = "ASIA";
        else if (prefix == "EP")
            region = "EUR";

        return region;
    }

    return ""; // Return empty string if no match is found
}

std::string get_app_info(const std::string &returnType)
{
    // add enum instead of string for arg

    std::regex ps2Pattern("^(SLES|SCES|SCED|SLUS|SCUS|SLPS|SCAJ|SLKA|SLPM|SCPS|"
                          "CF00|SCKA|ALCH|CPCS|SLAJ|KOEI|ARZE|TCPS"
                          "|SCCS|PAPX|SRPM|GUST|WLFD|ULKS|VUGJ|HAKU|ROSE|CZP2|"
                          "ARP2|PKP2|SLPN|NMP2|MTP2|SCPM|PBPX)\\d*$");

    std::string titleID = get_apps_titleid();
    bool is_home = (titleID == HOME_MENU);
    std::string exec = is_home ? "SceShellUI" : find_exec_by_titleID();
    std::string pid = std::to_string(find_pid_by_procName(exec.c_str()));

    std::string type =
        (titleID.rfind("CUSA", 0) == 0)
            ? "PS4"
            : (std::regex_match(titleID, ps2Pattern) ? "PS1/PS2" : "Homebrew");

    const std::unordered_map<std::string, std::function<std::string()>>
        resultMap = {
            {"pid", [&]()
             { return pid; }},
            {"titleID", [&]()
             { return titleID; }},
            {"name", [&]()
             { return get_apps_name(); }},
            {"region", [&]()
             { return is_home ? "" : get_apps_region(); }},
            {"exec", [&]()
             { return is_home ? (exec + " (eboot.bin)") : exec; }},
            {"version", [&]()
             { return is_home ? "" : get_apps_version(); }},
            {"minFW", [&]()
             { return is_home ? "" : get_apps_minFW(); }},
            {"type", [&]()
             { return is_home ? "" : type; }},
            {"image", [&]()
             {
                 return is_home ? "" : "/user/appmeta/" + titleID + "/icon0.png";
             }}};

    auto it = resultMap.find(returnType);
    return (it != resultMap.end()) ? it->second() : "";
}

std::string parse_apps_sfo_param(const std::string &key)
{
    std::string titleID = get_apps_titleid();

    if (titleID == HOME_MENU)
        return "";

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
    return sfoReader.GetValueFor<std::string>(key);
}

std::string find_exec_by_titleID()
{
    std::string titleID = get_apps_titleid();
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

std::string find_procName_of_pid(int pid)
{
    struct proc_list_entry *proc_list = nullptr;
    uint64_t pnum;

    if (get_proc_list(nullptr, &pnum) ||
        !(proc_list = (struct proc_list_entry *)malloc(
              pnum * sizeof(struct proc_list_entry))))
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
            std::string proc_name(proc_list[i].p_comm,
                                  32); // Create a string from the first 32 characters
            free(proc_list);
            return proc_name;
        }
    }

    free(proc_list);
    return ""; // Return an empty string if no matching process is found
}

std::string get_console_name()
{
    char buffer[65]{};
    int ret = sceSystemServiceParamGetString(
        ORBIS_SYSTEM_SERVICE_PARAM_ID_SYSTEM_NAME, buffer, sizeof(buffer));
    return std::string(buffer);
}

std::string get_disk_info(std::string infoType)
{
    struct statfs s;

    const char *MOUNT_POINT = "/user";

    long blocks_used = 0, percentUsed = -1;
    double totalSpace = 0, usedSpace = 0, freeSpace = 0;

    if (orbis_syscall(396, MOUNT_POINT, &s) != 0)
    {
        log_message("Cannot open %s", MOUNT_POINT);
        return "";
    }

    if (s.f_blocks > 0)
    {
        blocks_used = s.f_blocks - s.f_bfree;
        percentUsed = static_cast<long>(
            blocks_used * 100.0 / (blocks_used + s.f_bavail) + 0.5);
    }

    totalSpace = s.f_blocks * (s.f_bsize / 1024.0 / 1024.0 / 1024.0);
    freeSpace = s.f_bavail * (s.f_bsize / 1024.0 / 1024.0 / 1024.0);
    usedSpace = totalSpace - freeSpace;

    auto formatSize = [](double size) -> std::string
    {
        std::string unit = "GB";
        double displaySize = size;

        if (size >= 1024.0)
        {
            unit = "TB";
            displaySize = size / 1024.0;
        }
        else if (size < 1.0)
        {
            unit = "MB";
            displaySize = size * 1024.0;
        }

        int integerPart = static_cast<int>(displaySize);
        int fractionalPartRounded =
            static_cast<int>((displaySize - integerPart) * 100.0 + 0.5);

        return std::to_string(integerPart) + "." +
               (fractionalPartRounded < 10 ? "0" : "") +
               std::to_string(fractionalPartRounded) + " " + unit;
    };

    if (infoType == "percentUsed")
        return std::to_string(percentUsed) + "%";
    if (infoType == "totalSpace")
        return formatSize(totalSpace);
    if (infoType == "usedSpace")
        return formatSize(usedSpace);
    if (infoType == "freeSpace")
        return formatSize(freeSpace);

    return "";
}

std::string get_console_type()
{
    if (sceKernelIsDevKit())
        return "KIT";
    if (sceKernelIsTestKit())
        return "TEST";
    return "CEX";
}

std::string get_fw_version()
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

std::string get_username(OrbisUserServiceUserId userId)
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

int find_pid_by_procName(std::string proc_name)
{
    struct proc_list_entry *proc_list = nullptr;
    uint64_t pnum;

    if (get_proc_list(nullptr, &pnum) ||
        !(proc_list = (struct proc_list_entry *)malloc(
              pnum * sizeof(struct proc_list_entry))))
        return -1; // Return -1 to indicate failure

    if (get_proc_list(proc_list, &pnum))
    {
        free(proc_list);
        return -1;
    }

    for (size_t i = 0; i < pnum; ++i)
    {
        if (strncmp(proc_list[i].p_comm, proc_name.c_str(), 32) == 0)
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

int sys_proc_cmd(uint64_t pid, uint64_t cmd, void *data)
{
    return orbis_syscall(109 + 90, pid, cmd, data);
}

int sys_proc_alloc(uint64_t pid, free_and_alloc_args *args, bool free)
{
    if (free)
        return sys_proc_cmd(pid, SYS_PROC_FREE, args); // Pass the pointer directly
    else
        return sys_proc_cmd(pid, SYS_PROC_ALLOC, args); // Pass the pointer directly
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

void ring_buzzer(int type) { sceKernelIccSetBuzzer(type); }

void set_temperature_limit(uint8_t limit)
{
    if (limit < 50)
        limit = 50;

    if (limit > 80)
        limit = 80;

    int fd = open("/dev/icc_fan", O_RDONLY, 0);
    if (fd == 0)
    {
        char data[10] = {
            0x00, 0x00, 0x00, 0x00, 0x00,
            static_cast<char>(limit),
            0x00, 0x00, 0x00, 0x00};


        ioctl(fd, 0xC01C8F07, data);

        close(fd);
    }
}


void set_power_state(power_state state)
{
    if (state != DO_NOTHING)
        orbis_syscall(37, 1, static_cast<int>(state));
}
