#pragma once

namespace server
{
    namespace daemon
    {
        extern int daemon_sock, client_sock;
        extern threadData td;

        void *process(void *arg);
        void *thread(void *arg);
        void start();
    }
}
