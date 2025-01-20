#pragma once
#include <array>
#include <string>
#include <map>
#include <functional>

namespace server
{
    namespace daemon
    {
        extern std::array<char, BUFFER_SIZE> buffer;
        extern int daemon_sock, client_sock;
        extern pthread_t client_thread;

        void *process(void *arg);
        void *thread(void *arg);
    }
}
