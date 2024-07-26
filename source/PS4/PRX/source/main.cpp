#include "includes.h"

bool loadedFromBIN = false;

void* handle_client(void * arg) {
    int client_sock = *(int *)arg;
    char buffer[BUFFER_SIZE];

    memset(buffer, 0, sizeof(buffer));

    int bytes_received = sceNetRecv(client_sock, buffer, sizeof(buffer) - 1, 0);
    if (bytes_received > 0) {
        buffer[bytes_received] = '\0';

        if (strstr(buffer, "GET /connect") != NULL) {
            Connect(client_sock);
        } else if (strstr(buffer, "GET /notify") != NULL) {
            char * msg = NULL;
            int type = 0;

            // Extract type parameter
            char * start = strstr(buffer, "type=");
            if (start) {
                start += 5; // Skip "type="
                char * end = strchr(start, '&');
                if (end) *end = '\0';

                // Extract type
                sscanf(start, "%d", &type);

                // Move to next parameter
                start = end ? strchr(end + 1, '=') : NULL;
                if (start) {
                    start += 1; // Skip '='
                }
            }

            // Extract msg parameter
            if (start) {
                char * end = strchr(start, ' ');
                if (end) *end = '\0';

                // Decode the URL-encoded argument
                msg = url_decode(start);
            }

            Notify(client_sock, type, msg ? msg : "msg");

            if (msg) free(msg);

        } else sceNetSend(client_sock, RESPONSE_404, strlen(RESPONSE_404), 0);
    }
    sceNetSocketClose(client_sock);

    return NULL;
}

void start_server() {
    int server_sock, client_sock;

    OrbisNetSockaddr server_addr, client_addr;
    socklen_t client_addr_len = sizeof(client_addr);

    server_sock = sceNetSocket("server_sock", ORBIS_NET_AF_INET, ORBIS_NET_SOCK_STREAM, 0);
    if (server_sock < 0) {
        sceKernelDebugOutText(0, "[OCAPI] Failed to create server socket\n");
        return;
    }

    memset(&server_addr, 0, sizeof(server_addr));
    server_addr.len = sizeof(server_addr);
    server_addr.sa_family = ORBIS_NET_AF_INET;
    *(uint16_t *)server_addr.sa_data = sceNetHtons(PORT);
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
        pthread_create(&client_thread, NULL, handle_client, &client_sock);
        pthread_detach(client_thread);
    }
    sceNetSocketClose(server_sock);
}

extern "C" void* entry(void* _) {
    if (loadedFromBIN) start_server(); return NULL;
}//