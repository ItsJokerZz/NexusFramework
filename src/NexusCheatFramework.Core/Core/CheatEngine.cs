using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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
    /// and structured result reporting.
    /// </summary>
    public sealed class CheatEngine
    {
        private readonly INexusClient _client;
        private readonly ILogger _log;
        private readonly AobScanner _scanner;
        private readonly ConcurrentDictionary<string, List<MemoryPatch>> _enabled = new();

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
                return new CheatResult { CheatId = cheat.Id, Success = true, Status = CheatStatus.Enabled, AppliedCodes = report };
            }
            catch (Exception ex)
            {
                // Roll back any patches that were already written for this cheat.
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
            if (!_enabled.TryRemove(cheatId, out var patches))
                return new CheatResult { CheatId = cheatId, Success = true, Status = CheatStatus.Disabled };

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

        private async Task<(ulong address, byte[] patchBytes)> ResolveAsync(CheatCode code, CancellationToken ct)
        {
            byte[] patchBytes = code.Type switch
            {
                CheatCodeType.WriteBytes      => code.Bytes ?? throw new FormatException("WriteBytes requires 'bytes'."),
                CheatCodeType.AobWriteBytes   => code.Bytes ?? throw new FormatException("AobWriteBytes requires 'bytes'."),
                CheatCodeType.ModuleWriteBytes=> code.Bytes ?? throw new FormatException("ModuleWriteBytes requires 'bytes'."),
                CheatCodeType.WriteValue      => TypedValueEncoder.Encode(code.ValueType, code.Value),
                CheatCodeType.AobWriteValue   => TypedValueEncoder.Encode(code.ValueType, code.Value),
                _ => throw new NotSupportedException($"Cheat code type {code.Type} not supported."),
            };

            ulong baseAddr = code.Type switch
            {
                CheatCodeType.WriteBytes or CheatCodeType.WriteValue
                    => code.Address ?? throw new FormatException("Static write requires 'address'."),
                CheatCodeType.AobWriteBytes or CheatCodeType.AobWriteValue
                    => await ResolveAobAsync(code.AobPattern, ct).ConfigureAwait(false),
                CheatCodeType.ModuleWriteBytes
                    => await ResolveModuleBaseAsync(code.ModuleName).ConfigureAwait(false),
                _ => throw new NotSupportedException(),
            };

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

            // Reject mode: cap at 2 hits — that's enough to detect ambiguity
            // without scanning the entire address space when there are many matches.
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
                throw new FormatException("ModuleWriteBytes requires 'moduleName'.");
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

        private static string ToHex(byte[] b)
        {
            var sb = new StringBuilder(b.Length * 3);
            for (int i = 0; i < b.Length; i++) { if (i > 0) sb.Append(' '); sb.Append(b[i].ToString("X2")); }
            return sb.ToString();
        }
    }
}
