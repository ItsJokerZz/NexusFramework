#pragma once

namespace cmds
{
    namespace client
    {
        void version();
        void connect();
        void unload();
        void disconnect();
        void attach();
        void get_fw();
        void get_temp();
        void notify();
        void temp_limit();
        void sys_type();
        void beep();
        void exec_prx();
    }

    namespace daemon
    {
        void ping();
        void attach();
        void exec_prx();
        void process_list();
    }
}
