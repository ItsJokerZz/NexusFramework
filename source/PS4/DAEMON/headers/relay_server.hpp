#pragma once
#include <array>
#include <string>
#include <map>
#include <functional>

namespace server
{
    namespace relay
    {
        extern std::array<char, BUFFER_SIZE> buffer;
        extern int relay_sock, daemon_sock;
        extern pthread_t daemon_thread;

        void *process(void *arg);
        void *thread(void *arg);
    }
}
