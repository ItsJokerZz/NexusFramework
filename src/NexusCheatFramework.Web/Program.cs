using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.FileProviders;
using NexusCheatFramework.Core;
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

        var builder = WebApplication.CreateBuilder();
        var app = builder.Build();

        // Serve static files from menu/webui
        var webuiPath = FindWebuiPath();
        if (webuiPath != null && Directory.Exists(webuiPath))
        {
            app.UseStaticFiles(new StaticFileOptions
            {
                FileProvider = new PhysicalFileProvider(webuiPath),
                RequestPath = "",
            });
        }

        // API: connection state
        app.MapGet("/api/state", () =>
        {
            try
            {
                if (client == null || !client.Connected)
                    return Results.Json(new ApiState(false, null, Array.Empty<CheatRowData>(), logger.GetLogs(), "Not connected"));

                var process = client.Connected ? client.GetActiveProcessAsync().GetAwaiter().GetResult() : null;
                var rows = manager?.ActiveCheats.Select(c => new CheatRowData(
                    c.Id, c.Name, c.Description, manager?.IsEnabled(c.Id) ?? false
                )).ToList() ?? new List<CheatRowData>();

                return Results.Json(new ApiState(true,
                    process != null ? new ProcessInfoData(process.TitleId, process.Name, process.Version, process.Region) : null,
                    rows, logger.GetLogs(), null));
            }
            catch (Exception ex)
            {
                return Results.Json(new ApiState(false, null, Array.Empty<CheatRowData>(), logger.GetLogs(), ex.Message));
            }
        });

        // API: process info
        app.MapGet("/api/process", () =>
        {
            try
            {
                if (client == null || !client.Connected)
                    return Results.Json(new ErrorResponse("Not connected"));
                var process = client.GetActiveProcessAsync().GetAwaiter().GetResult();
                return Results.Json(new { titleId = process.TitleId, name = process.Name, version = process.Version, region = process.Region, pid = process.Pid });
            }
            catch (Exception ex) { return Results.Json(new ErrorResponse(ex.Message)); }
        });

        // API: list cheats
        app.MapGet("/api/cheats", () =>
        {
            if (manager == null)
                return Results.Json(new ErrorResponse("No manager"));
            return Results.Json(manager.ActiveCheats.Select(c => new {
                c.Id, c.Name, c.Description, Enabled = manager.IsEnabled(c.Id)
            }));
        });

        // API: enable cheat
        app.MapPost("/api/enable", async (HttpContext ctx) =>
        {
            try
            {
                var body = await JsonSerializer.DeserializeAsync<CheatActionRequest>(ctx.Request.Body, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (body == null || manager == null)
                    return Results.Json(new { success = false, error = "Invalid request or not connected" });
                var r = await manager.EnableAsync(body.Id);
                return Results.Json(new { success = r.Success, cheatId = r.CheatId, error = r.Error });
            }
            catch (Exception ex) { return Results.Json(new { success = false, error = ex.Message }); }
        });

        // API: disable cheat
        app.MapPost("/api/disable", async (HttpContext ctx) =>
        {
            try
            {
                var body = await JsonSerializer.DeserializeAsync<CheatActionRequest>(ctx.Request.Body, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (body == null || manager == null)
                    return Results.Json(new { success = false, error = "Invalid request or not connected" });
                var r = await manager.DisableAsync(body.Id);
                return Results.Json(new { success = r.Success, cheatId = r.CheatId, error = r.Error });
            }
            catch (Exception ex) { return Results.Json(new { success = false, error = ex.Message }); }
        });

        // API: scan memory
        app.MapPost("/api/scan", async (HttpContext ctx) =>
        {
            try
            {
                var body = await JsonSerializer.DeserializeAsync<ScanRequestData>(ctx.Request.Body, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (body == null || client == null || !client.Connected)
                    return Results.Json(new ErrorResponse("Invalid request or not connected"));

                var pattern = AobPattern.Parse(body.Pattern);
                var opts = new AobScanOptions
                {
                    ScanReadableOnly = true,
                    ScanExecutableOnly = body.ExecutableOnly,
                    MaxResults = body.MaxResults > 0 ? body.MaxResults : 1,
                };
                var scanner = new AobScanner(client, logger);
                var matches = await scanner.FindAllAsync(pattern, opts);
                return Results.Json(new { matches = matches.Select(m => $"0x{m:X}").ToList(), count = matches.Count });
            }
            catch (Exception ex) { return Results.Json(new ErrorResponse(ex.Message)); }
        });

        // API: reload cheats
        app.MapPost("/api/reload-cheats", () =>
        {
            if (manager == null)
                return Results.Json(new { success = false, error = "Not connected" });
            manager.LoadDatabase(cheatDb);
            _ = manager.RefreshActiveProcessAsync();
            return Results.Json(new { success = true });
        });

        // API: get config
        app.MapGet("/api/config", () =>
        {
            return Results.Json(new { consoleIp, consolePort, cheatDb, verbose });
        });

        // API: update config (stub)
        app.MapPost("/api/config", () =>
        {
            return Results.Json(new { consoleIp, consolePort, cheatDb, verbose, message = "Config changes require restart" });
        });

        // Connect to console on startup
        logger.Info("Connecting to console...");
        try
        {
            client = new HttpNexusClient(consoleIp, consolePort);
            await client.ConnectAsync(consoleIp);
            logger.Info($"Connected to {consoleIp}:{consolePort}");

            var dbSvc = new CheatDatabaseService(logger: logger);
            var engine = new CheatEngine(client, logger: logger);
            manager = new CheatManager(client, dbSvc, engine, logger);
            manager.LoadDatabase(cheatDb);
            await manager.RefreshActiveProcessAsync();
            logger.Info($"Active game: {manager.ActiveProcess?.Name} ({manager.ActiveProcess?.TitleId})");
        }
        catch (Exception ex)
        {
            logger.Warn($"Failed to connect initially: {ex.Message}");
            logger.Info("WebUI will start; restart with correct --ip to connect");
        }

        logger.Info($"WebUI starting on http://0.0.0.0:{webPort}");
        await app.RunAsync($"http://0.0.0.0:{webPort}");
    }

    private static string? FindWebuiPath()
    {
        // Look relative to the executable directory, then upward for project root
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
public sealed record ErrorResponse(string error);

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
