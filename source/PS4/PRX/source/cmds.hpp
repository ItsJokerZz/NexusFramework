#pragma once

#include "includes.hpp"

namespace OrbisControl
{
    namespace CMDS
    {
        void Version();
        void Connect();
        void Disconnect();
        void Unload();
        void GetFW();
        void GetTemp();
        void Notify();
        void TempLimit();
        void SysType();
        void Beep();

        void test();
    }
}