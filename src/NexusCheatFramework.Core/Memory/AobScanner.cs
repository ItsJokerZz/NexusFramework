using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NexusCheatFramework.Logging;
using NexusCheatFramework.Nexus;

namespace NexusCheatFramework.Memory
{
    /// <summary>
    /// Pattern/AOB scanner that operates over an <see cref="INexusClient"/> by
    /// reading memory in chunks. Handles overlap so matches that straddle a
    /// chunk boundary are not missed.
    /// </summary>
    public sealed class AobScanner
    {
        private readonly INexusClient _client;
        private readonly ILogger _log;

        public AobScanner(INexusClient client, ILogger? logger = null)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
            _log = logger ?? NullLogger.Instance;
        }

        public async Task<ulong?> FindFirstAsync(AobPattern pattern, AobScanOptions options, CancellationToken ct = default)
        {
            var hits = await ScanAsync(pattern, options, true, ct).ConfigureAwait(false);
            return hits.Count > 0 ? hits[0] : (ulong?)null;
        }

        public async Task<IReadOnlyList<ulong>> FindAllAsync(AobPattern pattern, AobScanOptions options, CancellationToken ct = default)
            => await ScanAsync(pattern, options, false, ct).ConfigureAwait(false);

        private async Task<List<ulong>> ScanAsync(AobPattern pattern, AobScanOptions options, bool firstOnly, CancellationToken ct)
        {
            if (pattern is null) throw new ArgumentNullException(nameof(pattern));
            if (options is null) throw new ArgumentNullException(nameof(options));
            if (options.ChunkSize <= (uint)pattern.Length)
                throw new ArgumentException("ChunkSize must exceed pattern length.", nameof(options));

            var regions = await ResolveRegionsAsync(options, ct).ConfigureAwait(false);
            var results = new List<ulong>();
            ulong scanned = 0;
            ulong totalBytes = (ulong)regions.Sum(r => (long)r.Length);

            foreach (var region in regions)
            {
                ct.ThrowIfCancellationRequested();
                var regionStart = options.Start.HasValue ? Math.Max(region.Start, options.Start.Value) : region.Start;
                var regionEnd = options.End.HasValue ? Math.Min(region.End, options.End.Value) : region.End;
                if (regionEnd <= regionStart) continue;

                _log.Debug($"[AOB] Scanning {region.Name} 0x{regionStart:X}-0x{regionEnd:X} ({regionEnd - regionStart} bytes)");

                ulong cursor = regionStart;
                while (cursor < regionEnd)
                {
                    ct.ThrowIfCancellationRequested();

                    ulong remaining = regionEnd - cursor;
                    // Read a chunk + (patternLen-1) overlap so cross-boundary matches are caught,
                    // but never read past regionEnd.
                    ulong wantRead = Math.Min((ulong)options.ChunkSize + (ulong)(pattern.Length - 1), remaining);
                    if (wantRead < (ulong)pattern.Length) break;

                    byte[]? buffer = null;
                    try
                    {
                        buffer = await _client.ReadMemoryAsync(cursor, (uint)wantRead, ct).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        if (options.Strict) throw;
                        _log.Warn($"[AOB] read failed at 0x{cursor:X}: {ex.Message}");
                    }

                    if (buffer == null || buffer.Length < pattern.Length)
                    {
                        cursor += (ulong)options.ChunkSize;
                        continue;
                    }

                    int searchLimit = buffer.Length - pattern.Length;
                    for (int i = 0; i <= searchLimit; i++)
                    {
                        if (pattern.FirstSolidIndex >= 0 && buffer[i + pattern.FirstSolidIndex] != pattern.FirstSolidValue)
                            continue;
                        if (!pattern.Matches(buffer, i)) continue;

                        ulong matchAddr = cursor + (ulong)i;
                        results.Add(matchAddr);
                        _log.Debug($"[AOB] match @ 0x{matchAddr:X}");
                        if (firstOnly || (options.MaxResults.HasValue && results.Count >= options.MaxResults.Value))
                            return results;
                    }

                    // Advance by chunk size (not buffer length) so the (pattern.Length-1)
                    // overlap is preserved on the next iteration.
                    cursor += (ulong)options.ChunkSize;
                    scanned += (ulong)options.ChunkSize;
                    options.Progress?.Invoke(Math.Min(scanned, totalBytes), totalBytes);
                }
            }

            return results;
        }

        private async Task<List<MemoryRegion>> ResolveRegionsAsync(AobScanOptions options, CancellationToken ct)
        {
            var maps = await _client.GetVirtualMemoryMapsAsync(ct).ConfigureAwait(false);
            var filtered = new List<MemoryRegion>();
            foreach (var m in maps)
            {
                if (options.ScanReadableOnly && !m.IsReadable) continue;
                if (options.ScanExecutableOnly && !m.IsExecutable) continue;
                if (!string.IsNullOrEmpty(options.RegionNameContains) &&
                    (m.Name?.IndexOf(options.RegionNameContains!, StringComparison.OrdinalIgnoreCase) ?? -1) < 0)
                    continue;
                if (m.End <= m.Start) continue;
                filtered.Add(m);
            }
            return filtered;
        }
    }
}
