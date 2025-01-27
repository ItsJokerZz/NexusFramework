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
            void get_proc_list();
            void get_proc_info();

            void find_pid_by_name();
            void find_name_of_pid();

            void read_proc_mem();
            void write_proc_mem();
            void alloc_proc_mem();
            void free_proc_mem();

            void stop_plugin();
            void start_plugin();
            void unload_module();
            void load_module();

        }

    }
}