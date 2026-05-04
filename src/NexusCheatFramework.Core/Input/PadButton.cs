using System;

namespace NexusCheatFramework.Input
{
    /// <summary>
    /// PS4/PS5 pad button bitmap. Values mirror Sony's ScePadButtonDataOffset
    /// constants as exposed in the open-source orbis/openorbis SDKs (the same
    /// header is used by etaHEN). Reproduced here as plain numeric constants
    /// for portability; no Sony code is embedded.
    /// </summary>
    [Flags]
    public enum PadButton : uint
    {
        None = 0,
        L3 = 0x00000002,
        R3 = 0x00000004,
        Options = 0x00000008,
        Up = 0x00000010,
        Right = 0x00000020,
        Down = 0x00000040,
        Left = 0x00000080,
        L2 = 0x00000100,
        R2 = 0x00000200,
        L1 = 0x00000400,
        R1 = 0x00000800,
        Triangle = 0x00001000,
        Circle = 0x00002000,
        Cross = 0x00004000,
        Square = 0x00008000,
        TouchPad = 0x00100000,
        // The Share/Create button is handled by the system/shellui and is not
        // reported by scePadReadState directly; etaHEN intercepts it via shellui
        // hooks. Modeled here as a synthetic flag so shortcut configs are honest
        // about what console-side polling can actually detect.
        Share = 0x40000000,
    }
}
