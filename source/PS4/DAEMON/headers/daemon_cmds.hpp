#pragma once

namespace cmds
{
    namespace daemon
    {
        void attach_relay();
        void load_module();
        void start_plugin();
        void read_proc_mem();
        void write_proc_mem();

    }
}