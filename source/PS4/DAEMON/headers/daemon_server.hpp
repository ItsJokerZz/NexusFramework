#pragma once

namespace server
{
    namespace daemon
    {
        extern threadData td;

        void *process(void *arg);
        void *thread(void *arg);
        void start();
    }
}
