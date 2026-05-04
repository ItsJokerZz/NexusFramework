#include "headers.hpp"

extern bool g_is_library_request;

bool is_port_in_use(int port)
{
  int sock = socket(AF_INET, SOCK_STREAM, 0);
  if (sock < 0)
    return false;

  sockaddr_in addr{};
  addr.sin_family = AF_INET;
  addr.sin_port = htons(port);
  addr.sin_addr.s_addr = htonl(INADDR_LOOPBACK);

  bool open =
      connect(sock, reinterpret_cast<sockaddr *>(&addr), sizeof(addr)) == 0;
  close(sock);
  return open;
}

std::string decode_url(std::string_view url)
{
  std::string decoded;
  decoded.reserve(url.size());
  for (size_t i = 0; i < url.size(); ++i)
  {
    if (url[i] == '%')
    {
      if (i + 2 < url.size() && std::isxdigit(url[i + 1]) &&
          std::isxdigit(url[i + 2]))
      {
        auto hex_char_to_int = [](char c) -> int
        {
          return c >= '0' && c <= '9'   ? c - '0'
                 : c >= 'a' && c <= 'f' ? c - 'a' + 10
                 : c >= 'A' && c <= 'F' ? c - 'A' + 10
                                        : 0;
        };
        decoded += static_cast<char>((hex_char_to_int(url[i + 1]) << 4) |
                                     hex_char_to_int(url[i + 2]));
        i += 2;
      }
      else
        decoded += '%';
    }
    else if (url[i] == '+')
      decoded += ' ';
    else
      decoded += url[i];
  }
  return decoded;
}

std::string extract_param(std::string_view key, bool GET)
{
  const std::string &buffer = server.buffer;

  if (GET)
  {
    size_t query_pos = buffer.find('?');
    if (query_pos == std::string::npos)
      return "";

    std::string_view query(buffer.data() + query_pos + 1,
                           buffer.size() - query_pos - 1);
    std::string key_str = std::string(key) + "=";
    size_t key_pos = query.find(key_str);
    if (key_pos == std::string::npos)
      return "";

    size_t value_start = key_pos + key_str.length();
    size_t value_end = query.find('&', value_start);
    if (value_end == std::string::npos)
      value_end = query.find(' ', value_start);
    if (value_end == std::string::npos)
      value_end = query.length();

    return std::string(query.substr(value_start, value_end - value_start));
  }

  size_t header_end = buffer.find("\r\n\r\n");
  if (header_end == std::string::npos)
    return "";

  auto j_opt = json::parse(buffer.begin() + header_end + 4, buffer.end(),
                           nullptr, false);
  if (!j_opt.is_object() && !j_opt.is_array())
    return "";

  const json *current = &j_opt;

  if (key.empty())
    return current->dump();

  size_t pos = 0;
  while (true)
  {
    size_t dot_pos = key.find('.', pos);
    std::string part = (dot_pos == std::string::npos)
                           ? std::string(key.substr(pos))
                           : std::string(key.substr(pos, dot_pos - pos));

    if (current->is_object())
    {
      if (!current->contains(part))
        return "";
      current = &((*current)[part]);
    }
    else if (current->is_array())
    {
      if (part.empty() || !std::all_of(part.begin(), part.end(), ::isdigit))
        return "";

      size_t index = std::stoul(part);
      if (index >= current->size())
        return "";
      current = &((*current)[index]);
    }
    else
    {
      return "";
    }

    if (dot_pos == std::string::npos)
      break;

    pos = dot_pos + 1;
  }

  if (current->is_null())
    return "";

  if (current->is_string())
    return current->get<std::string>();

  if (current->is_boolean())
    return current->get<bool>() ? "true" : "false";

  if (current->is_number_integer())
    return std::to_string(current->get<int64_t>());

  if (current->is_number_unsigned())
    return std::to_string(current->get<uint64_t>());

  if (current->is_number_float())
    return std::to_string(current->get<double>());

  if (current->is_array() || current->is_object())
    return current->dump();

  return "";
}

void send_raw_file_response(const uint8_t *data, size_t len,
                            const std::string &filename)
{
  std::string header = "HTTP/1.1 200 OK\r\n"
                       "Content-Type: application/octet-stream\r\n"
                       "Content-Disposition: attachment; filename=\"" +
                       filename +
                       "\"\r\n"
                       "Content-Length: " +
                       std::to_string(len) +
                       "\r\n"
                       "Connection: close\r\n\r\n";

  ssize_t sent =
      send(server.sockets.client, header.c_str(), header.length(), 0);
  if (sent < 0 || sent != (ssize_t)header.length())
  {
    connected = 0;
    shutdown(server.sockets.client, SHUT_RDWR);
    close(server.sockets.client);
    return;
  }

  size_t total_sent = 0;
  while (total_sent < len)
  {
    ssize_t chunk =
        send(server.sockets.client, data + total_sent, len - total_sent, 0);
    if (chunk <= 0)
    {
      connected = 0;
      break;
    }
    total_sent += chunk;
  }

  shutdown(server.sockets.client, SHUT_RDWR);
  close(server.sockets.client);
}

void send_response(const nlohmann::json &data)
{
  nlohmann::json response_body = {{"RESPONSE", data}};
  std::string json_str = response_body.dump();

  std::string response;
  response.reserve(128 + json_str.length());
  response += "HTTP/1.1 200 OK\r\n"
              "Content-Type: application/json\r\n"
              "Content-Length: " +
              std::to_string(json_str.length()) +
              "\r\n"
              "Access-Control-Allow-Origin: *\r\n"
              "Connection: keep-alive\r\n\r\n" +
              json_str;

  if (send(server.sockets.client, response.data(), response.length(), 0) <
      static_cast<ssize_t>(response.length()))
  {
    connected = false;
  }

  shutdown(server.sockets.client, SHUT_RDWR);
  close(server.sockets.client);
}

void send_error_response(ErrorCode code)
{
  ErrorCode safe_code =
      (code >= 0 && code < ERROR_COUNT) ? code : UNKNOWN_ERROR;
  send_response({{"ERROR", safe_code}, {"MESSAGE", error_messages[safe_code]}});
}

bool no_open_app_response()
{
  pid_t pid = get_running_app_pid();
  bool is_daemon = is_daemon_process();
  if (pid == -1 || is_daemon)
  {
    send_error_response(NO_OPEN_APP);
    return true;
  }

  return false;
}

void handle_command(void (*func)())
{
  if (!g_is_library_request || connected)
    func();
  else
    send_error_response(NOT_CONNECTED);
}
