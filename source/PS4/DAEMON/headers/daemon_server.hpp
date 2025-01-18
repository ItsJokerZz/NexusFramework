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

        void *process(void *arg);
        void *thread(void *arg);
        void start();
    }
}
