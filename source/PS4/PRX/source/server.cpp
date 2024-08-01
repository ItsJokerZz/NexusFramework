#include "utilities.hpp"

float version = 0.01;

bool loadedFromBIN = false;
bool breakThread = false;

char buffer[BUFFER_SIZE];
char response[BUFFER_SIZE];

bool connected = false;
std::string consoleType;

namespace OrbisControl {
int server_sock, client_sock;

int HandlePlugin(int load, ...) {
    va_list args;
    va_start(args, load);

    char* process_name = va_arg(args, char*);
    int result = 0;

    if (load) {
        char* path = va_arg(args, char*);
        result = sys_sdk_proc_prx_load(process_name, path);
    } else {
        int handle = va_arg(args, int);
        sys_sdk_proc_prx_unload(process_name, handle);
    }

    va_end(args);

    return result;
}

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

void SendResponse(const char* message) {
    ssize_t bytes_sent = sceNetSend(client_sock, message, strlen(message), 0);
    if (bytes_sent < 0 || bytes_sent < strlen(message)) connected = false;
}

namespace CMDS {
void Version() {
    char message[BUFFER_SIZE];
    int message_length = snprintf(message, sizeof(message), "%f", version);
    if (message_length >= sizeof(message)) {
        message_length = sizeof(message) - 1;
        message[message_length] = '\0';
    }
    snprintf(response, sizeof(response), RESPONSE_OK, message_length, message);
    SendResponse(response);
}

void Connect() {
    snprintf(response, sizeof(response), 
               RESPONSE_OK, 4, "true.");
    SendResponse(response);connected = true;
}

void Disconnect() {
    snprintf(response, sizeof(response),
               RESPONSE_OK, 4, "done.");
    SendResponse(response); connected = false;
}

void Unload() {
    snprintf(response, sizeof(response), 
                RESPONSE_OK, 4, "done.");
    SendResponse(response);
    breakThread = true;
}

void GetFW() {
    char message[BUFFER_SIZE];
    int message_length = snprintf(message, sizeof(message), "%s", System::GetFWVersion());
    if (message_length >= sizeof(message)) {
        message_length = sizeof(message) - 1;
        message[message_length] = '\0';
    }
    snprintf(response, sizeof(response), RESPONSE_OK, message_length, message);
    SendResponse(response);
}

void GetTemp() { // make all other char*/string args this way
   char temp[BUFFER_SIZE] = {0};

    char* start = strstr(buffer, "type=");
    if (start) {
        start += 5;  // Move past "type="
        char* end = strchr(start, '&');
        if (end) *end = '\0';
        sscanf(start, "%s", temp);  // Remove '&' to correctly scan the string
        start = end ? strchr(end + 1, '=') : NULL;
        if (start) start += 1;  // Move past '='
    }

    char message[BUFFER_SIZE];
    int tempValue = NULL;
    if (strcmp(temp, "cpu") == 0) {
        tempValue = System::GetCPUTemperature();
    } else if (strcmp(temp, "soc") == 0) {
        tempValue = System::GetSOCTemperature();
    }
    else return; // maybe remove

    int message_length = snprintf(message, sizeof(message), "%i", tempValue);
    if (message_length >= sizeof(message)) {
        message_length = sizeof(message) - 1;
        message[message_length] = '\0';
    }

    snprintf(response, sizeof(response), RESPONSE_OK, message_length, message);
    SendResponse(response);
}

void Notify() {
    char* msg = NULL;
    int type = 0;

    char* start = strstr(buffer, "type=");
    if (start) {
        start += 5;  
        char* end = strchr(start, '&');
        if (end) *end = '\0';
        sscanf(start, "%d", &type);
        start = end ? strchr(end + 1, '=') : NULL;
        if (start) start += 1;  
    }

    if (start) {
        char* end = strchr(start, ' ');
        if (end) *end = '\0';
        msg = DecodeURL(start);
    }

    char message[BUFFER_SIZE];
    int message_length = snprintf(message, sizeof(message), "done.");
    if (message_length >= sizeof(message)) {
        message_length = sizeof(message) - 1;
        message[message_length] = '\0';
    }

    snprintf(response, sizeof(response), RESPONSE_OK, message_length, message);
    SendResponse(response); System::TextNotify(type, msg ? msg : "msg");

    if (msg) {
        free(msg);
    }
}

void TempLimit() {
    uint8_t temp = 0;
    char* start = strstr(buffer, "limit=");
    if (start) {
        start += 6;  
        char* end = strchr(start, '&');
        if (end) *end = '\0';
        sscanf(start, "%hhu", &temp);  
        start = end ? strchr(end + 1, '=') : NULL;
        if (start) start += 1;  
    }

    char message[BUFFER_SIZE];
    int message_length = snprintf(message, sizeof(message), "done.");
    if (message_length >= sizeof(message)) {
        message_length = sizeof(message) - 1;
        message[message_length] = '\0';
    }

    snprintf(response, sizeof(response), RESPONSE_OK, message_length, message);
    SendResponse(response);

        System::SetTemperatureLimit(temp);
}

void UnloadSPRX() {  // make all other char*/string args this way
    char process[BUFFER_SIZE] = {0};
    int handle;

    char* start = strstr(buffer, "process=");
    if (start) {
        start += 8;  // Move past "process="
        char* end = strchr(start, '&');
        if (end) *end = '\0';
        sscanf(start, "%s", process);
        start = end ? end + 1 : NULL;  // Move start to after '&'
    }

    if (start) {
        start = strstr(start, "handle=");
        if (start) {
            start += 7;  // Move past "handle="
            char* end = strchr(start, '&');
            if (end) *end = '\0';
            sscanf(start, "%i", &handle);  // Pass the address of handle
        }
    }

    char message[BUFFER_SIZE];
    int message_length = snprintf(message, sizeof(message), "done.");
    if (message_length >= sizeof(message)) {
        message_length = sizeof(message) - 1;
        message[message_length] = '\0';
    }

    snprintf(response, sizeof(response), RESPONSE_OK, message_length, message);
    SendResponse(response);

    HandlePlugin((int)false, process, handle);
}

void LoadSPRX() { // make all other char*/string args this way
    char process[BUFFER_SIZE] = {0};
    char path[BUFFER_SIZE] = {0};

    char* start = strstr(buffer, "process=");
    if (start) {
        start += 8;  // Move past "process="
        char* end = strchr(start, '&');
        if (end) *end = '\0';
        sscanf(start, "%s", process);  // Remove '&' to correctly scan the string
        start = end ? end + 1 : NULL;  // Move start to after '&'
    }

    if (start) {
        start = strstr(start, "path=");
        if (start) {
            start += 5;  // Move past "path="
            char* end = strchr(start, '&');
            if (end) *end = '\0';
            sscanf(start, "%s", path);  // Remove '&' to correctly scan the string
        }
    }

    int handle = HandlePlugin((int)true, process, path);

    char message[BUFFER_SIZE];
    int message_length = snprintf(message, sizeof(message), "Process: %s, Path: %s, Handle: %i", process, path, handle);
    if (message_length >= sizeof(message)) {
        message_length = sizeof(message) - 1;
        message[message_length] = '\0';
    }

    snprintf(response, sizeof(response), RESPONSE_OK, message_length, message);
    SendResponse(response);
}

void SysType() {
    char message[BUFFER_SIZE];
    int message_length = snprintf(message, sizeof(message), "%s", System::Type());
    if (message_length >= sizeof(message)) {
        message_length = sizeof(message) - 1;
        message[message_length] = '\0';
    }
    snprintf(response, sizeof(response), RESPONSE_OK, message_length, message);
    SendResponse(response);
}

void Beep() {
    int type = -1;
    char* start = strstr(buffer, "type=");
    if (start) {
        start += 5;  
        char* end = strchr(start, '&');
        if (end) *end = '\0';
        sscanf(start, "%d", &type);
        start = end ? strchr(end + 1, '=') : NULL;
        if (start) start += 1;  
    }

    char message[BUFFER_SIZE] = {0};
    int message_length = snprintf(message, sizeof(message), "done.");
    if (message_length >= sizeof(message)) {
        message_length = sizeof(message) - 1;
        message[message_length] = '\0';
    }
    snprintf(response, sizeof(response), RESPONSE_OK, message_length, message);
    SendResponse(response);

        switch (type) {
            case 0: // stop
                System::Beep(0); break;
            case 1: // single
                System::Beep(1); break;
            case 2: // double
                System::Beep(1);
                sceKernelUsleep(125000); 
                System::Beep(1);
                break;
            case 3: // triple
                System::Beep(1);
                sceKernelUsleep(125000); 
                System::Beep(1);
                sceKernelUsleep(125000); 
                System::Beep(1);
                break;
            case 4: // continuous
                System::Beep(6); break;
        }
}
}

void HandleCommand(void (*func)()) {
    if (connected) func(); else SendResponse(RESPONSE_CONNECT); 
}

void* HandleClients(void* arg) {
  client_sock = *(int*)arg;

  memset(buffer, 0, sizeof(buffer));

  int bytes_received = sceNetRecv(client_sock, buffer, sizeof(buffer) - 1, 0);

  if (bytes_received > 0) {
    buffer[bytes_received] = '\0';

    if (strstr(buffer, "GET /connect") != NULL) 
      CMDS::Connect();
    else if (strstr(buffer, "GET /disconnect") != NULL) 
      HandleCommand(CMDS::Disconnect);
    else if (strstr(buffer, "GET /unload") != NULL)
      HandleCommand(CMDS::Unload); // make able to run unconnected (limiting is pointless)
    else if (strstr(buffer, "GET /version") != NULL)
      HandleCommand(CMDS::Version);
    else if (strstr(buffer, "GET /fw") != NULL)
      HandleCommand(CMDS::GetFW);
    else if (strstr(buffer, "GET /sysType") != NULL)
      HandleCommand(CMDS::SysType);
    else if (strstr(buffer, "GET /temp") != NULL)
      HandleCommand(CMDS::GetTemp);
    else if (strstr(buffer, "GET /notify") != NULL)
      HandleCommand(CMDS::Notify);
    else if (strstr(buffer, "GET /setTempLimit") != NULL)
      HandleCommand(CMDS::TempLimit);
    else if (strstr(buffer, "GET /beep") != NULL)
      HandleCommand(CMDS::Beep);
    else if (strstr(buffer, "GET /lSPRX") != NULL)
      HandleCommand(CMDS::LoadSPRX);
      else if (strstr(buffer, "GET /unlSPRX") != NULL)
      HandleCommand(CMDS::UnloadSPRX);
    else sceNetSend(client_sock, RESPONSE_404, strlen(RESPONSE_404), 0);
  }
  sceNetSocketClose(client_sock);
  return NULL;
}

void StartServer() {
    OrbisNetSockaddr server_addr, client_addr;
    socklen_t client_addr_len = sizeof(client_addr);

    while (!breakThread) {
        server_sock = sceNetSocket("server_sock", ORBIS_NET_AF_INET, ORBIS_NET_SOCK_STREAM, 0);
        if (server_sock < 0) {
            sceKernelDebugOutText(0, "[OCAPI] Failed to create server socket, retrying...\n");
            sceKernelUsleep(1000000); // Sleep for 1 second before retrying
            continue;
        }

        memset(&server_addr, 0, sizeof(server_addr));
        server_addr.len = sizeof(server_addr);
        server_addr.sa_family = ORBIS_NET_AF_INET;
        *(uint16_t*)server_addr.sa_data = sceNetHtons(1337);
        memset(server_addr.sa_data + 2, 0, 4);

        if (sceNetBind(server_sock, &server_addr, sizeof(server_addr)) < 0) {
            sceKernelDebugOutText(0, "[OCAPI] Failed to bind server socket, retrying...\n");
            sceNetSocketClose(server_sock);
            sceKernelUsleep(1000000); // Sleep for 1 second before retrying
            continue;
        }

        if (sceNetListen(server_sock, 1) < 0) {
            sceKernelDebugOutText(0, "[OCAPI] Failed to listen on server socket, retrying...\n");
            sceNetSocketClose(server_sock);
            sceKernelUsleep(1000000); // Sleep for 1 second before retrying
            continue;
        }

        sceKernelDebugOutText(0, "[OCAPI] Server listening on port 1337\n");

        while (!breakThread) {
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
}