using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NexusCheatFramework.Nexus;

namespace NexusCheatFramework.Tests;

/// <summary>
/// Trivial in-memory <see cref="INexusClient"/> backed by a single byte buffer
/// at a configurable base address. Used by AobScanner and CheatEngine tests.
/// </summary>
internal sealed class FakeNexusClient : INexusClient
{
    public byte[] Buffer { get; }
    public ulong Base { get; }
    public List<(ulong addr, byte[] data)> Writes { get; } = new();
    public bool ReadShouldFail { get; set; }
    public bool Connected { get; set; } = true;
    public string? IpAddress => "fake";

    public FakeNexusClient(byte[] buffer, ulong baseAddress = 0x10000)
    {
        Buffer = buffer; Base = baseAddress;
    }

    public Task ConnectAsync(string ipAddress, CancellationToken ct = default) => Task.CompletedTask;
    public Task DisconnectAsync(CancellationToken ct = default) { Connected = false; return Task.CompletedTask; }

    public Task<TargetSnapshot> GetTargetInfoAsync(CancellationToken ct = default)
        => Task.FromResult(new TargetSnapshot { Name = "fake" });

    public Task<IReadOnlyList<ProcessSummary>> GetProcessListAsync(CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<ProcessSummary>>(Array.Empty<ProcessSummary>());

    public Task<ProcessSnapshot> GetActiveProcessAsync(CancellationToken ct = default)
        => Task.FromResult(new ProcessSnapshot { TitleId = "CUSA00000", Name = "fake-game" });

    public Task<IReadOnlyList<MemoryRegion>> GetVirtualMemoryMapsAsync(CancellationToken ct = default)
    {
        var region = new MemoryRegion
        {
            Name = "fake.eboot",
            Start = Base,
            End = Base + (ulong)Buffer.Length,
            Protection = MemoryProtection.ReadWrite,
        };
        return Task.FromResult<IReadOnlyList<MemoryRegion>>(new[] { region });
    }

    public Task<byte[]> ReadMemoryAsync(ulong address, uint length, CancellationToken ct = default)
    {
        if (ReadShouldFail) throw new InvalidOperationException("read fail");
        if (address < Base) throw new ArgumentOutOfRangeException(nameof(address));
        long offset = (long)(address - Base);
        if (offset >= Buffer.Length) return Task.FromResult(Array.Empty<byte>());
        long take = Math.Min((long)length, Buffer.Length - offset);
        var dst = new byte[take];
        Array.Copy(Buffer, (int)offset, dst, 0, (int)take);
        return Task.FromResult(dst);
    }

    public Task WriteMemoryAsync(ulong address, byte[] data, CancellationToken ct = default)
    {
        if (address < Base) throw new ArgumentOutOfRangeException(nameof(address));
        long offset = (long)(address - Base);
        if (offset + data.Length > Buffer.Length) throw new ArgumentOutOfRangeException(nameof(address));
        Array.Copy(data, 0, Buffer, (int)offset, data.Length);
        Writes.Add((address, (byte[])data.Clone()));
        return Task.CompletedTask;
    }

    public Task SetMemoryProtectionAsync(ulong address, uint length, MemoryProtection protection, CancellationToken ct = default)
        => Task.CompletedTask;

    public Task<AobScanResult?> AobScanAsync(AobScanRequest request, CancellationToken ct = default)
        => Task.FromResult<AobScanResult?>(null);

    public Task<PadStateSnapshot?> GetPadStateAsync(CancellationToken ct = default)
        => Task.FromResult<PadStateSnapshot?>(null);
}
