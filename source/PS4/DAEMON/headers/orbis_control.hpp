#include "../headers/includes.hpp"

void handle_command(void (*func)());
void *unified_process(void *arg);
void *unified_thread(void *arg);

extern "C" int32_t __wrap__init(size_t args, const void *argp);
