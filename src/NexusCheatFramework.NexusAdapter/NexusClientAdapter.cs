using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NexusCheatFramework.Nexus;

namespace NexusCheatFramework.Nexus.Adapter
{
    /// <summary>
    /// Concrete <see cref="INexusClient"/> backed by the upstream
    /// <see cref="NexusFramework.Library"/>. All addresses round-trip as ulong;
    /// memory protection flags are translated bit-for-bit since the upstream
    /// uses the same numeric layout.
    /// </summary>
    public sealed class NexusClientAdapter : INexusClient
    {
        private readonly NexusFramework.Library _lib;

        public NexusClientAdapter(NexusFramework.Library? lib = null)
        {
            _lib = lib ?? new NexusFramework.Library();
        }

        public bool Connected => _lib.Target.Connected;
        public string? IpAddress => _lib.Target.IP;

        public async Task ConnectAsync(string ipAddress, CancellationToken ct = default)
            => await _lib.Connect(ipAddress).ConfigureAwait(false);

        public async Task DisconnectAsync(CancellationToken ct = default)
            => await _lib.Disconnect().ConfigureAwait(false);

        public async Task<TargetSnapshot> GetTargetInfoAsync(CancellationToken ct = default)
        {
            await _lib.GetTargetInfo().ConfigureAwait(false);
            var t = _lib.Target;
            return new TargetSnapshot
            {
                Name = t.Name,
                ConsoleType = t.ConsoleType,
                Firmware = t.Firmware,
                Model = t.Model,
            };
        }

        public async Task<IReadOnlyList<ProcessSummary>> GetProcessListAsync(CancellationToken ct = default)
        {
            await _lib.GetProcessList().ConfigureAwait(false);
            return _lib.ProcessList.Select(p => new ProcessSummary
            {
                AppId = p.AppId, Pid = p.PID, Executable = p.Exec, TitleId = p.TitleId,
            }).ToList();
        }

        public async Task<ProcessSnapshot> GetActiveProcessAsync(CancellationToken ct = default)
        {
            await _lib.GetProcessInfo().ConfigureAwait(false);
            var p = _lib.Process;
            var maps = (p.MemoryMaps ?? Array.Empty<NexusFramework.Definitions.MemoryEntry>())
                .Select(MapRegion).ToList();
            return new ProcessSnapshot
            {
                AppId = p.AppID, Pid = p.PID, TitleId = p.TitleID, Name = p.Name,
                Region = p.Region, Executable = p.Executable, Version = p.Version,
                AppType = p.AppType, MemoryMaps = maps,
            };
        }

        public async Task<IReadOnlyList<MemoryRegion>> GetVirtualMemoryMapsAsync(CancellationToken ct = default)
        {
            var maps = await _lib.GetVirtualMemoryMaps().ConfigureAwait(false);
            return (maps ?? Array.Empty<NexusFramework.Definitions.MemoryEntry>())
                .Select(MapRegion).ToList();
        }

        public Task<byte[]> ReadMemoryAsync(ulong address, uint length, CancellationToken ct = default)
            => _lib.ReadMemory<byte[]>(address, length);

        public async Task WriteMemoryAsync(ulong address, byte[] data, CancellationToken ct = default)
            => await _lib.WriteMemory(address, data).ConfigureAwait(false);

        public async Task SetMemoryProtectionAsync(ulong address, uint length, MemoryProtection protection, CancellationToken ct = default)
            => await _lib.SetMemoryProtection(address, length, (NexusFramework.Definitions.MemoryProtection)(uint)protection).ConfigureAwait(false);

        private static MemoryRegion MapRegion(NexusFramework.Definitions.MemoryEntry e) => new()
        {
            Name = e.Name, Start = e.Start, End = e.End, Offset = e.Offset,
            Protection = (MemoryProtection)e.Protection,
        };
    }
}
