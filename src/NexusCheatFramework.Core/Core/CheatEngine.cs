using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NexusCheatFramework.Formats;
using NexusCheatFramework.Logging;
using NexusCheatFramework.Memory;
using NexusCheatFramework.Nexus;

namespace NexusCheatFramework.Core
{
    /// <summary>
    /// Applies cheat definitions against a connected <see cref="INexusClient"/>.
    /// Stores original bytes, supports restore on disable, AOB resolution,
    /// pointer chains, freeze loops, and structured result reporting.
    /// </summary>
    public sealed class CheatEngine
    {
        private readonly INexusClient _client;
        private readonly ILogger _log;
        private readonly AobScanner _scanner;
        private readonly ConcurrentDictionary<string, List<MemoryPatch>> _enabled = new();
        private readonly ConcurrentDictionary<string, FreezeState> _freezes = new();
        private readonly ConcurrentDictionary<string, CancellationTokenSource> _freezeCts = new();

        public CheatRuntimeOptions Options { get; }

        public CheatEngine(INexusClient client, CheatRuntimeOptions? options = null, ILogger? logger = null)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
            _log = logger ?? NullLogger.Instance;
            Options = options ?? new CheatRuntimeOptions();
            _scanner = new AobScanner(_client, _log);
        }

        public IReadOnlyCollection<string> EnabledCheatIds => _enabled.Keys.ToArray();
        public bool IsEnabled(string cheatId) => _enabled.ContainsKey(cheatId);

        public async Task<CheatResult> EnableAsync(CheatDefinition cheat, CancellationToken ct = default)
        {
            if (cheat is null) throw new ArgumentNullException(nameof(cheat));
            if (_enabled.ContainsKey(cheat.Id))
                return new CheatResult { CheatId = cheat.Id, Success = true, Status = CheatStatus.Enabled };

            var applied = new List<MemoryPatch>();
            var report = new List<AppliedCode>();
            try
            {
                foreach (var code in cheat.Codes)
                {
                    ct.ThrowIfCancellationRequested();

                    var (address, patchBytes) = await ResolveAsync(code, ct).ConfigureAwait(false);

                    var original = await _client.ReadMemoryAsync(address, (uint)patchBytes.Length, ct).ConfigureAwait(false);
                    if (original is null || original.Length != patchBytes.Length)
                        throw new InvalidOperationException($"Failed to read {patchBytes.Length} bytes at 0x{address:X}.");

                    if (code.ExpectedBytes != null)
                    {
                        bool ok = code.ExpectedBytes.Length == original.Length
                                  && original.SequenceEqual(code.ExpectedBytes);
                        if (!ok)
                        {
                            string msg = $"ExpectedBytes mismatch at 0x{address:X}. " +
                                         $"got={ToHex(original)} expected={ToHex(code.ExpectedBytes)}";
                            if (Options.RequireExpectedBytes && !Options.ForceApply)
                                throw new InvalidOperationException(msg);
                            _log.Warn(msg + " (force-applying)");
                        }
                    }

                    var patch = new MemoryPatch(address, patchBytes);
                    if (!Options.DryRun)
                    {
                        await _client.WriteMemoryAsync(address, patchBytes, ct).ConfigureAwait(false);
                    }
                    patch.MarkApplied(original);
                    applied.Add(patch);
                    report.Add(new AppliedCode
                    {
                        Address = address,
                        PatchBytes = patchBytes,
                        OriginalBytes = original,
                        Note = Options.DryRun ? "dry-run" : null,
                    });
                }

                _enabled[cheat.Id] = applied;

                // Start any freeze loops with the resolved address and patch bytes
                foreach (var item in cheat.Codes.Zip(applied, (code, patch) => new { code, patch }))
                {
                    if (item.code.Type == CheatCodeType.FreezeValue)
                    {
                        StartFreeze(cheat.Id, item.patch.Address, item.patch.PatchBytes, item.code.FreezeIntervalMs);
                    }
                }

                return new CheatResult { CheatId = cheat.Id, Success = true, Status = CheatStatus.Enabled, AppliedCodes = report };
            }
            catch (Exception ex)
            {
                foreach (var p in applied)
                {
                    if (!Options.DryRun && p.OriginalBytes != null)
                    {
                        try { await _client.WriteMemoryAsync(p.Address, p.OriginalBytes, ct).ConfigureAwait(false); }
                        catch (Exception rex) { _log.Error($"Rollback failed at 0x{p.Address:X}", rex); }
                    }
                }
                _log.Error($"Enable failed for cheat '{cheat.Id}': {ex.Message}", ex);
                return new CheatResult { CheatId = cheat.Id, Success = false, Status = CheatStatus.Failed, Error = ex.Message };
            }
        }

        public async Task<CheatResult> DisableAsync(string cheatId, CancellationToken ct = default)
        {
            // Stop any freeze loops
            StopFreeze(cheatId);

            if (!_enabled.TryRemove(cheatId, out var patches))
                return new CheatResult { CheatId = cheatId, Success = true, Status = CheatStatus.Disabled };

            _freezes.TryRemove(cheatId, out _);

            var errors = new List<string>();
            if (Options.RestoreOriginalBytesOnDisable)
            {
                foreach (var p in patches)
                {
                    if (p.OriginalBytes == null) continue;
                    try
                    {
                        if (!Options.DryRun)
                            await _client.WriteMemoryAsync(p.Address, p.OriginalBytes, ct).ConfigureAwait(false);
                        p.MarkRestored();
                    }
                    catch (Exception ex)
                    {
                        var msg = $"Failed to restore 0x{p.Address:X}: {ex.Message}";
                        _log.Error(msg, ex);
                        errors.Add(msg);
                    }
                }
            }

            return new CheatResult
            {
                CheatId = cheatId,
                Success = errors.Count == 0,
                Status = errors.Count == 0 ? CheatStatus.Disabled : CheatStatus.Failed,
                Error = errors.Count == 0 ? null : string.Join("; ", errors),
            };
        }

        public void StopAllFreezes()
        {
            foreach (var kvp in _freezeCts)
            {
                try
                {
                    kvp.Value.Cancel();
                    kvp.Value.Dispose();
                }
                catch { /* best effort */ }
            }
            _freezeCts.Clear();
            _freezes.Clear();
        }

        private void StartFreeze(string cheatId, ulong address, byte[] patchBytes, int freezeIntervalMs)
        {
            if (_freezeCts.ContainsKey(cheatId))
            {
                _log.Debug($"Freeze already running for '{cheatId}'");
                return;
            }

            var cts = new CancellationTokenSource();
            _freezeCts[cheatId] = cts;

            var interval = freezeIntervalMs > 0 ? freezeIntervalMs : 250;

            _freezes[cheatId] = new FreezeState { Address = address, PatchBytes = patchBytes };

            _ = Task.Run(async () =>
            {
                try
                {
                    while (!cts.Token.IsCancellationRequested)
                    {
                        try
                        {
                            await _client.WriteMemoryAsync(address, patchBytes, cts.Token).ConfigureAwait(false);
                        }
                        catch (OperationCanceledException) { break; }
                        catch (Exception ex)
                        {
                            _log.Warn($"Freeze write failed for '{cheatId}': {ex.Message}");
                            // Check if process disconnected
                            if (!_client.Connected) break;
                        }
                        await Task.Delay(interval, cts.Token).ConfigureAwait(false);
                    }
                }
                catch (OperationCanceledException) { }
                catch (Exception ex)
                {
                    _log.Error($"Freeze loop terminated for '{cheatId}': {ex.Message}", ex);
                }
            }, cts.Token);
        }

        private void StopFreeze(string cheatId)
        {
            if (_freezeCts.TryRemove(cheatId, out var cts))
            {
                try
                {
                    cts.Cancel();
                    cts.Dispose();
                }
                catch { /* best effort */ }
            }
        }

        private async Task<(ulong address, byte[] patchBytes)> ResolveAsync(CheatCode code, CancellationToken ct)
        {
            byte[] patchBytes;
            ulong baseAddr;

            switch (code.Type)
            {
                case CheatCodeType.FreezeValue:
                    // Freeze supports dynamic resolution: module, AOB, pointer chain
                    patchBytes = TypedValueEncoder.Encode(code.ValueType, code.Value);
                    if (code.PointerOffsets is { Count: > 0 })
                    {
                        if (!string.IsNullOrWhiteSpace(code.AobPattern))
                            baseAddr = await ResolveAobPointerChainAsync(code.AobPattern, code.AobOffset, code.PointerOffsets, ct).ConfigureAwait(false);
                        else if (code.Address.HasValue)
                            baseAddr = await ResolvePointerChainAsync(code.Address, code.PointerOffsets, ct).ConfigureAwait(false);
                        else
                            throw new FormatException("FreezeValue with pointerOffsets requires 'address' or 'aobPattern'.");
                    }
                    else if (!string.IsNullOrWhiteSpace(code.AobPattern))
                    {
                        baseAddr = await ResolveAobAsync(code.AobPattern, ct).ConfigureAwait(false);
                    }
                    else if (!string.IsNullOrWhiteSpace(code.ModuleName))
                    {
                        baseAddr = await ResolveModuleBaseAsync(code.ModuleName).ConfigureAwait(false);
                    }
                    else
                    {
                        baseAddr = code.Address ?? throw new FormatException("FreezeValue requires 'address', 'moduleName', 'aobPattern', or 'pointerOffsets'.");
                    }
                    break;

                case CheatCodeType.WriteBytes:
                case CheatCodeType.ModuleWriteBytes:
                    patchBytes = code.Bytes ?? throw new FormatException($"{code.Type} requires 'bytes'.");
                    baseAddr = code.Type == CheatCodeType.WriteBytes
                        ? (code.Address ?? throw new FormatException("Static write requires 'address'."))
                        : await ResolveModuleBaseAsync(code.ModuleName).ConfigureAwait(false);
                    break;

                case CheatCodeType.WriteValue:
                case CheatCodeType.ModuleWriteValue:
                    patchBytes = TypedValueEncoder.Encode(code.ValueType, code.Value);
                    baseAddr = code.Type == CheatCodeType.WriteValue
                        ? (code.Address ?? throw new FormatException("WriteValue requires 'address'."))
                        : await ResolveModuleBaseAsync(code.ModuleName).ConfigureAwait(false);
                    break;

                case CheatCodeType.AobWriteBytes:
                    patchBytes = code.Bytes ?? throw new FormatException("AobWriteBytes requires 'bytes'.");
                    baseAddr = await ResolveAobAsync(code.AobPattern, ct).ConfigureAwait(false);
                    break;

                case CheatCodeType.AobWriteValue:
                    patchBytes = TypedValueEncoder.Encode(code.ValueType, code.Value);
                    baseAddr = await ResolveAobAsync(code.AobPattern, ct).ConfigureAwait(false);
                    break;

                case CheatCodeType.PointerWriteBytes:
                    patchBytes = code.Bytes ?? throw new FormatException("PointerWriteBytes requires 'bytes'.");
                    baseAddr = await ResolvePointerChainAsync(code.Address, code.PointerOffsets, ct).ConfigureAwait(false);
                    break;

                case CheatCodeType.PointerWriteValue:
                    patchBytes = TypedValueEncoder.Encode(code.ValueType, code.Value);
                    baseAddr = await ResolvePointerChainAsync(code.Address, code.PointerOffsets, ct).ConfigureAwait(false);
                    break;

                case CheatCodeType.AobPointerWriteBytes:
                    patchBytes = code.Bytes ?? throw new FormatException("AobPointerWriteBytes requires 'bytes'.");
                    baseAddr = await ResolveAobPointerChainAsync(code.AobPattern, code.AobOffset, code.PointerOffsets, ct).ConfigureAwait(false);
                    break;

                case CheatCodeType.AobPointerWriteValue:
                    patchBytes = TypedValueEncoder.Encode(code.ValueType, code.Value);
                    baseAddr = await ResolveAobPointerChainAsync(code.AobPattern, code.AobOffset, code.PointerOffsets, ct).ConfigureAwait(false);
                    break;

                default:
                    throw new NotSupportedException($"Cheat code type {code.Type} not supported.");
            }

            ulong address = unchecked(baseAddr + (ulong)code.Offset);
            return (address, patchBytes);
        }

        private async Task<ulong> ResolveAobAsync(string? patternStr, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(patternStr))
                throw new FormatException("AOB cheat requires 'aobPattern'.");
            var pattern = AobPattern.Parse(patternStr!);

            if (Options.AmbiguousMatchPolicy == AmbiguousMatchPolicy.FirstMatch)
            {
                var hit = await _scanner.FindFirstAsync(pattern, new AobScanOptions { ScanReadableOnly = true }, ct).ConfigureAwait(false);
                if (hit == null) throw new InvalidOperationException($"AOB pattern not found: {patternStr}");
                return hit.Value;
            }

            var opts = new AobScanOptions { ScanReadableOnly = true, MaxResults = 2 };
            var hits = await _scanner.FindAllAsync(pattern, opts, ct).ConfigureAwait(false);
            if (hits.Count == 0)
                throw new InvalidOperationException($"AOB pattern not found: {patternStr}");
            if (hits.Count > 1)
                throw new InvalidOperationException(
                    $"AOB pattern matched multiple addresses; refine pattern or set AmbiguousMatchPolicy=FirstMatch.");
            return hits[0];
        }

        private async Task<ulong> ResolveModuleBaseAsync(string? moduleName)
        {
            if (string.IsNullOrWhiteSpace(moduleName))
                throw new FormatException("ModuleWrite requires 'moduleName'.");
            var maps = await _client.GetVirtualMemoryMapsAsync().ConfigureAwait(false);
            var match = maps
                .Where(m => !string.IsNullOrEmpty(m.Name) &&
                            m.Name.IndexOf(moduleName!, StringComparison.OrdinalIgnoreCase) >= 0 &&
                            m.IsExecutable)
                .OrderBy(m => m.Start)
                .FirstOrDefault();
            if (match == null)
                throw new InvalidOperationException($"Module '{moduleName}' not found in memory maps.");
            return match.Start;
        }

        /// <summary>
        /// Resolves a pointer chain: read ulong at each address, add next offset, repeat.
        /// Supports module: prefix for the base address.
        /// </summary>
        private async Task<ulong> ResolvePointerChainAsync(ulong? baseAddress, List<string>? offsets, CancellationToken ct)
        {
            if (!baseAddress.HasValue || baseAddress.Value == 0)
                throw new FormatException("Pointer chain requires non-zero 'address'.");
            if (offsets == null || offsets.Count == 0)
                throw new FormatException("Pointer chain requires at least one 'pointerOffsets' entry.");

            ulong current = baseAddress.Value;
            var offsetValues = offsets.Select(o => ParseOffset(o)).ToList();

            for (int i = 0; i < offsets.Count; i++)
            {
                if (i == offsets.Count - 1)
                {
                    // Last offset: add and return as final address
                    current = unchecked(current + (ulong)offsetValues[i]);
                }
                else
                {
                    // Read pointer from current + offset
                    ulong readAddr = unchecked(current + (ulong)offsetValues[i]);
                    var bytes = await _client.ReadMemoryAsync(readAddr, 8, ct).ConfigureAwait(false);
                    if (bytes == null || bytes.Length < 8)
                        throw new InvalidOperationException($"Pointer chain: failed to read 8 bytes at 0x{readAddr:X}");
                    if (BitConverter.IsLittleEndian && false) { } // always LE on PS
                    current = BitConverter.ToUInt64(bytes, 0);
                    if (current == 0)
                        throw new InvalidOperationException($"Pointer chain: null pointer at offset {i}, address 0x{readAddr:X}");
                }
            }

            return current;
        }

        /// <summary>
        /// Resolves an AOB pattern match, then applies pointer chain from match + aobOffset.
        /// </summary>
        private async Task<ulong> ResolveAobPointerChainAsync(string? aobPattern, int aobOffset, List<string>? pointerOffsets, CancellationToken ct)
        {
            var aobBase = await ResolveAobAsync(aobPattern, ct).ConfigureAwait(false);
            ulong chainStart = unchecked(aobBase + (ulong)aobOffset);
            return await ResolvePointerChainAsync(chainStart, pointerOffsets, ct).ConfigureAwait(false);
        }

        private long ParseOffset(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return 0;
            s = s.Trim();
            bool neg = s.StartsWith("-", StringComparison.Ordinal);
            if (neg) s = s.Substring(1);
            long val;
            if (s.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                val = long.Parse(s.Substring(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            else
                val = long.Parse(s, CultureInfo.InvariantCulture);
            return neg ? -val : val;
        }

        private static string ToHex(byte[] b)
        {
            var sb = new System.Text.StringBuilder(b.Length * 3);
            for (int i = 0; i < b.Length; i++) { if (i > 0) sb.Append(' '); sb.Append(b[i].ToString("X2")); }
            return sb.ToString();
        }

        private sealed class FreezeState
        {
            public ulong Address { get; init; }
            public byte[] PatchBytes { get; init; } = Array.Empty<byte>();
        }
    }
}
