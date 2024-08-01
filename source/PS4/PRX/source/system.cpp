#include "system.hpp"

extern "C" {
namespace System {
void PrintToConsole(const char* message, int type) {
  static const char* prefixes[] = {"[DEBUG] ",   "[CRITICAL] ", "[ERROR] ",
                                   "[WARNING] ", "[INFO] ",     "[VERBOSE] "};
  std::string logMessage = prefixes[type] + std::string(message) + "\n";
  sceKernelDebugOutText(type, logMessage.c_str());
}

void TextNotify(int type, const char* _msg) {
  std::string msg = _msg;
  sceSysUtilSendSystemNotificationWithText(type, msg.c_str());
}

void ImageNotify(const char* IconUri, const char* text) {
  if (IconUri == NULL) IconUri = "cxml://psnotification/tex_icon_system";
  
    OrbisNotificationRequest Buffer;
    Buffer.type = NotificationRequest;
    Buffer.unk3 = 0;
    Buffer.useIconImageUri = 1;
    Buffer.targetId = -1;
    strcpy(Buffer.message, text);
    strcpy(Buffer.iconUri, IconUri);
    sceKernelSendNotificationRequest(0, &Buffer, sizeof(Buffer), 0);
}

const char* Type() {
  int32_t cex = sceKernelIsCEX();
  int32_t devKit = sceKernelIsDevKit();
  int32_t testKit = sceKernelIsTestKit();

  if (cex) consoleType = "CEX";
  if (devKit) consoleType = "KIT";
  if (testKit) consoleType = "TEST";

  return consoleType.c_str();
}

void MountRootDirectories() {
  static const char* devices[] = {"/dev/da0x0.crypt", "/dev/da0x1.crypt",
                                  "/dev/da0x4.crypt", "/dev/da0x5.crypt"};
  static const char* mount_points[] = {"/preinst", "/preinst2", "/system",
                                       "/system_ex"};
  for (int i = 0; i < 4; ++i) {
    mount_large_fs(devices[i], mount_points[i], "exfatfs", "511",
                   0x0000000000010000ULL);
  }
}

int32_t GetSystemLanguageID() {
  int32_t languageID = -1;
  sceSystemServiceParamGetInt(ORBIS_SYSTEM_SERVICE_PARAM_ID_LANG, &languageID);
  return languageID;
}

const char* GetSystemLanguage() {
  static const char* languages[] = {
      "jp", "en-US", "fr",    "es",    "de",    "it", "nl",    "pt-PT",
      "ru", "ko",    "zh-TW", "zh-CN", "fi",    "sv", "da",    "no",
      "pl", "pt-BR", "en-GB", "tr",    "es-LA", "ar", "fr-CA", "cs",
      "hu", "el",    "ro",    "th",    "vi",    "id"};

  int32_t langID = GetSystemLanguageID();
  if (langID >= ORBIS_SYSTEM_PARAM_LANG_JAPANESE &&
      langID <= ORBIS_SYSTEM_PARAM_LANG_INDONESIAN) {
    return languages[langID];
  }
  return "NULL";
}

const char* GetFWVersion() {
  static char versionString[0x1C];
  OrbisKernelSwVersion versionInfo;
  if (sceKernelGetSystemSwVersion(&versionInfo) < 0) return NULL;
  strncpy(versionString, versionInfo.VersionString, 5);
  versionString[5] = '\0';
  return versionString;
}

uint32_t GetCPUTemperature() {
  uint32_t celsius;
  sceKernelGetCpuTemperature(&celsius);
  return celsius;
}

uint32_t GetSOCTemperature() {
  uint32_t celsius;
  sceKernelGetSocSensorTemperature(0, &celsius);
  return celsius;
}

void SetTemperatureLimit(uint8_t limit = 60) {
  int fd = open("/dev/icc_fan", O_RDONLY);
  if (fd < 0) return;
  char data[10] = {0x00, 0x00, 0x00, 0x00, 0x00, 
                   static_cast<char>(limit),
                   0x00, 0x00, 0x00, 0x00};
  ioctl(fd, 0xC01C8F07, data);
  close(fd);
}

const char* GetKeyboardInput(const char* title, const char* initialText) {
  char userInput[512] = {0};
  if (Keyboard(title, initialText, userInput)) {
    char* output = new char[strlen(userInput) + 1];
    strcpy(output, userInput);
    return output;
  }
  return strdup("NULL");
}

void Beep(int type) { 
  sceKernelIccSetBuzzer(type);
  }
}

void PrintToConsole(const char* message, int type);
void TextNotify(int type, const char* _msg);
void ImageNotify(const char* IconUri, const char* text);
const char* Type() ;
void MountRootDirectories();
int32_t GetSystemLanguageID();
const char* GetSystemLanguage();
const char* GetFWVersion();
uint32_t GetCPUTemperature();
uint32_t GetSOCTemperature();
void SetTemperatureLimit(uint8_t limit = 60);
const char* GetKeyboardInput(const char* title, const char* initialText);
void Beep(int type);
}