using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.FileProviders;
using NexusCheatFramework.Core;
using NexusCheatFramework.Input;
using NexusCheatFramework.Logging;
using NexusCheatFramework.Memory;
using NexusCheatFramework.Nexus;
using NexusCheatFramework.Services;

namespace NexusCheatFramework.Web;

internal static class Program
{
    public static async Task<int> Main(string[] args)
    {
        string consoleIp = "192.168.1.1";
        int webPort = 9080;
        int consolePort = 9090;
        string cheatDb = "./cheats";
        bool verbose = false;

        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "--ip" && i + 1 < args.Length) consoleIp = args[++i];
            if (args[i] == "--port" && i + 1 < args.Length) webPort = int.Parse(args[++i]);
            if (args[i] == "--console-port" && i + 1 < args.Length) consolePort = int.Parse(args[++i]);
            if (args[i] == "--db" && i + 1 < args.Length) cheatDb = args[++i];
            if (args[i] == "--verbose") verbose = true;
        }

        await RunServerAsync(consoleIp, webPort, consolePort, cheatDb, verbose);
        return 0;
    }

    private static async Task RunServerAsync(string consoleIp, int webPort, int consolePort, string cheatDb, bool verbose)
    {
        var logger = new WebUILogger(verbose);
        INexusClient? client = null;
        CheatManager? manager = null;
        CheatManagerSession? session = null;
        ShortcutConfigStore? shortcutStore = null;
        var cts = new CancellationTokenSource();

        var builder = WebApplication.CreateBuilder();
        var app = builder.Build();

        // Serve static files from menu/webui
        var webuiPath = FindWebuiPath();
        if (webuiPath != null && Directory.Exists(webuiPath))
        {
            app.UseDefaultFiles(new DefaultFilesOptions
            {
                FileProvider = new PhysicalFileProvider(webuiPath),
                RequestPath = "",
                DefaultFileNames = new[] { "index.html" }
            });
            app.UseStaticFiles(new StaticFileOptions
            {
                FileProvider = new PhysicalFileProvider(webuiPath),
                RequestPath = "",
                ServeUnknownFileTypes = false,
            });
        }
        else
        {
            logger.Warn("WebUI static files not found at expected path.");
        }

        // Fallback: serve index.html for root path
        app.MapGet("/", async (HttpContext ctx) =>
        {
            if (webuiPath != null)
            {
                var indexPath = Path.Combine(webuiPath, "index.html");
                if (File.Exists(indexPath))
                {
                    ctx.Response.ContentType = "text/html";
                    await ctx.Response.SendFileAsync(indexPath);
                    return;
                }
            }
            ctx.Response.StatusCode = 404;
            await ctx.Response.WriteAsync("WebUI index.html not found");
        });

        // Helper: structured error response
        ErrorResponse Err(string msg) => new(msg);

        // Helper: check payload availability
        bool PayloadAvailable() => client != null && client.Connected;

        // API: connection state
        app.MapGet("/api/state", async (HttpContext ctx) =>
        {
            try
            {
                if (!PayloadAvailable())
                {
                    await ctx.Response.WriteAsJsonAsync(new ApiState(false, null,
                        Array.Empty<CheatRowData>(), logger.GetLogs(), "Not connected"));
                    return;
                }

                ProcessSnapshot? process = null;
                try
                {
                    process = await client!.GetActiveProcessAsync(cts.Token);
                }
                catch { /* not connected */ }

                var rows = manager?.ActiveCheats.Select(c => new CheatRowData(
                    c.Id, c.Name, c.Description, manager?.IsEnabled(c.Id) ?? false
                )).ToList() ?? new List<CheatRowData>();

                var result = new ApiState(client!.Connected,
                    process != null ? new ProcessInfoData(
                        process.TitleId, process.Name, process.Version, process.Region) : null,
                    rows, logger.GetLogs(), null);
                await ctx.Response.WriteAsJsonAsync(result);
            }
            catch (Exception ex)
            {
                await ctx.Response.WriteAsJsonAsync(new ApiState(false, null,
                    Array.Empty<CheatRowData>(), logger.GetLogs(), ex.Message));
            }
        });

        // API: process info
        app.MapGet("/api/process", async (HttpContext ctx) =>
        {
            try
            {
                if (!PayloadAvailable())
                {
                    ctx.Response.StatusCode = 503;
                    await ctx.Response.WriteAsJsonAsync(Err("Not connected"));
                    return;
                }
                var process = await client!.GetActiveProcessAsync(cts.Token);
                await ctx.Response.WriteAsJsonAsync(new
                {
                    titleId = process.TitleId,
                    name = process.Name,
                    version = process.Version,
                    region = process.Region,
                    pid = process.Pid
                });
            }
            catch (Exception ex)
            {
                ctx.Response.StatusCode = 500;
                await ctx.Response.WriteAsJsonAsync(Err(ex.Message));
            }
        });

        // API: list cheats
        app.MapGet("/api/cheats", async (HttpContext ctx) =>
        {
            if (manager == null)
            {
                ctx.Response.StatusCode = 503;
                await ctx.Response.WriteAsJsonAsync(Err("No cheat manager available"));
                return;
            }
            await ctx.Response.WriteAsJsonAsync(manager.ActiveCheats.Select(c => new
            {
                c.Id,
                c.Name,
                c.Description,
                Enabled = manager.IsEnabled(c.Id)
            }));
        });

        // API: enable cheat
        app.MapPost("/api/enable", async (HttpContext ctx) =>
        {
            try
            {
                var body = await JsonSerializer.DeserializeAsync<CheatActionRequest>(
                    ctx.Request.Body, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (body == null || manager == null)
                {
                    ctx.Response.StatusCode = 400;
                    await ctx.Response.WriteAsJsonAsync(new { success = false, error = "Invalid request or not connected" });
                    return;
                }
                var r = await manager.EnableAsync(body.Id, cts.Token);
                await ctx.Response.WriteAsJsonAsync(new { success = r.Success, cheatId = r.CheatId, error = r.Error });
            }
            catch (Exception ex)
            {
                ctx.Response.StatusCode = 500;
                await ctx.Response.WriteAsJsonAsync(new { success = false, error = ex.Message });
            }
        });

        // API: disable cheat
        app.MapPost("/api/disable", async (HttpContext ctx) =>
        {
            try
            {
                var body = await JsonSerializer.DeserializeAsync<CheatActionRequest>(
                    ctx.Request.Body, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (body == null || manager == null)
                {
                    ctx.Response.StatusCode = 400;
                    await ctx.Response.WriteAsJsonAsync(new { success = false, error = "Invalid request or not connected" });
                    return;
                }
                var r = await manager.DisableAsync(body.Id, cts.Token);
                await ctx.Response.WriteAsJsonAsync(new { success = r.Success, cheatId = r.CheatId, error = r.Error });
            }
            catch (Exception ex)
            {
                ctx.Response.StatusCode = 500;
                await ctx.Response.WriteAsJsonAsync(new { success = false, error = ex.Message });
            }
        });

        // API: scan memory
        app.MapPost("/api/scan", async (HttpContext ctx) =>
        {
            try
            {
                var body = await JsonSerializer.DeserializeAsync<ScanRequestData>(
                    ctx.Request.Body, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (body == null || !PayloadAvailable())
                {
                    ctx.Response.StatusCode = 400;
                    await ctx.Response.WriteAsJsonAsync(Err("Invalid request or not connected"));
                    return;
                }

                if (string.IsNullOrWhiteSpace(body.Pattern) || body.Pattern.Length < 2)
                {
                    ctx.Response.StatusCode = 400;
                    await ctx.Response.WriteAsJsonAsync(Err("Pattern is required and must be at least 2 characters"));
                    return;
                }

                var pattern = AobPattern.Parse(body.Pattern);
                var opts = new AobScanOptions
                {
                    ScanReadableOnly = true,
                    ScanExecutableOnly = body.ExecutableOnly,
                    MaxResults = body.MaxResults > 0 ? body.MaxResults : 1,
                };
                var localScanner = new AobScanner(client!, logger);
                var matches = await localScanner.FindAllAsync(pattern, opts, cts.Token);
                await ctx.Response.WriteAsJsonAsync(new
                {
                    matches = matches.Select(m => $"0x{m:X}").ToList(),
                    count = matches.Count
                });
            }
            catch (FormatException ex)
            {
                ctx.Response.StatusCode = 400;
                await ctx.Response.WriteAsJsonAsync(Err($"Invalid pattern: {ex.Message}"));
            }
            catch (Exception ex)
            {
                ctx.Response.StatusCode = 500;
                await ctx.Response.WriteAsJsonAsync(Err(ex.Message));
            }
        });

        // API: reload cheats
        app.MapPost("/api/reload-cheats", async (HttpContext ctx) =>
        {
            if (manager == null)
            {
                ctx.Response.StatusCode = 503;
                await ctx.Response.WriteAsJsonAsync(new { success = false, error = "Not connected" });
                return;
            }
            manager.LoadDatabase(cheatDb);
            try { await manager.RefreshActiveProcessAsync(cts.Token); } catch { /* best effort */ }
            await ctx.Response.WriteAsJsonAsync(new { success = true });
        });

        // API: get config
        app.MapGet("/api/config", async (HttpContext ctx) =>
        {
            await ctx.Response.WriteAsJsonAsync(new { consoleIp, consolePort, cheatDb, verbose });
        });

        // API: update config (stub — changes require restart)
        app.MapPost("/api/config", async (HttpContext ctx) =>
        {
            await ctx.Response.WriteAsJsonAsync(new
            {
                consoleIp,
                consolePort,
                cheatDb,
                verbose,
                message = "Config changes require restart. Edit config and restart the WebUI server."
            });
        });

        // ===== Cheat Manager Endpoints =====

        // POST /api/cheat-manager/open
        app.MapPost("/api/cheat-manager/open", async (HttpContext ctx) =>
        {
            if (session == null)
            {
                ctx.Response.StatusCode = 503;
                await ctx.Response.WriteAsJsonAsync(new { error = "Cheat manager session not initialized" });
                return;
            }

            try
            {
                var state = await session.OpenAsync(cts.Token);
                var cheatCount = manager?.ActiveCheats.Count ?? 0;
                await ctx.Response.WriteAsJsonAsync(new
                {
                    open = state.IsOpen,
                    titleId = state.TitleId,
                    cheatCount
                });
            }
            catch (Exception ex)
            {
                ctx.Response.StatusCode = 500;
                await ctx.Response.WriteAsJsonAsync(new { error = ex.Message });
            }
        });

        // POST /api/cheat-manager/close
        app.MapPost("/api/cheat-manager/close", async (HttpContext ctx) =>
        {
            if (session == null)
            {
                ctx.Response.StatusCode = 503;
                await ctx.Response.WriteAsJsonAsync(new { error = "Cheat manager session not initialized" });
                return;
            }

            try
            {
                var state = await session.CloseAsync(cts.Token);
                await ctx.Response.WriteAsJsonAsync(new { open = state.IsOpen });
            }
            catch (Exception ex)
            {
                ctx.Response.StatusCode = 500;
                await ctx.Response.WriteAsJsonAsync(new { error = ex.Message });
            }
        });

        // GET /api/cheat-manager/state
        app.MapGet("/api/cheat-manager/state", async (HttpContext ctx) =>
        {
            if (session == null)
            {
                ctx.Response.StatusCode = 503;
                await ctx.Response.WriteAsJsonAsync(new { error = "Cheat manager session not initialized" });
                return;
            }

            var state = session.GetState();
            await ctx.Response.WriteAsJsonAsync(new
            {
                open = state.IsOpen,
                titleId = state.TitleId,
                lastError = state.LastError
            });
        });

        // ===== Shortcut Config Endpoints =====

        // GET /api/shortcut-config
        app.MapGet("/api/shortcut-config", async (HttpContext ctx) =>
        {
            if (shortcutStore == null)
            {
                ctx.Response.StatusCode = 503;
                await ctx.Response.WriteAsJsonAsync(new { error = "Shortcut config store not initialized" });
                return;
            }

            var config = shortcutStore.Load();
            await ctx.Response.WriteAsJsonAsync(new
            {
                schemaVersion = config.SchemaVersion,
                enabled = config.Enabled,
                mode = config.Mode.ToString(),
                holdDurationMs = (int)config.HoldDuration.TotalMilliseconds,
                debounceMs = (int)config.Debounce.TotalMilliseconds,
                cheatManagerTrigger = config.CheatManagerTrigger.ToString(),
                closeTrigger = config.CloseTrigger.ToString(),
                customOpenChord = config.CustomOpenChord.Select(b => b.ToString()).ToList(),
                customChordHoldMs = config.CustomChordHoldMs
            });
        });

        // POST /api/shortcut-config
        app.MapPost("/api/shortcut-config", async (HttpContext ctx) =>
        {
            if (shortcutStore == null)
            {
                ctx.Response.StatusCode = 503;
                await ctx.Response.WriteAsJsonAsync(new { error = "Shortcut config store not initialized" });
                return;
            }

            try
            {
                var body = await JsonSerializer.DeserializeAsync<ShortcutConfigUpdateBody>(
                    ctx.Request.Body, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (body == null)
                {
                    ctx.Response.StatusCode = 400;
                    await ctx.Response.WriteAsJsonAsync(new { error = "Invalid request body" });
                    return;
                }

                var update = new ShortcutConfigUpdate
                {
                    Enabled = body.Enabled,
                    CheatManagerTrigger = ParseEnum<CheatManagerShortcut>(body.CheatManagerTrigger),
                    CloseTrigger = ParseEnum<CheatManagerShortcut>(body.CloseTrigger),
                    CustomChordHoldMs = body.CustomChordHoldMs,
                };

                if (body.CustomOpenChord != null)
                {
                    update.CustomOpenChord = body.CustomOpenChord
                        .Select(s => Enum.TryParse<PadButton>(s, ignoreCase: true, out var b) ? b : PadButton.None)
                        .Where(b => b != PadButton.None)
                        .ToList()
                        .AsReadOnly();
                }

                var config = shortcutStore.MergeAndSave(update);
                await ctx.Response.WriteAsJsonAsync(new
                {
                    schemaVersion = config.SchemaVersion,
                    enabled = config.Enabled,
                    mode = config.Mode.ToString(),
                    cheatManagerTrigger = config.CheatManagerTrigger.ToString(),
                    closeTrigger = config.CloseTrigger.ToString(),
                    customOpenChord = config.CustomOpenChord.Select(b => b.ToString()).ToList(),
                    customChordHoldMs = config.CustomChordHoldMs
                });
            }
            catch (JsonException ex)
            {
                ctx.Response.StatusCode = 400;
                await ctx.Response.WriteAsJsonAsync(new { error = $"Invalid JSON: {ex.Message}" });
            }
        });

        // POST /api/shortcut-config/record
        app.MapPost("/api/shortcut-config/record", async (HttpContext ctx) =>
        {
            if (shortcutStore == null)
            {
                ctx.Response.StatusCode = 503;
                await ctx.Response.WriteAsJsonAsync(new { error = "Shortcut config store not initialized" });
                return;
            }

            if (!PayloadAvailable())
            {
                ctx.Response.StatusCode = 503;
                await ctx.Response.WriteAsJsonAsync(new { error = "Payload unavailable" });
                return;
            }

            try
            {
                var body = await JsonSerializer.DeserializeAsync<RecordShortcutBody>(
                    ctx.Request.Body, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                int windowMs = body?.WindowMs ?? 5000;
                if (windowMs < 1000) windowMs = 1000;
                if (windowMs > 30000) windowMs = 30000;

                // Check if pad_state is available
                var testPad = await client!.GetPadStateAsync(cts.Token);
                if (testPad == null)
                {
                    await ctx.Response.WriteAsJsonAsync(new
                    {
                        success = false,
                        error = "Live pad polling not supported by the connected payload — pick a preset trigger instead."
                    });
                    return;
                }

                // Poll for the window duration to capture the chord
                var capturedButtons = PadButton.None;
                var startTime = DateTime.UtcNow;
                int samples = 0;

                while ((DateTime.UtcNow - startTime).TotalMilliseconds < windowMs)
                {
                    var pad = await client!.GetPadStateAsync(cts.Token);
                    if (pad != null)
                    {
                        var btns = (PadButton)pad.Buttons;
                        if (btns != PadButton.None)
                        {
                            capturedButtons |= btns;
                            samples++;
                        }
                    }
                    await Task.Delay(50, cts.Token);
                }

                if (capturedButtons == PadButton.None || samples == 0)
                {
                    await ctx.Response.WriteAsJsonAsync(new
                    {
                        success = false,
                        error = "No buttons were pressed during the recording window. Try again."
                    });
                    return;
                }

                // Decompose the captured buttons into individual buttons
                var chord = new List<PadButton>();
                foreach (PadButton b in Enum.GetValues(typeof(PadButton)))
                {
                    if (b != PadButton.None && (capturedButtons & b) == b)
                        chord.Add(b);
                }

                var config = shortcutStore.MergeAndSave(new ShortcutConfigUpdate
                {
                    CheatManagerTrigger = CheatManagerShortcut.Custom,
                    CustomOpenChord = chord.AsReadOnly(),
                    CustomChordHoldMs = 200,
                });

                await ctx.Response.WriteAsJsonAsync(new
                {
                    success = true,
                    chord = chord.Select(b => b.ToString()).ToList(),
                    samples,
                    message = $"Recorded chord: {string.Join(" + ", chord.Select(b => b.ToString()))}"
                });
            }
            catch (JsonException ex)
            {
                ctx.Response.StatusCode = 400;
                await ctx.Response.WriteAsJsonAsync(new { error = $"Invalid JSON: {ex.Message}" });
            }
            catch (Exception ex)
            {
                ctx.Response.StatusCode = 500;
                await ctx.Response.WriteAsJsonAsync(new { error = ex.Message });
            }
        });

        // Connect to console on startup
        logger.Info("Connecting to console...");
        try
        {
            client = new HttpNexusClient(consoleIp, consolePort);
            await client.ConnectAsync(consoleIp, cts.Token);
            logger.Info($"Connected to {consoleIp}:{consolePort}");

            var dbSvc = new CheatDatabaseService(logger: logger);
            var engine = new CheatEngine(client, logger: logger);
            manager = new CheatManager(client, dbSvc, engine, logger);
            manager.LoadDatabase(cheatDb);
            await manager.RefreshActiveProcessAsync(cts.Token);
            logger.Info($"Active game: {manager.ActiveProcess?.Name} ({manager.ActiveProcess?.TitleId})");

            // Initialize shortcut config store and cheat manager session
            shortcutStore = new ShortcutConfigStore(cheatDb, logger);
            var shortcutConfig = shortcutStore.Load();
            session = new CheatManagerSession(client, shortcutConfig, logger);
            session.StartPolling();
            logger.Info("Cheat manager session initialized");
        }
        catch (Exception ex)
        {
            logger.Warn($"Failed to connect initially: {ex.Message}");
            logger.Info("WebUI will start; restart with correct --ip to connect");
        }

        logger.Info($"WebUI starting on http://0.0.0.0:{webPort}");
        await app.RunAsync($"http://0.0.0.0:{webPort}");
    }

    private static TEnum? ParseEnum<TEnum>(string? value) where TEnum : struct, Enum
    {
        if (string.IsNullOrEmpty(value)) return null;
        if (Enum.TryParse<TEnum>(value, ignoreCase: true, out var result))
            return result;
        return null;
    }

    private static string? FindWebuiPath()
    {
        var dir = AppDomain.CurrentDomain.BaseDirectory;
        for (int i = 0; i < 8; i++)
        {
            var candidate = Path.Combine(dir, "menu", "webui");
            if (Directory.Exists(candidate)) return candidate;
            var parent = Directory.GetParent(dir);
            if (parent == null) break;
            dir = parent.FullName;
        }
        return null;
    }
}

// ----- API model types -----

public sealed record ApiState(
    bool Connected,
    ProcessInfoData? Process,
    IReadOnlyList<CheatRowData> Cheats,
    IReadOnlyList<string> Logs,
    string? Error
);

public sealed record ProcessInfoData(string TitleId, string Name, float Version, string? Region);
public sealed record CheatRowData(string Id, string Name, string? Description, bool Enabled);
public sealed record CheatActionRequest(string Id, bool Force = false);
public sealed record ScanRequestData(string Pattern, bool ExecutableOnly = false, int MaxResults = 1);
public sealed record ErrorResponse(string Error);

// Cheat manager request/response types
public sealed record ShortcutConfigUpdateBody
{
    public bool? Enabled { get; init; }
    public string? CheatManagerTrigger { get; init; }
    public string? CloseTrigger { get; init; }
    public List<string>? CustomOpenChord { get; init; }
    public int? CustomChordHoldMs { get; init; }
}

public sealed record RecordShortcutBody
{
    public int WindowMs { get; init; } = 5000;
}

// ----- WebUI logger -----

internal sealed class WebUILogger : ILogger
{
    private readonly List<string> _logs = new();
    private readonly bool _verbose;
    private readonly object _lock = new();

    public WebUILogger(bool verbose) { _verbose = verbose; }

    public void Debug(string message) { if (_verbose) Add("DBG", message); }
    public void Info(string message) => Add("INF", message);
    public void Warn(string message) => Add("WRN", message);
    public void Error(string message, Exception? ex = null) => Add("ERR", $"{message}{(ex != null ? " :: " + ex.Message : "")}");

    private void Add(string level, string msg)
    {
        lock (_lock)
        {
            _logs.Add($"[{level}] {msg}");
            if (_logs.Count > 200) _logs.RemoveRange(0, _logs.Count - 200);
        }
    }

    public IReadOnlyList<string> GetLogs()
    {
        lock (_lock) return _logs.ToArray();
    }
}
