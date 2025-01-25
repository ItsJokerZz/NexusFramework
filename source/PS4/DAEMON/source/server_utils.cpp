#include "../headers/includes.hpp"

struct ResponseData
{
  char *buffer;
  size_t responseSize;
};

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

static size_t writeCallback(void *contents, size_t size, size_t nmemb, void *userp)
{
  size_t totalSize = size * nmemb;

  // Cast userp to a ResponseData pointer, assuming userp is a pointer to ResponseData struct
  ResponseData *responseData = (ResponseData *)userp;

  // Reallocate buffer to accommodate new data
  char *newBuffer = (char *)realloc(responseData->buffer, responseData->responseSize + totalSize + 1);
  if (!newBuffer)
  {
    log_message("Memory allocation failed during response buffering.");
    return 0;
  }

  responseData->buffer = newBuffer;
  memcpy(&(responseData->buffer[responseData->responseSize]), contents, totalSize);
  responseData->responseSize += totalSize;
  responseData->buffer[responseData->responseSize] = '\0'; // Null-terminate the string

  return totalSize;
}

char *perform_get_request(const char *command, int port, const char *custom_url)
{
  CURL *curl;
  CURLcode result;

  ResponseData responseData = {NULL, 0}; // Initialize responseData with null buffer and size 0

  curl = curl_easy_init();
  if (!curl)
  {
    log_message("Failed to initialize CURL.");
    return NULL;
  }

  std::string url;
  if (custom_url)
    url = custom_url;
  else
    url = "http://127.0.0.1:" + std::to_string(port) + "/" + command;

  // Configure CURL options.
  curl_easy_setopt(curl, CURLOPT_URL, url.c_str());
  curl_easy_setopt(curl, CURLOPT_USERAGENT, "PS4");
  curl_easy_setopt(curl, CURLOPT_SSL_VERIFYHOST, 0);
  curl_easy_setopt(curl, CURLOPT_SSL_VERIFYPEER, 0);
  curl_easy_setopt(curl, CURLOPT_WRITEFUNCTION, writeCallback);
  curl_easy_setopt(curl, CURLOPT_WRITEDATA, &responseData); // Pass responseData to callback
  curl_easy_setopt(curl, CURLOPT_FOLLOWLOCATION, 1);

  result = curl_easy_perform(curl);

  if (result != CURLE_OK)
  {
    free(responseData.buffer); // Free the buffer if an error occurred
    responseData.buffer = NULL;
  }
  else
  {
    long httpStatusCode;
    curl_easy_getinfo(curl, CURLINFO_RESPONSE_CODE, &httpStatusCode);

    if (httpStatusCode != 200)
    {
      free(responseData.buffer); // Free the buffer if the status code is not 200
      responseData.buffer = NULL;
    }
  }

  curl_easy_cleanup(curl);

  return responseData.buffer; // Return the buffer, which contains the response
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
