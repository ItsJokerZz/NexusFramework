using System.Collections.Generic;

namespace NexusCheatFramework.Core
{
    public enum CheatStatus
    {
        Disabled,
        Enabled,
        Failed,
    }

    public sealed class CheatResult
    {
        public string CheatId { get; init; } = string.Empty;
        public bool Success { get; init; }
        public CheatStatus Status { get; init; }
        public string? Error { get; init; }
        public List<AppliedCode> AppliedCodes { get; init; } = new();
    }

    public sealed class AppliedCode
    {
        public ulong Address { get; init; }
        public byte[] PatchBytes { get; init; } = System.Array.Empty<byte>();
        public byte[] OriginalBytes { get; init; } = System.Array.Empty<byte>();
        public string? Note { get; init; }
    }
}
