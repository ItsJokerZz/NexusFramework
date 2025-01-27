#pragma once

namespace cmds
{
    namespace daemon
    {
        void attach_relay();

        void read_memory();
        void write_memory();
        void alloc_memory();
        void free_memory();

        void unload_module();
        void load_module();
        void stop_plugin();
        void start_plugin();

    }
}