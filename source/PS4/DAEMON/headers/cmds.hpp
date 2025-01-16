#pragma once

namespace cmds
{
    namespace client
    {
        void Version();
        void Connect();
        void Unload();
        void Disconnect();
        void Attach();
        void GetFW();
        void GetTemp();
        void Notify();
        void TempLimit();
        void SysType();
        void Beep();
    }

    namespace daemon
    {
        void Ping();
        void Attach();
    }
}
