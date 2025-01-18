#include "../headers/includes.hpp"

int sys_proc_list(struct proc_list_entry *procs, uint64_t *num)
{
    return orbis_syscall(107 + 90, procs, num);
}

int find_process_pid(const char *proc_name, int *pid)
{
    struct proc_list_entry *proc_list;
    uint64_t pnum;

    if (sys_proc_list(NULL, &pnum))
        return 0;

    proc_list = (struct proc_list_entry *)malloc(pnum * sizeof(struct proc_list_entry));

    if (!proc_list)
        return 0;

    if (sys_proc_list(proc_list, &pnum))
    {
        free(proc_list);
        return 0;
    }

    for (size_t i = 0; i < pnum; i++)
        if (strncmp(proc_list[i].p_comm, proc_name, 32) == 0)
        {
            *pid = proc_list[i].pid;
            free(proc_list);
            return 1;
        }

    free(proc_list);
    return 0;
}

char *perform_get_request(const char *cmd)
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
        log_message("Failed to initialize HTTP. Error code: %d", httpCtxId);
        return NULL;
    }

    // Format the User-Agent string
    snprintf(userAgent, sizeof(userAgent), "OCAPIv%.2fb%d", VERSION, BUILD);

    // Create the HTTP template using the context ID
    tmplId = sceHttpCreateTemplate(httpCtxId, userAgent, 1, 0);
    if (tmplId < 0)
    {
        log_message("Failed to create HTTP template. Error code: %d", tmplId);
        sceHttpTerm(httpCtxId); // Cleanup HTTP context if template creation fails
        return NULL;
    }

    // Create the HTTP connection
    connId = sceHttpCreateConnection(tmplId, "127.0.0.1", "http", RELAYS_PORT, 1);
    if (connId < 0)
    {
        log_message("Failed to create HTTP connection. Error code: %d", connId);
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
        log_message("Failed to create HTTP request. Error code: %d", reqId);
        sceHttpDeleteConnection(connId); // Cleanup connection
        sceHttpDeleteTemplate(tmplId);   // Cleanup template
        sceHttpTerm(httpCtxId);          // Cleanup HTTP context
        return NULL;
    }

    // Send the HTTP request (no data body for GET)
    int sendRequestResult = sceHttpSendRequest(reqId, NULL, 0);
    if (sendRequestResult < 0 && strcmp(cmd, "attach") != 0)
    {
        log_message("Failed to send HTTP request. Error code: %d", sendRequestResult);
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
        log_message("Failed to read HTTP response. Error code: %d", bytesRead);
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

bool is_relay_running()
{
    char *response = perform_get_request("ping");
    return (response != NULL && strcmp(response, "true") == 0);
}

char *decode_url(const char *url)
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

void send_response(const char *msg, int socket, bool &toggle) // Pass by reference
{
    ssize_t bytes_sent = sceNetSend(socket, msg, strlen(msg), 0);

    if (bytes_sent < 0 || bytes_sent < strlen(msg))
    {
        toggle = false; // This will now update the original variable
        attached = false;
    }
}

void send_formatted_response(const char *message, int socket, bool *toggle)
{
    char response[BUFFER_SIZE];
    int message_length = snprintf(response, sizeof(response), RESPONSE_OK, (int)strlen(message), message);
    if (message_length >= sizeof(response))
    {
        message_length = sizeof(response) - 1;
        response[message_length] = '\0';
    }
    send_response(response, socket, *toggle); // Pass by value as it's dereferenced inside
}

void handle_command(void (*func)(), int socket, bool &toggle)
{
    if (toggle)
        func();
    else
    {
        if (socket == server::daemon::client_sock)
            send_response(RESPONSE_CONNECT, socket, toggle);
        else if (socket == server::relay::daemon_sock)
            send_response(RESPONSE_ATTACH, socket, toggle);
    }
}
