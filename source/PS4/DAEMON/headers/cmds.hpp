#pragma once

namespace cmds
{
    namespace client
    {
        namespace connection
        {
            void version();
            void connect();
            void unload();
            void disconnect();
            void attach();

        }

        namespace sys_info
        {
            void sys_type();
            void get_fw();
            void get_temp();

        }

        namespace sys_control
        {
            void notify();
            void temp_limit();
            void beep();

        }

        void exec_prx();
        void proc_list();
        void find_pid_by_name();
        void load_plugin();

    }

    namespace daemon
    {
        void ping();
        void attach();
        void exec_prx();
        void load_plugin();

    }
}