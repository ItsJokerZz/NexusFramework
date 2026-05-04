#include "headers.hpp"

std::vector<process_info> get_proc_list() {
  int mib[4] = {CTL_KERN, KERN_PROC, KERN_PROC_PROC, 0};
  size_t buf_size;

  if (sysctl(mib, 4, NULL, &buf_size, NULL, 0) || !buf_size)
    return {};

  std::vector<uint8_t> buf(buf_size);
  if (sysctl(mib, 4, buf.data(), &buf_size, NULL, 0))
    return {};

  std::vector<process_info> list;
  for (uint8_t *ptr = buf.data(); ptr < buf.data() + buf_size;) {
    kinfo_proc *ki = (kinfo_proc *)ptr;
    if (ki->ki_structsize <= 0)
      break;

    ptr += ki->ki_structsize;

    app_info appinfo = {};
    sceKernelGetAppInfo(ki->ki_pid, &appinfo);

    char safe_tid[11] = {0};
    memcpy(safe_tid, appinfo.TitleId, sizeof(appinfo.TitleId));

    process_info info;
    info.process_id = ki->ki_pid;
    info.app_id = appinfo.AppId;
    info.tid = safe_tid;
    info.exec = ki->ki_comm;
    list.push_back(info);
  }

  return list;
}

pid_t find_pid_by_exec(const char *exec) {
  return get_proc_info(exec, &process_info::exec, &process_info::process_id,
                       -1);
}

int get_running_app_id() {
#ifdef __PROSPERO__
  int appId = sceSystemServiceGetAppIdOfRunningBigApp();
#else
  int appId = sceSystemServiceGetAppIdOfBigApp();
#endif

  return (appId < 0) ? get_proc_info(SHELLUI_TID, &process_info::tid,
                                     &process_info::app_id, -1)
                     : appId;
}

std::string get_running_app_tid() {
#ifdef __PROSPERO__
  int appId = sceSystemServiceGetAppIdOfRunningBigApp();
#else
  int appId = sceSystemServiceGetAppIdOfBigApp();
#endif

  if (appId < 0)
    return SHELLUI_TID;

  std::string tid =
      get_proc_info(appId, &process_info::app_id, &process_info::tid, "");
  return tid.empty() ? SHELLUI_TID : tid;
}

pid_t get_running_app_pid() {
  std::string tid = get_running_app_tid();
  pid_t pid = get_proc_info(tid.c_str(), &process_info::tid,
                            &process_info::process_id, -1);

  if (pid < 0)
    pid = find_pid_by_exec("SceShellUI");

  return pid;
}

std::string get_running_app_exec() {
  pid_t pid = get_running_app_pid();
  std::string name =
      get_proc_info(pid, &process_info::process_id, &process_info::exec, "");
  return name.empty() ? "" : name;
}

std::string get_name_by_tid(std::string_view tid) {
  std::string _tid;
  if (tid.empty()) {
    _tid = get_running_app_tid();
    tid = _tid;
  }

  if (tid == SHELLUI_TID)
    return "User Interface (UI)";

  std::string ps4_name = parse_param_file("TITLE");

#ifdef __PROSPERO__
  std::string ps5_name = parse_param_file("titleName");
  return ps5_name.empty() ? ps4_name : ps5_name;
#else
  return ps4_name;
#endif
}

std::string get_app_version() {
  std::string ps4_ver = parse_param_file("VERSION");

#ifdef __PROSPERO__
  std::string ps5_ver = parse_param_file("masterVersion");
  return ps5_ver.empty() ? ps4_ver : ps5_ver;
#else
  return ps4_ver;
#endif
}

std::string get_app_sdk() {
  std::string sdk_str;

#ifdef __PROSPERO__
  sdk_str = parse_param_file("requiredSystemSoftwareVersion");
#endif

  if (sdk_str.empty())
    sdk_str = parse_param_file("SYSTEM_VER");

  if (sdk_str.empty())
    return "";

  if (sdk_str.rfind("0x", 0) == 0 || sdk_str.rfind("0X", 0) == 0) {
    sdk_str = sdk_str.substr(2);

    if (sdk_str.size() < 4)
      return "";

    char buf[8];
    std::snprintf(buf, sizeof(buf), "%.2s.%.2s", sdk_str.c_str(),
                  sdk_str.c_str() + 2);
    return buf;
  }

  unsigned long value = 0;
  try {
    value = std::stoul(sdk_str);
  } catch (...) {
    return "";
  }

  int major = (value >> 24) & 0xFF;
  int minor = (value >> 16) & 0xFF;

  char buf[8];
  std::snprintf(buf, sizeof(buf), "%02d.%02d", major, minor);
  return buf;
}

std::string get_app_region() {
  char content_id[10];
  std::string content_id_str;

#ifdef __PROSPERO__
  content_id_str = parse_param_file("contentId");
#endif

  if (content_id_str.empty())
    content_id_str = parse_param_file("CONTENT_ID");

  if (content_id_str.empty())
    return "";

  std::regex regex("^(\\w{2})\\d+");
  std::smatch match;

  if (std::regex_search(content_id_str, match, regex) && match.size() > 1) {
    std::string prefix = match.str(1);
    if (prefix == "UP")
      return "USA";
    if (prefix == "JP")
      return "JAP";
    if (prefix == "AS")
      return "ASIA";
    if (prefix == "EP")
      return "EUR";
  }

  return "";
}

bool is_daemon_process() {
  return get_running_app_tid() == SHELLUI_TID ||
         parse_param_file("CATEGORY") == "gdd";
}