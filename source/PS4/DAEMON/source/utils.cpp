#include "../headers/includes.hpp"

void printMsgToUART(const char *file, const char *func, int line, const char *fmt, ...)
{
    char msgBuffer[256];
    time_t rawtime;
    struct tm *timeinfo;
    char timeBuffer[80];
    time(&rawtime);
    timeinfo = localtime(&rawtime);
    char msg[512];

    va_list args;
    va_start(args, fmt);
    vsnprintf(msgBuffer, sizeof(msgBuffer), fmt, args);
    va_end(args);

    strftime(timeBuffer, sizeof(timeBuffer), "%m/%d/%Y @ %I:%M:%S%p", timeinfo);
    snprintf(msg, sizeof(msg), "[OCAPI %.2fb%d] %s: (%s:%d->%s) %s\n", VERSION, BUILD, timeBuffer, file, line, func, msgBuffer);

    sceKernelDebugOutText(0, msg);
}

char *performGETRequest(const char *cmd)
{
    static char buffer[BUFFER_SIZE];

    int httpCtxId = 0;
    int tmplId = 0;
    int connId = 0;
    int reqId = 0;
    int bytesRead = 0;

    char userAgent[64];
    char url[256];

    // Initialize HTTP context
    httpCtxId = sceHttpInit(0, 0, 1024 * 1024); // Keep httpCtxId here
    if (httpCtxId < 0)
    {
        PrintMsgToUART("Failed to initialize HTTP. Error code: %d", httpCtxId);
        return NULL;
    }

    // Format the User-Agent string
    snprintf(userAgent, sizeof(userAgent), "OCAPIv%.2fb%d", VERSION, BUILD);

    // Create the HTTP template using the context ID
    tmplId = sceHttpCreateTemplate(httpCtxId, userAgent, 1, 0);
    if (tmplId < 0)
    {
        PrintMsgToUART("Failed to create HTTP template. Error code: %d", tmplId);
        sceHttpTerm(httpCtxId); // Cleanup HTTP context if template creation fails
        return NULL;
    }

    // Create the HTTP connection
    connId = sceHttpCreateConnection(tmplId, "127.0.0.1", "http", RELAYS_PORT, 1);
    if (connId < 0)
    {
        PrintMsgToUART("Failed to create HTTP connection. Error code: %d", connId);
        sceHttpDeleteTemplate(tmplId); // Cleanup template
        sceHttpTerm(httpCtxId);        // Cleanup HTTP context
        return NULL;
    }

    // Format the URL path (only the path, without the protocol and host)
    snprintf(url, sizeof(url), "/%s", cmd);

    // Create the HTTP request
    reqId = sceHttpCreateRequest(connId, ORBIS_METHOD_GET, url, 0);
    if (reqId < 0)
    {
        PrintMsgToUART("Failed to create HTTP request. Error code: %d", reqId);
        sceHttpDeleteConnection(connId); // Cleanup connection
        sceHttpDeleteTemplate(tmplId);   // Cleanup template
        sceHttpTerm(httpCtxId);          // Cleanup HTTP context
        return NULL;
    }

    // Send the HTTP request (no data body for GET)
    int sendRequestResult = sceHttpSendRequest(reqId, NULL, 0);
    if (sendRequestResult < 0 && strcmp(cmd, "attach") != 0)
    {
        PrintMsgToUART("Failed to send HTTP request. Error code: %d", sendRequestResult);
        sceHttpDeleteRequest(reqId);     // Cleanup request
        sceHttpDeleteConnection(connId); // Cleanup connection
        sceHttpDeleteTemplate(tmplId);   // Cleanup template
        sceHttpTerm(httpCtxId);          // Cleanup HTTP context
        return NULL;
    }

    // Read the HTTP response
    bytesRead = sceHttpReadData(reqId, buffer, sizeof(buffer));
    if (bytesRead < 0)
    {
        PrintMsgToUART("Failed to read HTTP response. Error code: %d", bytesRead);
        sceHttpDeleteRequest(reqId);     // Cleanup request
        sceHttpDeleteConnection(connId); // Cleanup connection
        sceHttpDeleteTemplate(tmplId);   // Cleanup template
        sceHttpTerm(httpCtxId);          // Cleanup HTTP context
        return NULL;
    }

    buffer[bytesRead] = '\0';

    // Process the response (strip headers)
    char *bodyStart = strstr(buffer, "\r\n\r\n");
    if (bodyStart != NULL)
    {
        bodyStart += 4; // Skip the headers
        size_t bodyLength = bytesRead - (bodyStart - buffer);
        memmove(buffer, bodyStart, bodyLength);
        buffer[bodyLength] = '\0'; // Null-terminate the body
    }

    // Cleanup only if they were successfully created
    sceHttpDeleteRequest(reqId);     // Cleanup request
    sceHttpDeleteConnection(connId); // Cleanup connection
    sceHttpDeleteTemplate(tmplId);   // Cleanup template
    sceHttpTerm(httpCtxId);          // Cleanup HTTP context

    return buffer;
}

bool isRelayRunning()
{
    char *response = performGETRequest("ping");
    return (response != NULL && strcmp(response, "true") == 0);
}

char *DecodeURL(const char *url)
{
    size_t len = strlen(url);
    char *decoded = (char *)malloc(len + 1);

    if (decoded == NULL)
        return NULL;

    char *d = decoded;
    for (const char *s = url; *s; ++s)
    {
        if (*s == '%')
        {
            if (isxdigit(s[1]) && isxdigit(s[2]))
            {
                int value;
                sscanf(s + 1, "%2x", &value);
                *d++ = (char)value;
                s += 2;
            }
            else
                *d++ = '%';
        }
        else if (*s == '+')
            *d++ = ' ';
        else
            *d++ = *s;
    }
    *d = '\0';

    return decoded;
}

void HandleCommand(void (*func)(), int socket, bool toggle)
{
    if (toggle)
        func();
    else
    {
        if (socket == server::daemon::client_sock)
            SendResponse(RESPONSE_CONNECT, socket, toggle);
        else if (socket == server::relay::daemon_sock)
            SendResponse(RESPONSE_ATTACH, socket, toggle);
    }
}

void SendFormattedResponse(const char *message, int socket, bool *toggle)
{
    char response[BUFFER_SIZE];
    int message_length = snprintf(response, sizeof(response), RESPONSE_OK, (int)strlen(message), message);
    if (message_length >= sizeof(response))
    {
        message_length = sizeof(response) - 1;
        response[message_length] = '\0';
    }
    SendResponse(response, socket, *toggle);
}

void SendResponse(const char *msg, int socket, bool toggle)
{
    ssize_t bytes_sent = sceNetSend(socket, msg, strlen(msg), 0);

    if (bytes_sent < 0 || bytes_sent < strlen(msg))
    {
        toggle = false;
        attached = false;
    }
}
