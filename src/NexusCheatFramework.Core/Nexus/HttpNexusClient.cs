using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace NexusCheatFramework.Nexus
{
    /// <summary>
    /// Lightweight <see cref="INexusClient"/> backed by direct HTTP calls to the
    /// NexusFramework payload's HTTP API. Provided so command-line tools and
    /// tests can run without a hard dependency on the upstream NexusFramework
    /// C# library (which embeds payload .elf binaries).
    ///
    /// Endpoints used (documented in docs/NEXUS_API_ENDPOINTS.md):
    ///   GET  /status, /version, /connect, /disconnect
    ///   GET  /get_sys_info, /get_proc_list, /get_proc_info, /get_vm_maps
    ///   GET  /read_memory?address=0xADDR&amp;length=N      (returns raw bytes)
    ///   POST /write_memory  body { address, data }
    ///   GET  /memory_protection?address=...&amp;length=...&amp;prot=...
    /// </summary>
    public sealed class HttpNexusClient : INexusClient, IDisposable
    {
        private readonly HttpClient _http;
        private readonly int _port;
        private string? _ip;
        private bool _connected;
        private bool _ownsHttp;

        public HttpNexusClient(string ipAddress, int port = 9090, HttpClient? http = null)
        {
            _ip = ipAddress;
            _port = port;
            _http = http ?? new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
            _ownsHttp = http is null;
        }

        public bool Connected => _connected;
        public string? IpAddress => _ip;

        private string Url(string path, string? query = null)
        {
            var sb = new StringBuilder().Append("http://").Append(_ip).Append(':').Append(_port).Append('/').Append(path);
            if (!string.IsNullOrEmpty(query)) sb.Append('?').Append(query);
            return sb.ToString();
        }

        public async Task ConnectAsync(string ipAddress, CancellationToken ct = default)
        {
            _ip = ipAddress;
            await _http.GetStringAsync(Url("connect")).ConfigureAwait(false);
            _connected = true;
        }

        public async Task DisconnectAsync(CancellationToken ct = default)
        {
            try { await _http.GetStringAsync(Url("disconnect")).ConfigureAwait(false); }
            finally { _connected = false; }
        }

        public async Task<TargetSnapshot> GetTargetInfoAsync(CancellationToken ct = default)
        {
            var doc = await GetJsonAsync("get_sys_info", ct).ConfigureAwait(false);
            var r = doc.RootElement;
            return new TargetSnapshot
            {
                Name = GetString(r, "NAME"),
                ConsoleType = GetString(r, "TYPE"),
                Firmware = (float)(GetDouble(r, "FW") ?? 0),
                Model = GetString(r, "MODEL"),
            };
        }

        public async Task<IReadOnlyList<ProcessSummary>> GetProcessListAsync(CancellationToken ct = default)
        {
            var doc = await GetJsonAsync("get_proc_list", ct).ConfigureAwait(false);
            var list = new List<ProcessSummary>();
            if (doc.RootElement.TryGetProperty("LIST", out var arr) && arr.ValueKind == JsonValueKind.Array)
            {
                foreach (var i in arr.EnumerateArray())
                    list.Add(new ProcessSummary
                    {
                        AppId = GetInt(i, "AID") ?? 0,
                        Pid = GetInt(i, "PID") ?? 0,
                        Executable = GetString(i, "EXEC"),
                        TitleId = GetString(i, "TID"),
                    });
            }
            return list;
        }

        public async Task<ProcessSnapshot> GetActiveProcessAsync(CancellationToken ct = default)
        {
            var doc = await GetJsonAsync("get_proc_info", ct).ConfigureAwait(false);
            var r = doc.RootElement;
            var maps = await GetVirtualMemoryMapsAsync(ct).ConfigureAwait(false);
            return new ProcessSnapshot
            {
                AppId = GetInt(r, "AID") ?? 0,
                Pid = GetInt(r, "PID") ?? 0,
                TitleId = GetString(r, "TID"),
                Name = GetString(r, "NAME"),
                Region = GetString(r, "REGION"),
                Executable = GetString(r, "EXEC"),
                Version = (float)(GetDouble(r, "VER") ?? 0),
                AppType = GetString(r, "TYPE"),
                MemoryMaps = maps,
            };
        }

        public async Task<IReadOnlyList<MemoryRegion>> GetVirtualMemoryMapsAsync(CancellationToken ct = default)
        {
            var doc = await GetJsonAsync("get_vm_maps", ct).ConfigureAwait(false);
            var list = new List<MemoryRegion>();
            if (doc.RootElement.TryGetProperty("maps", out var arr) && arr.ValueKind == JsonValueKind.Array)
            {
                foreach (var e in arr.EnumerateArray())
                    list.Add(new MemoryRegion
                    {
                        Name = GetString(e, "NAME"),
                        Start = GetUlong(e, "START") ?? 0,
                        End = GetUlong(e, "END") ?? 0,
                        Offset = GetUlong(e, "OFFSET") ?? 0,
                        Protection = (MemoryProtection)(GetUint(e, "PROT") ?? 0),
                    });
            }
            return list;
        }

        public async Task<byte[]> ReadMemoryAsync(ulong address, uint length, CancellationToken ct = default)
        {
            var url = Url("read_memory", $"address=0x{address:X}&length={length}");
            using var resp = await _http.GetAsync(url, ct).ConfigureAwait(false);
            resp.EnsureSuccessStatusCode();
            return await resp.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
        }

        public async Task WriteMemoryAsync(ulong address, byte[] data, CancellationToken ct = default)
        {
            var body = JsonSerializer.Serialize(new
            {
                address = $"0x{address:X}",
                data = BitConverter.ToString(data).Replace("-", "").ToLowerInvariant(),
            });
            using var content = new StringContent(body, Encoding.UTF8, "application/json");
            using var resp = await _http.PostAsync(Url("write_memory"), content, ct).ConfigureAwait(false);
            resp.EnsureSuccessStatusCode();
        }

        public async Task SetMemoryProtectionAsync(ulong address, uint length, MemoryProtection protection, CancellationToken ct = default)
        {
            var url = Url("memory_protection", $"address=0x{address:X}&length={length}&prot={(uint)protection}");
            using var resp = await _http.GetAsync(url, ct).ConfigureAwait(false);
            resp.EnsureSuccessStatusCode();
        }

        private async Task<JsonDocument> GetJsonAsync(string path, CancellationToken ct)
        {
            using var resp = await _http.GetAsync(Url(path), ct).ConfigureAwait(false);
            resp.EnsureSuccessStatusCode();
            using var s = await resp.Content.ReadAsStreamAsync().ConfigureAwait(false);
            using var ms = new MemoryStream();
            await s.CopyToAsync(ms).ConfigureAwait(false);
            ms.Position = 0;
            return JsonDocument.Parse(ms);
        }

        private static string GetString(JsonElement parent, string name)
        {
            if (!parent.TryGetProperty(name, out var v)) return string.Empty;
            return v.ValueKind switch
            {
                JsonValueKind.String => v.GetString() ?? string.Empty,
                JsonValueKind.Number => v.ToString(),
                _ => v.ToString(),
            };
        }
        private static int? GetInt(JsonElement p, string n) => p.TryGetProperty(n, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetInt32() : (int?)null;
        private static uint? GetUint(JsonElement p, string n) => p.TryGetProperty(n, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetUInt32() : (uint?)null;
        private static ulong? GetUlong(JsonElement p, string n)
        {
            if (!p.TryGetProperty(n, out var v)) return null;
            if (v.ValueKind == JsonValueKind.Number) return v.GetUInt64();
            if (v.ValueKind == JsonValueKind.String)
            {
                var s = v.GetString();
                if (string.IsNullOrEmpty(s)) return null;
                if (s!.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                    return ulong.Parse(s.Substring(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                return ulong.Parse(s, CultureInfo.InvariantCulture);
            }
            return null;
        }
        private static double? GetDouble(JsonElement p, string n) => p.TryGetProperty(n, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetDouble() : (double?)null;

        // ----- Payload-side AOB scan (optional) -----
        public async Task<AobScanResult?> AobScanAsync(AobScanRequest request, CancellationToken ct = default)
        {
            try
            {
                var body = JsonSerializer.Serialize(new
                {
                    pattern = request.Pattern,
                    start = request.Start,
                    end = request.End,
                    max_results = request.MaxResults,
                    readable_only = request.ReadableOnly,
                    executable_only = request.ExecutableOnly,
                    region_name_contains = request.RegionNameContains
                });
                using var content = new StringContent(body, Encoding.UTF8, "application/json");
                using var resp = await _http.PostAsync(Url("aob_scan"), content, ct).ConfigureAwait(false);
                if (!resp.IsSuccessStatusCode) return null;
                using var s = await resp.Content.ReadAsStreamAsync().ConfigureAwait(false);
                var doc = await JsonDocument.ParseAsync(s, cancellationToken: ct).ConfigureAwait(false);
                var r = doc.RootElement;
                var matches = new List<string>();
                if (r.TryGetProperty("matches", out var mArr) && mArr.ValueKind == JsonValueKind.Array)
                {
                    foreach (var m in mArr.EnumerateArray())
                        matches.Add(m.GetString() ?? string.Empty);
                }
                return new AobScanResult(
                    matches,
                    GetInt(r, "scanned_regions") ?? 0,
                    GetInt(r, "skipped_regions") ?? 0,
                    GetLong(r, "elapsed_ms") ?? 0
                );
            }
            catch { return null; }
        }

        // ----- Payload-side pad state (optional) -----
        public async Task<PadStateSnapshot?> GetPadStateAsync(CancellationToken ct = default)
        {
            try
            {
                var doc = await GetJsonAsync("pad_state", ct).ConfigureAwait(false);
                var r = doc.RootElement;
                return new PadStateSnapshot(
                    GetBool(r, "connected") ?? false,
                    GetUint(r, "buttons") ?? 0,
                    (byte)(GetInt(r, "lx") ?? 128),
                    (byte)(GetInt(r, "ly") ?? 128),
                    (byte)(GetInt(r, "rx") ?? 128),
                    (byte)(GetInt(r, "ry") ?? 128),
                    (byte)(GetInt(r, "l2") ?? 0),
                    (byte)(GetInt(r, "r2") ?? 0),
                    GetLong(r, "timestamp") ?? 0
                );
            }
            catch { return null; }
        }

        private static long? GetLong(JsonElement p, string n)
        {
            if (!p.TryGetProperty(n, out var v)) return null;
            if (v.ValueKind == JsonValueKind.Number) return v.GetInt64();
            return null;
        }

        private static bool? GetBool(JsonElement p, string n)
        {
            if (!p.TryGetProperty(n, out var v)) return null;
            return v.ValueKind switch
            {
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                _ => null
            };
        }

        public void Dispose()
        {
            if (_ownsHttp) _http.Dispose();
        }
    }
}
