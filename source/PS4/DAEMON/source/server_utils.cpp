#include "../headers/includes.hpp"

bool is_port_open(int port)
{
  int sock =
      sceNetSocket("[OrbisControl] Ping Socket", ORBIS_NET_AF_INET, ORBIS_NET_SOCK_STREAM, 0);

  if (sock < 0)
    return false;

  OrbisNetSockaddr server_addr;
  memset(&server_addr, 0, sizeof(server_addr));
  server_addr.len = sizeof(server_addr);
  server_addr.sa_family = ORBIS_NET_AF_INET;
  *(uint16_t *)server_addr.sa_data = sceNetHtons(port);
  memset(server_addr.sa_data + 2, 0, 4);

  int result = sceNetConnect(sock, &server_addr, sizeof(server_addr));

  if (result == 0)
  {
    sceNetSocketClose(sock);
    return true;
  }

  sceNetSocketClose(sock);
  return false;
}

std::string extract_param(const char *key, const std::array<char, BUFFER_SIZE> &buffer)
{
  const char *query_start = strchr(buffer.data(), '?');
  if (!query_start)
    return "";

  query_start++;

  const char *param_start = strstr(query_start, key);
  if (!param_start)
    return "";

  param_start += strlen(key);

  if (*param_start != '=')
    return "";

  param_start++;

  const char *param_end = strchr(param_start, '&');
  if (!param_end)
    param_end = strchr(param_start, ' ');

  if (!param_end)
    param_end = param_start + strlen(param_start);

  return std::string(param_start, param_end - param_start);
}

char *perform_get_request(const char *command, int port)
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

  snprintf(userAgent, sizeof(userAgent), "OrbisControl v%.2fb%d", VERSION, BUILD);
  tmplId = sceHttpCreateTemplate(httpCtxId, userAgent, 1, 0);
  if (tmplId < 0)
  {
    log_message("Failed to create HTTP template. Error code: %d", tmplId);
    sceHttpTerm(httpCtxId);
    return NULL;
  }

  connId = sceHttpCreateConnection(tmplId, "127.0.0.1", "http", port, 1);
  if (connId < 0)
  {
    log_message("Failed to create HTTP connection. Error code: %d", connId);
    sceHttpDeleteTemplate(tmplId);
    sceHttpTerm(httpCtxId);
    return NULL;
  }

  snprintf(url, sizeof(url), "/%s", command);
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
  if (sendRequestResult < 0 && strcmp(command, "attach") != 0)
  {
    log_message("Failed to send HTTP request. Error code: %d",
                sendRequestResult);
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
      // Ensure there are at least two more characters to form a valid hex value
      if (s[1] && s[2] && isxdigit(s[1]) && isxdigit(s[2]))
      {
        int value;
        sscanf(s + 1, "%2x", &value);
        *d++ = (char)value;
        s += 2; // Skip past the 2 hex digits
      }
      else
      {
        // Invalid percent-encoding, just copy the '%' character
        *d++ = '%';
      }
    }
    else if (*s == '+')
    {
      *d++ = ' '; // Convert '+' to space
    }
    else
    {
      *d++ = *s; // Copy the regular character
    }
  }

  *d = '\0'; // Null-terminate the decoded string

  return decoded;
}

std::string generate_json(const std::unordered_map<std::string, nlohmann::json> &data_entries)
{
  nlohmann::json response = {{"DATA", nlohmann::json::object()}};

  for (const auto &pair : data_entries)
    response["DATA"][pair.first] = pair.second;

  return response.dump(4);
}

void send_file_response(const char *file_path, const char *save_as)
{
  char header[512];
  char buffer[BUFFER_SIZE];

  int socket = isDaemon ? data.sockets.daemon.client : data.sockets.relay.client;
  bool *toggle = isDaemon ? &connected : &attached;

  FILE *file = fopen(file_path, "rb");
  if (!file)
  {
    *toggle = false;
    return;
  }

  fseek(file, 0, SEEK_END);
  long file_size = ftell(file);
  rewind(file);

  const char *filename;
  if (save_as && *save_as != '\0')
    filename = save_as;
  else
  {
    filename = strrchr(file_path, '/');
    filename = (filename == NULL) ? file_path : filename + 1;
  }

  int header_length = snprintf(header, sizeof(header), RESPONSE_OK_FILE, file_size, filename);
  if (sceNetSend(socket, header, header_length, 0) < header_length)
  {
    fclose(file);
    *toggle = false;
    return;
  }

  ssize_t bytes_read;
  while ((bytes_read = fread(buffer, 1, sizeof(buffer), file)) > 0)
  {
    if (sceNetSend(socket, buffer, bytes_read, 0) < bytes_read)
    {
      fclose(file);
      *toggle = false;
      return;
    }
  }

  fclose(file);
}

void send_response(const char *message)
{
  char response[BUFFER_SIZE];

  int socket =
      isDaemon ? data.sockets.daemon.client : data.sockets.relay.client;
  bool *toggle = isDaemon ? &connected : &attached;

  int message_length = snprintf(response, sizeof(response), RESPONSE_OK,
                                (int)strlen(message), message);

  if (message_length >= sizeof(response))
  {
    message_length = sizeof(response) - 1;
    response[message_length] = '\0';
  }

  ssize_t bytes_sent = sceNetSend(socket, response, strlen(response), 0);

  if (bytes_sent < 0 || bytes_sent < strlen(response))
    *toggle = false;
}

void send_response(const nlohmann::json &response_data)
{
  std::unordered_map<std::string, nlohmann::json> data_entries = {
      {"RESPONSE", response_data}};

  std::string log_string = generate_json(data_entries);
  send_response(log_string.c_str()); // Call send_response
}

void send_error_response(ErrorCode error_code)
{
  bool *toggle = isDaemon ? &connected : &attached;

  const char *message = error_messages[UNKNOWN_ERROR].message;

  if (error_code >= 0 && error_code < ERROR_COUNT)
    message = error_messages[error_code].message;

  nlohmann::json error_data = {{std::to_string(error_code), message}};
  std::unordered_map<std::string, nlohmann::json> data_entries = {
      {"ERROR", error_data}};
  std::string log_string = generate_json(data_entries);
  send_response(log_string.c_str()); // Call send_response
}

void handle_command(void (*func)())
{
  bool *toggle = isDaemon ? &connected : &attached;

  if (*toggle)
    func();
  else
    send_error_response(toggle == &connected
                            ? NOT_CONNECTED
                            : NOT_ATTACHED); // Modifying toggle
}
