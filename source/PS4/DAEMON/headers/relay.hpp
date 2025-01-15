#include "../headers/includes.hpp"

namespace server
{
    namespace relay
    {
        extern bool alive;
        extern char buffer[BUFFER_SIZE];
        extern int relay_sock, daemon_sock;

        void *process(void *arg);
        void *thread(void *arg);
        void start();
    }
}
