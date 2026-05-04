#pragma once

enum ErrorCode {
  UNKNOWN_ERROR,

  NOT_CONNECTED,

  INVALID_CMD,
  INVALID_ARGS,

  NO_OPEN_APP,

  PS4_ONLY,
  PS5_ONLY,
  
  ERROR_COUNT
};

inline constexpr const char *error_messages[ERROR_COUNT] = {
    "Unknown error occurred. Try again later.",
    "Not connected to server. Check your network.",
    "Command not found. Verify spelling and syntax.",
    "Invalid parameters. Review and correct input.",
    "No open application. Launch one to proceed.",
    "This can only be ran on a PlayStation 5 system.",
    "This can only be ran on a PlayStation 4 system.",
};

constexpr const char *RESPONSE_OPTIONS =
    "HTTP/1.1 204 No Content\r\n"
    "Access-Control-Allow-Origin: *\r\n"
    "Access-Control-Allow-Methods: GET, POST, OPTIONS\r\n"
    "Access-Control-Allow-Headers: Content-Type\r\n"
    "Access-Control-Max-Age: 86400\r\n"
    "Connection: keep-alive\r\n"
    "\r\n";

constexpr const char *SUCCESS_MESSAGE = "Action completed successfully.";

bool is_port_in_use(int port);

std::string decode_url(std::string_view url);
std::string extract_param(std::string_view key, bool GET = true);

void send_raw_file_response(const uint8_t *data, size_t len,
                            const std::string &filename = "memory.bin");
void send_response(const nlohmann::json &data);
void send_error_response(ErrorCode code);
bool no_open_app_response();
void handle_command(void (*func)());
