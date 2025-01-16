#pragma once

namespace server
{
    namespace relay
    {
        extern char buffer[BUFFER_SIZE];
        extern int relay_sock, daemon_sock;

        void *process(void *arg);
        void *thread(void *arg);
        void start();
    }
}
