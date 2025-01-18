#pragma once


namespace server
{
    namespace relay
    {
        extern threadData td;

        void *process(void *arg);
        void *thread(void *arg);
        void start();
    }
}
