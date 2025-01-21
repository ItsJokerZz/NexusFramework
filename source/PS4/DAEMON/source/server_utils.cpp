#include "../headers/includes.hpp"

std::string extract_param(const char *key,
                          std::array<char, BUFFER_SIZE> buffer) {
  const char *start = strstr(buffer.data(), key);
  if (!start)
    return "";

  start += strlen(key);

  const char *end = strchr(start, ' ') ?: start + strlen(start);

  return std::string(start, end);
}

char *perform_get_request(const char *command) {
  static char buffer[BUFFER_SIZE];
  int httpCtxId = 0, tmplId = 0, connId = 0, reqId = 0, bytesRead = 0;
  char userAgent[64], url[256];

  httpCtxId = sceHttpInit(0, 0, 1024 * 1024);
  if (httpCtxId < 0) {
    log_message("Failed to initialize HTTP. Error code: %d", httpCtxId);
    return NULL;
  }

  snprintf(userAgent, sizeof(userAgent), "OCAPIv%.2fb%d", VERSION, BUILD);
  tmplId = sceHttpCreateTemplate(httpCtxId, userAgent, 1, 0);
  if (tmplId < 0) {
    log_message("Failed to create HTTP template. Error code: %d", tmplId);
    sceHttpTerm(httpCtxId);
    return NULL;
  }

  connId = sceHttpCreateConnection(tmplId, "127.0.0.1", "http", RELAYS_PORT, 1);
  if (connId < 0) {
    log_message("Failed to create HTTP connection. Error code: %d", connId);
    sceHttpDeleteTemplate(tmplId);
    sceHttpTerm(httpCtxId);
    return NULL;
  }

  snprintf(url, sizeof(url), "/%s", command);
  reqId = sceHttpCreateRequest(connId, ORBIS_METHOD_GET, url, 0);
  if (reqId < 0) {
    log_message("Failed to create HTTP request. Error code: %d", reqId);
    sceHttpDeleteConnection(connId);
    sceHttpDeleteTemplate(tmplId);
    sceHttpTerm(httpCtxId);
    return NULL;
  }

  int sendRequestResult = sceHttpSendRequest(reqId, NULL, 0);
  if (sendRequestResult < 0 && strcmp(command, "attach") != 0) {
    log_message("Failed to send HTTP request. Error code: %d",
                sendRequestResult);
    sceHttpDeleteRequest(reqId);
    sceHttpDeleteConnection(connId);
    sceHttpDeleteTemplate(tmplId);
    sceHttpTerm(httpCtxId);
    return NULL;
  }

  bytesRead = sceHttpReadData(reqId, buffer, sizeof(buffer));
  if (bytesRead < 0) {
    log_message("Failed to read HTTP response. Error code: %d", bytesRead);
    sceHttpDeleteRequest(reqId);
    sceHttpDeleteConnection(connId);
    sceHttpDeleteTemplate(tmplId);
    sceHttpTerm(httpCtxId);
    return NULL;
  }

  buffer[bytesRead] = '\0';
  char *bodyStart = strstr(buffer, "\r\n\r\n");
  if (bodyStart != NULL) {
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

bool is_port_open(int port) {
  int sock =
      sceNetSocket("ping.server", ORBIS_NET_AF_INET, ORBIS_NET_SOCK_STREAM, 0);

  if (sock < 0)
    return false;

  OrbisNetSockaddr server_addr;
  memset(&server_addr, 0, sizeof(server_addr));
  server_addr.len = sizeof(server_addr);
  server_addr.sa_family = ORBIS_NET_AF_INET;
  *(uint16_t *)server_addr.sa_data = sceNetHtons(port);
  memset(server_addr.sa_data + 2, 0, 4);

  int result = sceNetConnect(sock, &server_addr, sizeof(server_addr));

  if (result == 0) {
    sceNetSocketClose(sock);
    return true;
  }

  sceNetSocketClose(sock);
  return false;
}

char *decode_url(const char *url) {
  size_t len = strlen(url);
  char *decoded = (char *)malloc(len + 1);

  if (decoded == NULL)
    return NULL;

  char *d = decoded;
  for (const char *s = url; *s; ++s) {
    if (*s == '%') {
      if (isxdigit(s[1]) && isxdigit(s[2])) {
        int value;
        sscanf(s + 1, "%2x", &value);
        *d++ = (char)value;
        s += 2;
      } else
        *d++ = '%';
    } else if (*s == '+')
      *d++ = ' ';
    else
      *d++ = *s;
  }
  *d = '\0';

  return decoded;
}

std::string generate_json(
    const std::unordered_map<std::string, nlohmann::json> &data_entries) {
  nlohmann::json response = {{"DATA", nlohmann::json::object()}};

  for (const auto &pair : data_entries)
    response["DATA"][pair.first] = pair.second;

  return response.dump(4);
}

void send_response(const char *message) {
  char response[BUFFER_SIZE];

  int socket =
      isDaemon ? data.sockets.daemon.client : data.sockets.relay.client;
  bool *toggle = isDaemon ? &connected : &attached;

  int message_length = snprintf(response, sizeof(response), RESPONSE_OK,
                                (int)strlen(message), message);

  if (message_length >= sizeof(response)) {
    message_length = sizeof(response) - 1;
    response[message_length] = '\0';
  }

  ssize_t bytes_sent = sceNetSend(socket, response, strlen(response), 0);

  if (bytes_sent < 0 || bytes_sent < strlen(response))
    *toggle = false;
}

void send_response(const nlohmann::json &response_data) {
  std::unordered_map<std::string, nlohmann::json> data_entries = {
      {"RESPONSE", response_data}};

  std::string log_string = generate_json(data_entries);
  send_response(log_string.c_str()); // Call send_response
}

void send_error_response(ErrorCode error_code) {
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

void send_error_response(const std::string &message) {
  nlohmann::json error_response;
  error_response["ERROR"]["msg"] = message;

  std::string log_string = error_response.dump(4); // Pretty print with 4 spaces
  send_response(log_string.c_str());               // Call send_response
}
