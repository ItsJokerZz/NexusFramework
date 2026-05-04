using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace NexusCheatFramework.Nexus
{
    /// <summary>
    /// Abstraction over the subset of NexusFramework functionality that the
    /// cheat framework needs. Implemented by <see cref="NexusClientAdapter"/>
    /// against the real NexusFramework library, and mocked in tests.
    /// </summary>
    public interface INexusClient
    {
        bool Connected { get; }
        string? IpAddress { get; }

        Task ConnectAsync(string ipAddress, CancellationToken ct = default);
        Task DisconnectAsync(CancellationToken ct = default);

        Task<TargetSnapshot> GetTargetInfoAsync(CancellationToken ct = default);
        Task<IReadOnlyList<ProcessSummary>> GetProcessListAsync(CancellationToken ct = default);
        Task<ProcessSnapshot> GetActiveProcessAsync(CancellationToken ct = default);

        Task<IReadOnlyList<MemoryRegion>> GetVirtualMemoryMapsAsync(CancellationToken ct = default);

        Task<byte[]> ReadMemoryAsync(ulong address, uint length, CancellationToken ct = default);
        Task WriteMemoryAsync(ulong address, byte[] data, CancellationToken ct = default);

        Task SetMemoryProtectionAsync(ulong address, uint length, MemoryProtection protection, CancellationToken ct = default);
    }

    [Flags]
    public enum MemoryProtection : uint
    {
        None = 0x00,
        Read = 0x01,
        Write = 0x02,
        Execute = 0x04,
        Copy = 0x08,
        ReadWrite = Read | Write,
        All = Read | Write | Execute,
    }

    public sealed class TargetSnapshot
    {
        public string Name { get; init; } = string.Empty;
        public string ConsoleType { get; init; } = string.Empty;
        public float Firmware { get; init; }
        public string Model { get; init; } = string.Empty;
    }

    public sealed class ProcessSummary
    {
        public int AppId { get; init; }
        public int Pid { get; init; }
        public string Executable { get; init; } = string.Empty;
        public string TitleId { get; init; } = string.Empty;
    }

    public sealed class ProcessSnapshot
    {
        public int AppId { get; init; }
        public int Pid { get; init; }
        public string TitleId { get; init; } = string.Empty;
        public string Name { get; init; } = string.Empty;
        public string Region { get; init; } = string.Empty;
        public string Executable { get; init; } = string.Empty;
        public float Version { get; init; }
        public string AppType { get; init; } = string.Empty;
        public IReadOnlyList<MemoryRegion> MemoryMaps { get; init; } = Array.Empty<MemoryRegion>();
    }

    public sealed class MemoryRegion
    {
        public string Name { get; init; } = string.Empty;
        public ulong Start { get; init; }
        public ulong End { get; init; }
        public ulong Offset { get; init; }
        public MemoryProtection Protection { get; init; }

        public ulong Length => End > Start ? End - Start : 0;

        public bool IsReadable => (Protection & MemoryProtection.Read) != 0;
        public bool IsWritable => (Protection & MemoryProtection.Write) != 0;
        public bool IsExecutable => (Protection & MemoryProtection.Execute) != 0;
    }
}
