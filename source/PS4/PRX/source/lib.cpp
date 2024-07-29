#include "globals.h"

#define PORT 1337
#define BUFFER_SIZE 1024
#define RESPONSE_OK                                                 \
  "HTTP/1.1 200 OK\r\nContent-Type: text/plain\r\nContent-Length: " \
  "%d\r\n\r\n%s"
#define RESPONSE_404                                                         \
  "HTTP/1.1 404 Not Found\r\nContent-Type: text/plain\r\nContent-Length: "   \
  "115\r\n\r\n[OCAPI] The web server is alive! | If you're seeing this and " \
  "didnt mean to... check your command and/or arguments."

float version = 0.01;

bool loadedFromBIN = false;
int client_sock;
char buffer[BUFFER_SIZE];
char response[BUFFER_SIZE];
bool connected = false;

std::string consoleType;

/*
 struct proc_prx_load {
   char process_name[32];
   char prx_path[100];
   uint64_t res;
} __attribute__((packed));

struct proc_prx_unload {
   char process_name[32];
   uint64_t prx_handle;
   uint64_t res;
} __attribute__((packed));


uint64_t prx_handle;

int LoadPRXIntoProcess(char *process_name, char *prx_path) {
   struct proc_prx_load args;
   memset(&args, 0, sizeof(struct proc_prx_load));
   strncpy(args.process_name, process_name, sizeof(args.process_name));
   strncpy(args.prx_path, prx_path, sizeof(args.prx_path));

   syscall(500, 6, &args);

   return args.res;
}

int UnloadPRXFromProcess(char *process_name) {
   struct proc_prx_unload args;
   memset(&args, 0, sizeof(struct proc_prx_unload));
   strncpy(args.process_name, process_name, sizeof(args.process_name));
   args.prx_handle = prx_handle;

   syscall(500, 7, &args);

   return args.res;
}
 */

void HandlePlugin(bool load, char* process_name, char* prx_path) {
  // if (load) LoadPRXIntoProcess(process_name, prx_path);
  // else UnloadPRXFromProcess(process_name);
}

extern "C" {
namespace System {
void PrintToConsole(const char* message, int type) {
  static const char* prefixes[] = {"[DEBUG] ",   "[CRITICAL] ", "[ERROR] ",
                                   "[WARNING] ", "[INFO] ",     "[VERBOSE] "};
  std::string logMessage = prefixes[type] + std::string(message) + "\n";
  sceKernelDebugOutText(type, logMessage.c_str());
}

void Notify(int type, const char* _msg) {
  std::string msg = _msg;
  sceKernelLoadStartModule("/system/common/lib/libSceSysUtil.sprx", 0, NULL, 0,
                           0, 0);
  sceSysUtilSendSystemNotificationWithText(type, msg.c_str());
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

uint32_t GetTemperature() {
  uint32_t celsius;
  sceKernelGetCpuTemperature(&celsius);
  return celsius;
}

void SetTemperatureLimit(uint8_t limit = 60) {
  int fd = open("/dev/icc_fan", O_RDONLY);
  if (fd < 0) return;
  char data[10] = {0x00, 0x00, 0x00, 0x00, 0x00, static_cast<char>(limit),
                   0x00, 0x00, 0x00, 0x00};
  ioctl(fd, 0xC01C8F07, data);
  close(fd);
}

const char* GetKeyboardInput(const char* title, const char* initialText) {
  char userInput[SCE_IME_DIALOG_MAX_TEXT_LENGTH] = {0};
  if (Keyboard(title, initialText, userInput)) {
    char* output = new char[strlen(userInput) + 1];
    strcpy(output, userInput);
    return output;
  }
  return strdup("NULL");
}

}

namespace Application {
void EnterSandbox() {
  if (freeOfSandbox()) jbc_set_cred(&g_Cred);
}

void BreakFromSandbox() {
  if (freeOfSandbox()) return;
  jbc_get_cred(&g_Cred);
  g_RootCreds = g_Cred;
  jbc_jailbreak_cred(&g_RootCreds);
  jbc_set_cred(&g_RootCreds);
}

void ExitApplication() {
  EnterSandbox();
  sceSystemServiceLoadExec("exit", 0);
}
}

void PrintToConsole(const char* message, int type);
void Notify(int type, const char* _msg);
void MountRootDirectories();
int32_t GetSystemLanguageID();
const char* GetSystemLanguage();
const char* GetFWVersion();
uint32_t GetTemperature();
void SetTemperatureLimit(uint8_t limit);
const char* GetKeyboardInput(const char* title, const char* initialText);

void EnterSandbox();
void BreakFromSandbox();
void ExitApplication();
}

namespace OrbisControl {
char* DecodeURL(const char* url) {
  size_t len = strlen(url);
  char* decoded =
      (char*)malloc(len + 1);  // Allocate enough memory for the decoded string
  if (decoded == NULL) return NULL;

  char* d = decoded;
  for (const char* s = url; *s; ++s) {
    if (*s == '%') {
      if (isxdigit(s[1]) && isxdigit(s[2])) {
        int value;
        sscanf(s + 1, "%2x", &value);
        *d++ = (char)value;
        s += 2;  // Skip the next two characters
      } else
        *d++ = '%';  // Invalid encoding, copy '%' and current character
    } else if (*s == '+')
      *d++ = ' ';  // Replace '+' with space
    else
      *d++ = *s;
  }
  *d = '\0';  // Null-terminate the decoded string
  return decoded;
}

namespace CMDS {
void Version() {
  // Use a format string literal to construct the message
  char message[BUFFER_SIZE];
  int message_length = snprintf(message, sizeof(message), "%f", version);

  // Ensure the message is null-terminated if it was truncated
  if (message_length >= sizeof(message)) {
    message_length = sizeof(message) - 1;
    message[message_length] = '\0';
  }

  // Prepare the response
  int response_length = snprintf(response, sizeof(response), RESPONSE_OK,
                                 message_length, message);

  // Send the response
  ssize_t bytes_sent = sceNetSend(client_sock, response, response_length, 0);

  // Update the connection status based on the send result
  if (bytes_sent < 0 || bytes_sent < response_length)
    connected = false;
  else
    connected = true;
}

void Connect() {
  snprintf(response, sizeof(response), RESPONSE_OK, 4, "true");
  ssize_t bytes_sent = sceNetSend(client_sock, response, strlen(response), 0);

  if (bytes_sent < 0 || bytes_sent < strlen(response))
    connected = false;
  else
    connected = true;
}

void GetFW() {
  // Use a format string literal to construct the message
  char message[BUFFER_SIZE];
  int message_length =
      snprintf(message, sizeof(message), "%s", System::GetFWVersion());

  // Ensure the message is null-terminated if it was truncated
  if (message_length >= sizeof(message)) {
    message_length = sizeof(message) - 1;
    message[message_length] = '\0';
  }

  // Prepare the response
  int response_length = snprintf(response, sizeof(response), RESPONSE_OK,
                                 message_length, message);

  // Send the response
  ssize_t bytes_sent = sceNetSend(client_sock, response, response_length, 0);

  // Update the connection status based on the send result
  if (bytes_sent < 0 || bytes_sent < response_length)
    connected = false;
  else
    connected = true;
}

void GetTemp() {
  // Use a format string literal to construct the message
  char message[BUFFER_SIZE];
  int message_length =
      snprintf(message, sizeof(message), "%i", System::GetTemperature());

  // Ensure the message is null-terminated if it was truncated
  if (message_length >= sizeof(message)) {
    message_length = sizeof(message) - 1;
    message[message_length] = '\0';
  }

  // Prepare the response
  int response_length = snprintf(response, sizeof(response), RESPONSE_OK,
                                 message_length, message);

  // Send the response
  ssize_t bytes_sent = sceNetSend(client_sock, response, response_length, 0);

  // Update the connection status based on the send result
  if (bytes_sent < 0 || bytes_sent < response_length)
    connected = false;
  else
    connected = true;
}

void Notify() {
  char* msg = NULL;
  int type = 0;

  // Extract type parameter
  char* start = strstr(buffer, "type=");
  if (start) {
    start += 5;  // Skip "type="
    char* end = strchr(start, '&');
    if (end) *end = '\0';

    // Extract type
    sscanf(start, "%d", &type);

    // Move to next parameter
    start = end ? strchr(end + 1, '=') : NULL;
    if (start) start += 1;  // Skip '='
  }

  // Extract msg parameter
  if (start) {
    char* end = strchr(start, ' ');
    if (end) *end = '\0';

    // Decode the URL-encoded argument
    msg = DecodeURL(start);
  }

  char message[BUFFER_SIZE];
  int message_length =
      snprintf(message, sizeof(message), "type: %d, msg: %s", type, msg);

  if (message_length >= sizeof(message)) {
    message_length = sizeof(message) - 1;
    message[message_length] = '\0';
  }

  int response_length = snprintf(response, sizeof(response), RESPONSE_OK,
                                 message_length, message);
  ssize_t bytes_sent = sceNetSend(client_sock, response, response_length, 0);

  if (bytes_sent < 0 || bytes_sent < response_length)
    connected = false;
  else
    connected = true;

  if (connected) System::Notify(type, msg ? msg : "msg");
  if (msg) free(msg);
}

void TempLimit() {
  uint8_t temp = 0;

  // Extract temp parameter
  char* start = strstr(buffer, "temp=");
  if (start) {
    start += 5;  // Skip "temp="
    char* end = strchr(start, '&');
    if (end) *end = '\0';

    // Extract temp
    sscanf(start, "%hhu", &temp);  // Use %hhu for uint8_t

    // Move to next parameter
    start = end ? strchr(end + 1, '=') : NULL;
    if (start) start += 1;  // Skip '='
  }

  char message[BUFFER_SIZE];
  int message_length = snprintf(message, sizeof(message), "Limit Set To: %u C",
                                temp);  // Use %u for unsigned int

  if (message_length >= sizeof(message)) {
    message_length = sizeof(message) - 1;
    message[message_length] = '\0';
  }

  int response_length = snprintf(response, sizeof(response), RESPONSE_OK,
                                 message_length, message);
  ssize_t bytes_sent = sceNetSend(client_sock, response, response_length, 0);

  if (bytes_sent < 0 || bytes_sent < response_length)
    connected = false;
  else
    connected = true;

  if (connected) System::SetTemperatureLimit(temp);
}

void HandlePRX() {
  bool load = false;
  char* path = NULL;
  char* procName = NULL;

  // Extract load parameter
  char* start = strstr(buffer, "load=");
  if (start) {
    start += 5;  // Skip "load="
    char* end = strchr(start, '&');
    if (end) *end = '\0';

    // Determine the load state
    load = (strcmp(start, "true") == 0);

    // Move to next parameter
    start = end ? strchr(end + 1, '=') : NULL;
    if (start) start += 1;  // Skip '='
  }

  // Extract procName parameter
  if (start) {
    char* end = strchr(start, '&');
    if (end) *end = '\0';

    // Decode the URL-encoded argument
    procName = DecodeURL(start);

    // Move to next parameter
    start = end ? strchr(end + 1, '=') : NULL;
    if (start) start += 1;  // Skip '='
  }

  // Extract path parameter
  if (start) {
    char* end = strchr(start, ' ');
    if (end) *end = '\0';

    // Decode the URL-encoded argument
    path = DecodeURL(start);
  }

  char message[BUFFER_SIZE];
  int message_length =
      snprintf(message, sizeof(message), "load: %s, procName: %s, path: %s",
               load ? "true" : "false", procName ? procName : "null",
               path ? path : "null");

  if (message_length >= sizeof(message)) {
    message_length = sizeof(message) - 1;
    message[message_length] = '\0';
  }

  int response_length = snprintf(response, sizeof(response), RESPONSE_OK,
                                 message_length, message);
  ssize_t bytes_sent = sceNetSend(client_sock, response, response_length, 0);

  connected = (bytes_sent >= 0 && bytes_sent >= response_length);

  if (connected && path && procName) {
    HandlePlugin(load, procName, path);  // Implement this function as needed
  }
  if (path) {
    free(path);
  }
  if (procName) {
    free(procName);
  }
}

void SysType() {
  // Use a format string literal to construct the message
  char message[BUFFER_SIZE];
  int message_length = snprintf(message, sizeof(message), "%s", System::Type());

  // Ensure the message is null-terminated if it was truncated
  if (message_length >= sizeof(message)) {
    message_length = sizeof(message) - 1;
    message[message_length] = '\0';
  }

  // Prepare the response
  int response_length = snprintf(response, sizeof(response), RESPONSE_OK,
                                 message_length, message);

  // Send the response
  ssize_t bytes_sent = sceNetSend(client_sock, response, response_length, 0);

  // Update the connection status based on the send result
  if (bytes_sent < 0 || bytes_sent < response_length)
    connected = false;
  else
    connected = true;
}
}

void* HandleClients(void* arg) {
  client_sock = *(int*)arg;

  memset(buffer, 0, sizeof(buffer));

  int bytes_received = sceNetRecv(client_sock, buffer, sizeof(buffer) - 1, 0);

  if (bytes_received > 0) {
    buffer[bytes_received] = '\0';

    if (strstr(buffer, "GET /connect") != NULL)
      CMDS::Connect();
    else if (strstr(buffer, "GET /version") != NULL)
      CMDS::Version();
    else if (strstr(buffer, "GET /fw") != NULL)
      CMDS::GetFW();
    else if (strstr(buffer, "GET /sysType") != NULL)
      CMDS::SysType();
    else if (strstr(buffer, "GET /temp") != NULL)
      CMDS::GetTemp();
    else if (strstr(buffer, "GET /notify") != NULL)
      CMDS::Notify();
    else if (strstr(buffer, "GET /tempLimit") != NULL)
      CMDS::TempLimit();

    else if (strstr(buffer, "GET /sprx") != NULL)
      CMDS::HandlePRX();
    else
      sceNetSend(client_sock, RESPONSE_404, strlen(RESPONSE_404), 0);
  }
  sceNetSocketClose(client_sock);
  return NULL;
}

void StartServer() {
  int server_sock, client_sock;

  OrbisNetSockaddr server_addr, client_addr;
  socklen_t client_addr_len = sizeof(client_addr);

  server_sock =
      sceNetSocket("server_sock", ORBIS_NET_AF_INET, ORBIS_NET_SOCK_STREAM, 0);
  if (server_sock < 0) {
    sceKernelDebugOutText(0, "[OCAPI] Failed to create server socket\n");
    return;
  }

  memset(&server_addr, 0, sizeof(server_addr));
  server_addr.len = sizeof(server_addr);
  server_addr.sa_family = ORBIS_NET_AF_INET;
  *(uint16_t*)server_addr.sa_data = sceNetHtons(PORT);
  memset(server_addr.sa_data + 2, 0, 4);

  if (sceNetBind(server_sock, &server_addr, sizeof(server_addr)) < 0) {
    sceKernelDebugOutText(0, "[OCAPI] Failed to bind server socket\n");
    sceNetSocketClose(server_sock);
    return;
  }

  if (sceNetListen(server_sock, 1) < 0) {
    sceKernelDebugOutText(0, "[OCAPI] Failed to listen on server socket\n");
    sceNetSocketClose(server_sock);
    return;
  }

  sceKernelDebugOutText(0, "[OCAPI] Server listening on port 1337\n");

  for (;;) {
    client_sock = sceNetAccept(server_sock, &client_addr, &client_addr_len);
    if (client_sock < 0) {
      sceKernelDebugOutText(0, "[OCAPI] Failed to accept client connection\n");
      continue;
    }

    pthread_t client_thread;
    pthread_create(&client_thread, NULL, HandleClients, &client_sock);
    pthread_detach(client_thread);
  }
  sceNetSocketClose(server_sock);
}
}

extern "C" void entry() {
  if (loadedFromBIN) 
  OrbisControl::StartServer();
}