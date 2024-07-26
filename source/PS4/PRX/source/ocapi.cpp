#include "includes.h"

char response[BUFFER_SIZE];
bool connected = false;

void Connect(int client_sock) {
    snprintf(response, sizeof(response), RESPONSE_OK, 4, "true");
    ssize_t bytes_sent = sceNetSend(client_sock, response, strlen(response), 0);

    if (bytes_sent < 0 || bytes_sent
     < strlen(response)) connected = false;
    else connected = true;
}

void Notify(int client_sock, int type, const char * msg) {
    char message[BUFFER_SIZE];
    int message_length = snprintf(message, sizeof(message), "msg: %s, type: %d", msg, type);

    if (message_length >= sizeof(message)) {
        message_length = sizeof(message) - 1;
        message[message_length] = '\0';
    }

    int response_length = snprintf(response, sizeof(response), RESPONSE_OK, message_length, message);
    ssize_t bytes_sent = sceNetSend(client_sock, response, response_length, 0);

    if (bytes_sent < 0 || bytes_sent < response_length) connected = false;
    else connected = true;

    if (connected) {
        sceKernelLoadStartModule("/system/common/lib/libSceSysUtil.sprx", 0, NULL, 0, 0, 0);
        sceSysUtilSendSystemNotificationWithText(type, msg);
    }
}
