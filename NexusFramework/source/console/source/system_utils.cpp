#include "headers.hpp"

std::string string_to_hex(const uint8_t *src, size_t len)
{
  static constexpr char hex_chars[] = "0123456789ABCDEF";
  std::string result;
  result.reserve(len * 2);

  for (size_t i = 0; i < len; i++)
  {
    result.push_back(hex_chars[src[i] >> 4]);
    result.push_back(hex_chars[src[i] & 0x0F]);
  }

  return result;
}

std::string get_filename_from_path(const std::string &full_path)
{
  const size_t last_slash = full_path.find_last_of("/\\");
  return (last_slash == std::string::npos) ? full_path
                                           : full_path.substr(last_slash + 1);
}

bool path_exists(const char *path) { return std::filesystem::exists(path); }

void write_file(const std::filesystem::path &path, std::string_view content,
                bool overwrite)
{
  if (std::ofstream file{path, std::ios::binary | (overwrite ? std::ios::trunc
                                                             : std::ios::app)})
    file.write(content.data(), content.size());
}

#ifdef __PROSPERO__
[[maybe_unused]]
static std::string rtrim_copy(const std::string &s)
{
  size_t end = s.find_last_not_of(" \t\r\n");
  return end == std::string::npos ? "" : s.substr(0, end + 1);
}

[[maybe_unused]]
static const json *json_get_dotted(const json &j, std::string_view dotted)
{
  const json *cur = &j;
  size_t start = 0;
  while (start <= dotted.size())
  {
    size_t dot = dotted.find('.', start);
    std::string key = dot == std::string::npos
                          ? std::string(dotted.substr(start))
                          : std::string(dotted.substr(start, dot - start));
    if (!cur->is_object())
      return nullptr;
    auto it = cur->find(key);
    if (it == cur->end())
      return nullptr;
    cur = &(*it);
    if (dot == std::string::npos)
      break;
    start = dot + 1;
  }
  return cur;
}

[[maybe_unused]]
static bool json_find_key_anywhere(const json &j, std::string_view key,
                                   const json *&out)
{
  if (j.is_object())
  {
    for (auto it = j.begin(); it != j.end(); ++it)
    {
      if (it.key() == key)
      {
        out = &(*it);
        return true;
      }
      if (json_find_key_anywhere(it.value(), key, out))
        return true;
    }
    return false;
  }
  if (j.is_array())
  {
    for (const auto &el : j)
      if (json_find_key_anywhere(el, key, out))
        return true;
    return false;
  }
  return false;
}

[[maybe_unused]]
static std::string json_to_string_trim(const json &v)
{
  if (v.is_string())
    return rtrim_copy(v.get<std::string>());
  return v.dump();
}
#endif

std::string parse_param_file(std::string_view key)
{
  std::string titleId = get_running_app_tid();

  std::vector<std::string> sfo_paths = {
      "/system_data/priv/appmeta/" + titleId + "/param.sfo",
      "/system/vsh/app/" + titleId + "/sce_sys/param.sfo",
      "/system_ex/app/" + titleId + "/sce_sys/param.sfo",
      "/user/appmeta/" + titleId + "/param.sfo"};

  for (const auto &path : sfo_paths)
  {
    FILE *file = fopen(path.c_str(), "rb");
    if (!file)
      continue;

    fseek(file, 0, SEEK_END);
    long size = ftell(file);
    fseek(file, 0, SEEK_SET);

    if (size <= 0)
    {
      fclose(file);
      continue;
    }

    std::vector<u8> buf(size);
    fread(buf.data(), 1, size, file);
    fclose(file);

    SfoReader sfo(buf);

    std::string strValue = sfo.GetValueFor<std::string>(key.data());
    if (!strValue.empty())
      return strValue;

    uint32_t intValue = sfo.GetValueFor<uint32_t>(key.data());
    if (intValue != 0)
      return std::to_string(intValue);
  }

#ifdef __PROSPERO__
  std::vector<std::string> json_paths = {
      "/system_data/priv/appmeta/" + titleId + "/param.json",
      "/system/vsh/app/" + titleId + "/sce_sys/param.json",
      "/system_ex/app/" + titleId + "/sce_sys/param.json",
      "/user/appmeta/" + titleId + "/param.json"};

  for (const auto &path : json_paths)
  {
    FILE *file = fopen(path.c_str(), "rb");
    if (!file)
      continue;

    fseek(file, 0, SEEK_END);
    long size = ftell(file);
    fseek(file, 0, SEEK_SET);

    if (size <= 0)
    {
      fclose(file);
      continue;
    }

    std::string data(size, '\0');
    fread(data.data(), 1, size, file);
    fclose(file);

    json j = json::parse(data, nullptr, false);
    if (j.is_discarded())
      continue;

    const json *hit = nullptr;

    if (key.find('.') != std::string_view::npos)
    {
      const json *node = json_get_dotted(j, key);
      if (node)
        return json_to_string_trim(*node);
    }

    if (json_find_key_anywhere(j, key, hit))
      return json_to_string_trim(*hit);
  }
#endif

  return "";
}

void log_message(const char *fmt, ...)
{
  va_list args;
  va_start(args, fmt);
  int len = std::vsnprintf(nullptr, 0, fmt, args);
  va_end(args);

  if (len <= 0)
    return;

  std::string buf(len + 2, '\0');
  va_start(args, fmt);
  std::vsnprintf(buf.data(), len + 1, fmt, args);
  va_end(args);

  buf[len] = '\r';
  buf[len + 1] = '\n';

  klog_printf("%s", buf.c_str());
  write_file(LOG_FILE, buf);
}

void notify_debug(const char *fmt, ...)
{
  notify_request buffer{};

  va_list args;
  va_start(args, fmt);
  vsprintf(buffer.message, fmt, args);
  va_end(args);

  sceKernelSendNotificationRequest(0, &buffer, sizeof(notify_request), false);
}

void notify_debug(const char *icon, const char *fmt, ...)
{
#ifdef __PROSPERO__
  const char *iconUri = icon;
  const char *mainMsg = "";
  const char *actionText = "";
  const char *actionUri = "";

  char subMsg[1024];

  UNUSED(iconUri);
  UNUSED(mainMsg);
  UNUSED(actionText);
  UNUSED(actionUri);

  va_list args;
  va_start(args, fmt);
  vsnprintf(subMsg, sizeof(subMsg), fmt, args);
  va_end(args);

  struct timeval tv;
  gettimeofday(&tv, nullptr);

  struct tm tm_now;
  gmtime_r(&tv.tv_sec, &tm_now);

  int ms_now = (tv.tv_usec + 500) / 1000;
  if (ms_now == 1000)
  {
    ms_now = 0;
    tm_now.tm_sec++;
  }

  char current_time[25];
  snprintf(current_time, sizeof(current_time),
           "%04d-%02d-%02dT%02d:%02d:%02d.%03dZ", tm_now.tm_year + 1900,
           tm_now.tm_mon + 1, tm_now.tm_mday, tm_now.tm_hour, tm_now.tm_min,
           tm_now.tm_sec, ms_now);

  tm_now.tm_sec += 30;
  time_t normalized = timegm(&tm_now);
  gmtime_r(&normalized, &tm_now);

  char expire_time[25];
  snprintf(expire_time, sizeof(expire_time),
           "%04d-%02d-%02dT%02d:%02d:%02d.%03dZ", tm_now.tm_year + 1900,
           tm_now.tm_mon + 1, tm_now.tm_mday, tm_now.tm_hour, tm_now.tm_min,
           tm_now.tm_sec, ms_now);

  nlohmann::json j;

  nlohmann::json &raw = j["rawData"];
  raw["viewTemplateType"] = "InteractiveToastTemplateB";
  raw["channelType"] = "Downloads";
  raw["useCaseId"] = nullptr;
  raw["toastOverwriteType"] = "No";
  raw["isImmediate"] = true;
  raw["priority"] = 100;

  nlohmann::json &viewData = raw["viewData"];
  viewData["icon"] = {{"type", "Url"}, {"parameters", {{"url", iconUri}}}};
  viewData["message"]["body"] = mainMsg;
  viewData["subMessage"]["body"] = subMsg;

  nlohmann::json &action = viewData["actions"].emplace_back();
  action["actionName"] = actionText;
  action["actionType"] = "DeepLink";
  action["defaultFocus"] = true;
  action["parameters"]["actionUrl"] = actionUri;

  nlohmann::json &platformViews =
      raw["platformViews"]["previewDisabled"]["viewData"];

  platformViews["icon"] = {{"type", "Predefined"},
                           {"parameters", {{"icon", "download"}}}};

  platformViews["message"]["body"] = std::string(NAME) + " Notification";

  std::string identifier =
      std::to_string(0x05F5E100 + arc4random_uniform(0x3B9AC9FF));

  j["createdDateTime"] = current_time;
  j["updatedDateTime"] = current_time;
  j["expirationDateTime"] = expire_time;
  j["localNotificationId"] = identifier;

  sceNotificationSend(SCE_NOTIFICATION_LOCAL_USER_ID_SYSTEM, true,
                      j.dump().c_str());
#else
  notify_request buffer{};

  va_list args;
  va_start(args, fmt);
  vsprintf(buffer.message, fmt, args);
  va_end(args);

  buffer.use_icon = 1;
  strcpy(buffer.icon_uri, icon);

  sceKernelSendNotificationRequest(0, &buffer, sizeof(notify_request), false);
#endif
}

void ring_buzzer(int type) { sceKernelIccSetBuzzer(type); }
