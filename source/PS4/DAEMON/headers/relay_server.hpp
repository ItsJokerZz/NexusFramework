#pragma once


namespace server
{
    namespace relay
    {
        extern int relay_sock, daemon_sock;
        extern threadData td;

        void *process(void *arg);
        void *thread(void *arg);
        void start();
    }
}
