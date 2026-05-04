#pragma once

std::string string_to_hex(const uint8_t *src, size_t len);

std::string get_filename_from_path(const std::string &full_path);

bool path_exists(const char *path);

void write_file(const std::filesystem::path &path, std::string_view content,
                bool overwrite = false);

std::string parse_param_file(std::string_view key);

void log_message(const char *fmt, ...);

void notify_debug(const char *fmt, ...);
void notify_debug(const char *icon, const char *fmt, ...);

enum class disk_info : uint8_t
{
  PERCENT_USED,
  TOTAL_SPACE,
  USED_SPACE,
  FREE_SPACE
};

struct system_info
{
  struct init_info
  {
    const bool is_ps5 =
        path_exists("/system/common/lib/libSceNotification.sprx");
    const uint32_t firmware_int = init_firmware_int();
    const std::string firmware_str = init_firmware_str();
    const std::string hen_name = init_hen_name(is_ps5);
    const std::string model = init_model();
    const std::string type = init_type();
    const std::string name = init_system_name();
    const std::string psid = init_psid();
    const std::string idps = init_idps();

    static uint32_t init_firmware_int()
    {
      kernel_sw_info kinfo{};
#ifdef __PROSPERO__
      sceKernelGetProsperoSystemSwVersion(&kinfo);
#else
      sceKernelGetSystemSwVersion(&kinfo);
#endif
      return kinfo.version;
    }

    static std::string init_firmware_str()
    {
      kernel_sw_info kinfo{};
#ifdef __PROSPERO__
      sceKernelGetProsperoSystemSwVersion(&kinfo);
#else
      sceKernelGetSystemSwVersion(&kinfo);
#endif
      return std::string(kinfo.version_string).substr(0, 5);
    }

    static std::string init_hen_name(bool ps5)
    {
      std::string hen = "N/A";
      if (!ps5)
      {
        pid_t pid = find_pid_by_exec("SceShellUI");
        proc_vm_maps maps = get_vm_maps(pid);
        for (const auto &entry : maps.entries)
          if (std::string(entry.name).find("GoldHENLoader") !=
              std::string::npos)
          {
            hen = "GoldHEN";
            break;
          }
        if (hen == "N/A" && path_exists("/user/temp/hen.installed"))
          hen = "PS4HEN";
      }
      else
      {
        if (find_pid_by_exec("etaHEN Utility Daemon") > 0)
          hen = "etaHEN";
        else if (find_pid_by_exec("PS5HEN Daemon") > 0)
          hen = "PS5HEN";
      }
      return hen;
    }

    static std::string init_type()
    {
      if (sceKernelIsDevKit())
        return "DEX";
      else if (sceKernelIsTestKit() &&
               path_exists("/system/priv/lib/libSceDeci5Ttyp.sprx"))
        return "TEST";
      else
        return "CEX";
    }

    static std::string init_model()
    {
      char s[10]{};
      sceKernelGetHwModelName(s);
      return std::string(s).substr(0, 9);
    }

    static std::string init_system_name()
    {
      char buf[65]{};
      sceSystemServiceParamGetString(SYSTEM_SERVICE_PARAM_ID_SYSTEM_NAME, buf,
                                     sizeof(buf));
      return buf;
    }

    static std::string init_psid()
    {
      uint8_t _psid[17]{0};
      sceKernelGetOpenPsIdForSystem(_psid);
      return string_to_hex(_psid, 16);
    }

    static std::string init_idps()
    {
      uint8_t _idps[17]{0};
      sceKernelGetIdPs(_idps);
      return string_to_hex(_idps, 16);
    }
  };

  const init_info init;

  bool is_ps5() const { return init.is_ps5; }
  uint32_t firmware_int() const { return init.firmware_int; }
  const std::string &firmware_str() const { return init.firmware_str; }
  const std::string &hen_name() const { return init.hen_name; }
  const std::string &model() const { return init.model; }
  const std::string &type() const { return init.type; }
  const std::string &name() const { return init.name; }
  const std::string &psid() const { return init.psid; }
  const std::string &idps() const { return init.idps; }

  uint32_t get_temperature(bool soc) const
  {
    int32_t temp = 0;
    if (soc)
      sceKernelGetSocSensorTemperature(0, &temp);
    else
      sceKernelGetCpuTemperature(&temp);
    return temp;
  }

  std::pair<uint32_t, uint32_t> get_temperatures() const
  {
    return {get_temperature(true), get_temperature(false)};
  }

  std::string get_uptime() const
  {
    struct timespec ts{};
    sceKernelClockGettime(SCE_MONOTONIC, &ts);
    uint64_t total = ts.tv_sec;
    uint64_t d = total / 86400;
    uint64_t h = (total % 86400) / 3600;
    uint64_t m = (total % 3600) / 60;
    uint64_t s = total % 60;
    char buf[64];
    std::snprintf(buf, sizeof(buf), "%luD %luH %luM %luS", d, h, m, s);
    return buf;
  }

  std::string get_disk_info(disk_info type,
                            std::string_view path = "/user") const
  {
    struct statfs s{};
    if (statfs(path.data(), &s) != 0)
      return {};
    uint64_t total = s.f_blocks * static_cast<uint64_t>(s.f_bsize);
    uint64_t free_space = s.f_bavail * static_cast<uint64_t>(s.f_bsize);
    uint64_t used = total - free_space;
    long percent =
        total ? static_cast<long>((used * 100 + total / 2) / total) : 0;
    auto format_size = [](uint64_t bytes) -> std::string
    {
      constexpr const char *units[] = {"B", "KB", "MB", "GB", "TB"};
      int idx = 0;
      double size = static_cast<double>(bytes);
      while (size >= 1024.0 && idx < 4)
      {
        size /= 1024.0;
        ++idx;
      }
      char buf[32];
      std::snprintf(buf, sizeof(buf), "%.2f %s", size, units[idx]);
      return buf;
    };
    switch (type)
    {
    case disk_info::PERCENT_USED:
      return std::to_string(percent);
    case disk_info::TOTAL_SPACE:
      return format_size(total);
    case disk_info::USED_SPACE:
      return format_size(used);
    case disk_info::FREE_SPACE:
      return format_size(free_space);
    }
    return {};
  }

  std::string get_username() const
  {
    int32_t user_id = 0;
    char _username[USER_SERVICE_MAX_USER_NAME_LENGTH + 1]{};
    sceUserServiceGetForegroundUser(&user_id);
    if (user_id > 0)
      sceUserServiceGetUserName(user_id, _username, sizeof(_username));
    std::string username(_username);
    return username.empty() ? "UNKNOWN" : username;
  }

  std::string get_hw_serial() const
  {
    char buf[128]{};
    sceKernelGetHwSerialNumber(buf);
    return buf;
  }

  std::string get_cpu_frequency() const
  {
    uint64_t freq = static_cast<uint64_t>(
        static_cast<uint32_t>(sceKernelGetCpuFrequency()));
    char buf[16];
    std::snprintf(buf, sizeof(buf), "%.2f", freq / 1'000'000'000.0);
    return buf;
  }

  std::string get_ip_address() const
  {
    SceNetCtlInfo info{};
    char buf[32]{};
    if (sceNetCtlGetInfo(NET_CTL_INFO_IP_ADDRESS, &info) < 0)
      std::snprintf(buf, sizeof(buf), "IP NOT FOUND");
    else
      std::snprintf(buf, sizeof(buf), "%s", info.ip_address);
    return buf;
  }

  std::string get_mac_string() const
  {
    SceNetCtlInfo info{};
    char buf[32]{};
    if (sceNetCtlGetInfo(NET_CTL_INFO_ETHER_ADDR, &info) < 0)
      std::snprintf(buf, sizeof(buf), "MAC NOT FOUND");
    else
      std::snprintf(buf, sizeof(buf), "%02X:%02X:%02X:%02X:%02X:%02X",
                    info.ether_addr.data[0], info.ether_addr.data[1],
                    info.ether_addr.data[2], info.ether_addr.data[3],
                    info.ether_addr.data[4], info.ether_addr.data[5]);
    return buf;
  }

  void summary() const
  {
    log_message("=== System Summary ===");

    log_message("IP Address : %s", get_ip_address().c_str());
    log_message("MAC Address : %s", get_mac_string().c_str());

    log_message("Console : %s", is_ps5() ? "PS5" : "PS4");
    log_message("Firmware : %s (0x%08X)", firmware_str().c_str(),
                firmware_int());
    log_message("HEN : %s", hen_name().c_str());

    log_message("HW Serial : %s", get_hw_serial().c_str());
    log_message("Model : %s", model().c_str());
    log_message("Type : %s", type().c_str());
    log_message("Name : %s", name().c_str());

    log_message("PSID : %s", psid().c_str());
    log_message("IDPS : %s", idps().c_str());

    log_message("Username : %s", get_username().c_str());

    log_message("CPU Frequency : %s", get_cpu_frequency().c_str());

    auto [soc, cpu] = get_temperatures();
    log_message("Temperatures : SOC %u°C | CPU %u°C", soc, cpu);

    log_message("Uptime : %s", get_uptime().c_str());

    struct DiskEntry
    {
      disk_info type;
      const char *label;
    };
    DiskEntry disks[] = {{disk_info::TOTAL_SPACE, "Total"},
                         {disk_info::USED_SPACE, "Used"},
                         {disk_info::FREE_SPACE, "Free"},
                         {disk_info::PERCENT_USED, "Usage"}};
    for (const auto &d : disks)
      log_message("Disk %s : %s", d.label, get_disk_info(d.type).c_str());
    log_message("=======================");
  }
};

inline const system_info g_system_info{};

void ring_buzzer(int type);
