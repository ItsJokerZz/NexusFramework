#pragma once

constexpr const char *LOCALHOST = "127.0.0.1";
constexpr const char *WEB_ROOT = "/user/common/webM4N/";

constexpr uint16_t DAEMON_PORT = 2567;
constexpr uint16_t UART_PORT = 7000;
constexpr uint16_t WEBM4N_PORT = 80;
constexpr uint16_t OUTPUT_PORT = 1279;

void handle_client();

namespace threads
{
    void *API(void *);
}
