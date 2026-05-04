#pragma once

namespace cmds
{
    namespace connection
    {
        void status();
        void setup();
        void version();
        void connect();
        void unload();
        void disconnect();
        void metrics();
    }

    namespace sys_info
    {
        void get_ids();
        void get_name();
        void get_fw();
        void get_type();
        void get_model();
        void get_temp();
        void get_user();
        void get_uptime();
        void get_ip();
        void get_cpu_freq();
        void get_disk_info();
        void get_all();
    }

    namespace sys_control
    {
        void fan_threshold();
        void ring_buzzer();
        void send_notify();
        void launch_app();
        void launch_uri();
        void system_state();
        void exit_app();
        void log_message();
        void dump_kernel();
    }

    namespace process
    {
        void get_list();
        void get_info();
        void get_vm_maps();
        void load_elf();
        void unload_elf();
        void kill_process();
        void suspend_process();
        void resume_process();
        void read_memory();
        void write_memory();
        void allocate_memory();
        void free_memory();
        void memory_protection();
        void load_module();
        void unload_module();
        void get_module_handle();
        void get_all_modules();
        void resolve_symbol();
        void aob_scan();
        void rpc_call();

    }
}
