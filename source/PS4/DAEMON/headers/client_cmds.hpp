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

        } // namespace connection

        namespace sys_info
        {
            void sys_type();
            void get_fw();
            void get_temp();
            void get_user();

        } // namespace sys_info

        namespace sys_control
        {
            void notify();
            void temp_limit();
            void set_power_state();
            void ring_buzzer();

        } // namespace sys_control

        namespace process
        {
            void load_module();
            void get_proc_list();
            void find_pid_by_name();
            void find_name_of_pid();
            void load_plugin();
            void rw_proc_mem();

            void get_proc_info();

        } // namespace process

    }
}