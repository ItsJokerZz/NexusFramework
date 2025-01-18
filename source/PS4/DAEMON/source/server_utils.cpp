#include "../headers/includes.hpp"

bool is_relay_running()
{
    char *response = perform_get_request("ping");
    return (response != NULL && strcmp(response, "true") == 0);
}

char *perform_get_request(const char *cmd)
{
    static char buffer[BUFFER_SIZE];
    int httpCtxId = 0, tmplId = 0, connId = 0, reqId = 0, bytesRead = 0;
    char userAgent[64], url[256];

    httpCtxId = sceHttpInit(0, 0, 1024 * 1024);
    if (httpCtxId < 0)
    {
        log_message("Failed to initialize HTTP. Error code: %d", httpCtxId);
        return NULL;
    }

    snprintf(userAgent, sizeof(userAgent), "OCAPIv%.2fb%d", VERSION, BUILD);
    tmplId = sceHttpCreateTemplate(httpCtxId, userAgent, 1, 0);
    if (tmplId < 0)
    {
        log_message("Failed to create HTTP template. Error code: %d", tmplId);
        sceHttpTerm(httpCtxId);
        return NULL;
    }

    connId = sceHttpCreateConnection(tmplId, "127.0.0.1", "http", RELAYS_PORT, 1);
    if (connId < 0)
    {
        log_message("Failed to create HTTP connection. Error code: %d", connId);
        sceHttpDeleteTemplate(tmplId);
        sceHttpTerm(httpCtxId);
        return NULL;
    }

    snprintf(url, sizeof(url), "/%s", cmd);
    reqId = sceHttpCreateRequest(connId, ORBIS_METHOD_GET, url, 0);
    if (reqId < 0)
    {
        log_message("Failed to create HTTP request. Error code: %d", reqId);
        sceHttpDeleteConnection(connId);
        sceHttpDeleteTemplate(tmplId);
        sceHttpTerm(httpCtxId);
        return NULL;
    }

    int sendRequestResult = sceHttpSendRequest(reqId, NULL, 0);
    if (sendRequestResult < 0 && strcmp(cmd, "attach") != 0)
    {
        log_message("Failed to send HTTP request. Error code: %d", sendRequestResult);
        sceHttpDeleteRequest(reqId);
        sceHttpDeleteConnection(connId);
        sceHttpDeleteTemplate(tmplId);
        sceHttpTerm(httpCtxId);
        return NULL;
    }

    bytesRead = sceHttpReadData(reqId, buffer, sizeof(buffer));
    if (bytesRead < 0)
    {
        log_message("Failed to read HTTP response. Error code: %d", bytesRead);
        sceHttpDeleteRequest(reqId);
        sceHttpDeleteConnection(connId);
        sceHttpDeleteTemplate(tmplId);
        sceHttpTerm(httpCtxId);
        return NULL;
    }

    buffer[bytesRead] = '\0';
    char *bodyStart = strstr(buffer, "\r\n\r\n");
    if (bodyStart != NULL)
    {
        bodyStart += 4;
        size_t bodyLength = bytesRead - (bodyStart - buffer);
        memmove(buffer, bodyStart, bodyLength);
        buffer[bodyLength] = '\0';
    }

    sceHttpDeleteRequest(reqId);
    sceHttpDeleteConnection(connId);
    sceHttpDeleteTemplate(tmplId);
    sceHttpTerm(httpCtxId);

    return buffer;
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

std::string generate_json(const std::unordered_map<std::string, nlohmann::json> &data_entries)
{
    nlohmann::json response = {{"DATA", nlohmann::json::object()}};

    for (const auto &pair : data_entries)
        response["DATA"][pair.first] = pair.second;

    return response.dump(4);
}

void send_response(const char *msg, int socket, bool *toggle)
{
    char response[BUFFER_SIZE];
    int message_length = snprintf(response, sizeof(response), RESPONSE_OK, (int)strlen(msg), msg);

    if (message_length >= sizeof(response))
    {
        message_length = sizeof(response) - 1;
        response[message_length] = '\0';
    }

    ssize_t bytes_sent = sceNetSend(socket, response, strlen(response), 0);

    if (bytes_sent < 0 || bytes_sent < strlen(response))
        *toggle = false; // Modifying toggle
}

void send_response(const nlohmann::json &response_data, int socket, bool *toggle)
{
    std::unordered_map<std::string, nlohmann::json> data_entries = {
        {"RESPONSE", response_data}};

    std::string log_string = generate_json(data_entries);
    send_response(log_string.c_str(), socket, toggle); // Pass toggle
}

void send_error_response(ErrorCode error_code, int socket, bool *toggle)
{
    const char *message = error_messages[UNKNOWN_ERROR].message;

    if (error_code >= 0 && error_code < ERROR_COUNT)
        message = error_messages[error_code].message;

    nlohmann::json error_data =
        {{std::to_string(error_code), message}};

    std::unordered_map<std::string, nlohmann::json> data_entries = {{"ERROR", error_data}};
    std::string log_string = generate_json(data_entries);
    send_response(log_string.c_str(), socket, toggle); // Pass toggle
}

void send_error_response(const std::string &message, int socket, bool *toggle)
{
    std::string log_string = "{\n"
                             "    \"ERROR\": {\n"
                             "        \"msg\": \"" +
                             message + "\"\n"
                                       "    }\n"
                                       "}";

    send_response(log_string.c_str(), socket, toggle); // Pass toggle
}

void handle_command(void (*func)())
{
    int socket = (server::daemon::client_sock != -1) ? server::daemon::client_sock : server::relay::daemon_sock;
    bool *toggle = (server::daemon::client_sock != -1) ? &connected : &attached;

    if (*toggle)
        func();
    else
        send_error_response(toggle == &connected ? NOT_CONNECTED : NOT_ATTACHED, socket, toggle); // Modifying toggle
}
