using System.Collections.Generic;

namespace NexusCheatFramework.Formats
{
    /// <summary>
    /// Supported cheat code types for v0.2.
    /// Includes freeze, pointer chains, and module writes.
    /// </summary>
    public enum CheatCodeType
    {
        /// <summary>Write raw bytes at an absolute address.</summary>
        WriteBytes,
        /// <summary>Write a typed value (int/uint/float/...) at an absolute address.</summary>
        WriteValue,
        /// <summary>Resolve an AOB pattern, then write raw bytes at match + Offset.</summary>
        AobWriteBytes,
        /// <summary>Resolve an AOB pattern, then write a typed value at match + Offset.</summary>
        AobWriteValue,
        /// <summary>Module base + Offset write (raw bytes).</summary>
        ModuleWriteBytes,
        /// <summary>Module base + Offset typed write.</summary>
        ModuleWriteValue,
        /// <summary>Repeatedly write a typed value at an interval (freeze).</summary>
        FreezeValue,
        /// <summary>Resolve pointer chain, write raw bytes at final address.</summary>
        PointerWriteBytes,
        /// <summary>Resolve pointer chain, write typed value at final address.</summary>
        PointerWriteValue,
        /// <summary>Resolve AOB, then pointer chain from AOB match, write raw bytes.</summary>
        AobPointerWriteBytes,
        /// <summary>Resolve AOB, then pointer chain from AOB match, write typed value.</summary>
        AobPointerWriteValue,
    }

    public sealed class CheatFile
    {
        public string TitleId { get; set; } = string.Empty;
        public string? GameName { get; set; }
        public string? Version { get; set; }
        public string? Region { get; set; }
        public List<CheatDefinition> Cheats { get; set; } = new();
    }

    public sealed class CheatDefinition
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public List<CheatCode> Codes { get; set; } = new();
        public bool EnabledByDefault { get; set; }
    }

    public sealed class CheatCode
    {
        public CheatCodeType Type { get; set; }
        public ulong? Address { get; set; }
        public string? AobPattern { get; set; }
        public long Offset { get; set; }
        public byte[]? Bytes { get; set; }
        /// <summary>Expected current bytes at the target site, used for safety checks.</summary>
        public byte[]? ExpectedBytes { get; set; }
        /// <summary>Explicit restore bytes to write when the cheat is disabled.
        /// If set, used for disable restore. If null, captured original bytes are used.</summary>
        public byte[]? RestoreBytes { get; set; }
        public string? ValueType { get; set; }
        public string? Value { get; set; }
        public string? ModuleName { get; set; }

        // Freeze support
        /// <summary>Interval in ms between freeze writes. Default 250.</summary>
        public int FreezeIntervalMs { get; set; } = 250;

        // Pointer-chain support
        /// <summary>Offsets for pointer chain, from outermost to innermost.</summary>
        public List<string>? PointerOffsets { get; set; }

        // AOB pointer chain
        /// <summary>Offset from AOB match before starting pointer chain.</summary>
        public int AobOffset { get; set; }

        /// <summary>Optional activation type for button/checkbox behavior.</summary>
        public string? ActivationType { get; set; }
    }
    /// <summary>Activation type for cheat codes.</summary>
    public static class CheatActivationType
    {
        public const string Checkbox = "checkbox";
        public const string Button = "button";
    }

}
