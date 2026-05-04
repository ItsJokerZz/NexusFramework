using System.Collections.Generic;

namespace NexusCheatFramework.Formats
{
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
        public string? ValueType { get; set; }
        public string? Value { get; set; }
        public string? ModuleName { get; set; }
    }
}
