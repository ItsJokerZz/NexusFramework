#include "../headers/includes.hpp"

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

std::string create_json_response(const std::unordered_map<std::string, nlohmann::json> &data_entries)
{
    nlohmann::json response = {{"DATA", nlohmann::json::object()}};

    for (const auto &pair : data_entries)
        response["DATA"][pair.first] = pair.second;

    return response.dump(4);
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

void send_response(const nlohmann::json &response_data, int socket, bool *toggle)
{
    // Create a map to hold the "RESPONSE" key and associated JSON data
    std::unordered_map<std::string, nlohmann::json> data_entries = {
        {"RESPONSE", response_data} // The "RESPONSE" key holds the JSON data
    };

    // Generate the final JSON response string
    std::string log_string = create_json_response(data_entries);

    // Send the response
    send_formatted_response(log_string.c_str(), socket, toggle);
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

void send_error_response(ErrorCode error_code, int socket, bool *toggle)
{
    const char *message = error_messages[UNKNOWN_ERROR].message;

    if (error_code >= 0 && error_code < ERROR_COUNT)
        message = error_messages[error_code].message;

    nlohmann::json error_data =
        {{std::to_string(error_code), message}};

    std::unordered_map<std::string, nlohmann::json>
        data_entries = {{"ERROR", error_data}};

    std::string log_string = create_json_response(data_entries);
    send_formatted_response(log_string.c_str(), socket, toggle);
}

void send_error_response(const std::string &message, int socket, bool *toggle)
{
    // Create the JSON response with a default error code (0) and provided message
    std::string log_string = "{\n"
                             "    \"ERROR\": {\n"
                             "        \"msg\": \"" +
                             message + "\"\n"
                                       "    }\n"
                                       "}";

    send_formatted_response(log_string.c_str(), socket, toggle);
}

void handle_command(void (*func)(), int socket, bool &toggle)
{
    if (toggle)
        func();
    else
    {
        if (socket == server::daemon::client_sock)
            send_error_response(ErrorCode::NOT_CONNECTED, socket, &toggle);
        else if (socket == server::relay::daemon_sock)
            send_error_response(ErrorCode::NOT_ATTACHED, socket, &toggle);
    }
}
