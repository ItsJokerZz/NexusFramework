#include "../headers/includes.hpp"

void handle_request(const std::string &request);
void *unified_process(void *arg);
void *unified_thread(void *);

void *telnet_server(void *);
void *send_udp_signal(void *);

extern "C" int32_t __wrap__init(size_t, const void *);
