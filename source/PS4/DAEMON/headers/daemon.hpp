#pragma once

namespace server
{
    namespace daemon
    {
        extern char buffer[BUFFER_SIZE];
        extern int daemon_sock, client_sock;

        void *process(void *arg);
        void start();
    }
}
