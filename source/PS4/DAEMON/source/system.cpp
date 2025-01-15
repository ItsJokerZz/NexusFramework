#include "../headers/includes.hpp"

namespace System
{
  std::string consoleType;

  void TextNotify(int type, const char *_msg)
  {
    std::string msg = _msg;
    sceSysUtilSendSystemNotificationWithText(type, msg.c_str());
  }

  void ImageNotify(const char *IconUri, const char *text)
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

  const char *Type()
  {
    int32_t cex = sceKernelIsCEX();
    int32_t devKit = sceKernelIsDevKit();
    int32_t testKit = sceKernelIsTestKit();

    if (cex)
      consoleType = "CEX";
    if (devKit)
      consoleType = "KIT";
    if (testKit)
      consoleType = "TEST";

    return consoleType.c_str();
  }

  int32_t GetSystemLanguageID()
  {
    int32_t languageID = -1;
    sceSystemServiceParamGetInt(ORBIS_SYSTEM_SERVICE_PARAM_ID_LANG, &languageID);
    return languageID;
  }

  const char *GetSystemLanguage()
  {
    static const char *languages[] = {
        "jp", "en-US", "fr", "es", "de", "it", "nl", "pt-PT",
        "ru", "ko", "zh-TW", "zh-CN", "fi", "sv", "da", "no",
        "pl", "pt-BR", "en-GB", "tr", "es-LA", "ar", "fr-CA", "cs",
        "hu", "el", "ro", "th", "vi", "id"};

    int32_t langID = GetSystemLanguageID();
    if (langID >= ORBIS_SYSTEM_PARAM_LANG_JAPANESE &&
        langID <= ORBIS_SYSTEM_PARAM_LANG_INDONESIAN)
    {
      return languages[langID];
    }
    return "NULL";
  }

  const char *GetFWVersion()
  {
    static char versionString[0x1C];
    OrbisKernelSwVersion versionInfo;
    if (sceKernelGetSystemSwVersion(&versionInfo) < 0)
      return NULL;
    strncpy(versionString, versionInfo.VersionString, 5);
    versionString[5] = '\0';
    return versionString;
  }

  uint32_t GetCPUTemperature()
  {
    uint32_t celsius;
    sceKernelGetCpuTemperature(&celsius);
    return celsius;
  }

  uint32_t GetSOCTemperature()
  {
    uint32_t celsius;
    sceKernelGetSocSensorTemperature(0, &celsius);
    return celsius;
  }

  void SetTemperatureLimit(uint8_t limit = 60)
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

  void Beep(int type)
  {
    sceKernelIccSetBuzzer(type);
  }
}
