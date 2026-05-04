using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using NexusCheatFramework.Core;
using NexusCheatFramework.Input;
using NexusCheatFramework.Logging;
using NexusCheatFramework.Memory;
using NexusCheatFramework.Nexus;
using NexusCheatFramework.Services;
using NexusCheatFramework.UI;

namespace NexusCheatFramework.Cli;

internal static class Program
{
    public static async Task<int> Main(string[] args)
    {
        try
        {
            return await RunAsync(args);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"error: {ex.Message}");
            return 1;
        }
    }

    private static async Task<int> RunAsync(string[] args)
    {
        if (args.Length == 0) { PrintHelp(); return 0; }
        var cmd = args[0];
        var opts = ParseOptions(args.Skip(1));
        var ip = opts.Get("ip") ?? Environment.GetEnvironmentVariable("NCF_IP");
        var port = opts.Get("port") ?? "9080";
        var verbose = opts.Has("verbose");
        ILogger log = new ConsoleLogger(verbose);

        switch (cmd)
        {
            case "help": case "-h": case "--help": PrintHelp(); return 0;
            case "info": return await CmdInfo(ip, log);
            case "scan": return await CmdScan(ip, opts, log);
            case "cheats": return await CmdCheats(ip, opts, log);
            case "manager": return await CmdManager(ip, port, opts, log);
            case "shortcut": return await CmdShortcut(ip, port, opts, log);
            default: Console.Error.WriteLine($"unknown command: {cmd}"); PrintHelp(); return 2;
        }
    }

    private static void PrintHelp()
    {
        Console.WriteLine("ncf — NexusCheatFramework CLI");
        Console.WriteLine();
        Console.WriteLine("Usage:");
        Console.WriteLine("  ncf info   --ip <addr>");
        Console.WriteLine("  ncf scan   --ip <addr> --pattern \"48 8B ?? ?? 89\" [--all] [--exec]");
        Console.WriteLine("  ncf cheats list    --ip <addr> --db <dir>");
        Console.WriteLine("  ncf cheats enable  --ip <addr> --db <dir> --cheat <id>");
        Console.WriteLine("  ncf cheats disable --ip <addr> --db <dir> --cheat <id>");
        Console.WriteLine("  ncf manager open   --ip <addr> [--port <port>]");
        Console.WriteLine("  ncf manager close  --ip <addr> [--port <port>]");
        Console.WriteLine("  ncf manager status --ip <addr> [--port <port>]");
        Console.WriteLine("  ncf shortcut show  --ip <addr> [--port <port>]");
        Console.WriteLine("  ncf shortcut set   --trigger <Mode> [--ip <addr>] [--port <port>]");
        Console.WriteLine("  ncf shortcut record [--ip <addr>] [--port <port>] [--window-ms <ms>]");
        Console.WriteLine();
        Console.WriteLine("Set NCF_IP env var to avoid passing --ip every time.");
    }

    private static async Task<HttpNexusClient> ConnectAsync(string? ip)
    {
        if (string.IsNullOrWhiteSpace(ip))
            throw new ArgumentException("Missing --ip / NCF_IP.");
        var client = new HttpNexusClient(ip!);
        await client.ConnectAsync(ip!);
        return client;
    }

    private static async Task<int> CmdInfo(string? ip, ILogger log)
    {
        using var c = await ConnectAsync(ip);
        var t = await c.GetTargetInfoAsync();
        var p = await c.GetActiveProcessAsync();
        Console.WriteLine($"Target: {t.Name} ({t.Model}) FW {t.Firmware} type={t.ConsoleType}");
        Console.WriteLine($"Active: {p.Name} TID={p.TitleId} v={p.Version} pid={p.Pid}");
        Console.WriteLine($"Maps  : {p.MemoryMaps.Count}");
        return 0;
    }

    private static async Task<int> CmdScan(string? ip, Options opts, ILogger log)
    {
        var pattern = opts.Get("pattern") ?? throw new ArgumentException("--pattern required");
        using var c = await ConnectAsync(ip);
        var scanner = new AobScanner(c, log);
        var p = AobPattern.Parse(pattern);
        var so = new AobScanOptions
        {
            ScanReadableOnly = true,
            ScanExecutableOnly = opts.Has("exec"),
            MaxResults = opts.Has("all") ? null : 1,
        };
        var hits = await scanner.FindAllAsync(p, so);
        if (hits.Count == 0) { Console.WriteLine("no matches"); return 1; }
        foreach (var h in hits) Console.WriteLine($"0x{h:X}");
        return 0;
    }

    private static async Task<int> CmdCheats(string? ip, Options opts, ILogger log)
    {
        var sub = opts.Positional.ElementAtOrDefault(0) ?? "list";
        var db = opts.Get("db") ?? "./cheats";
        using var c = await ConnectAsync(ip);
        var dbSvc = new CheatDatabaseService(logger: log);
        var engine = new CheatEngine(c, logger: log);
        var mgr = new CheatManager(c, dbSvc, engine, log);
        mgr.LoadDatabase(db);
        await mgr.RefreshActiveProcessAsync();

        switch (sub)
        {
            case "list":
                ConsoleMenuRenderer.Render(CheatMenuModel.Build(mgr));
                return 0;
            case "enable":
                {
                    var id = opts.Get("cheat") ?? throw new ArgumentException("--cheat required");
                    var r = await mgr.EnableAsync(id);
                    Console.WriteLine(r.Success ? $"enabled: {id}" : $"failed: {r.Error}");
                    return r.Success ? 0 : 1;
                }
            case "disable":
                {
                    var id = opts.Get("cheat") ?? throw new ArgumentException("--cheat required");
                    var r = await mgr.DisableAsync(id);
                    Console.WriteLine(r.Success ? $"disabled: {id}" : $"failed: {r.Error}");
                    return r.Success ? 0 : 1;
                }
            default:
                Console.Error.WriteLine($"unknown cheats subcommand: {sub}");
                return 2;
        }
    }

    // ===== Manager subcommands =====

    private static async Task<int> CmdManager(string? ip, string port, Options opts, ILogger log)
    {
        var sub = opts.Positional.ElementAtOrDefault(0) ?? "status";

        // Try to reach the WebUI API first
        var baseUrl = $"http://{ip}:{port}";

        switch (sub)
        {
            case "open":
                return await CallWebApi(baseUrl, "/api/cheat-manager/open", "POST", log);
            case "close":
                return await CallWebApi(baseUrl, "/api/cheat-manager/close", "POST", log);
            case "status":
                return await CallWebApi(baseUrl, "/api/cheat-manager/state", "GET", log);
            default:
                Console.Error.WriteLine($"unknown manager subcommand: {sub}");
                return 2;
        }
    }

    // ===== Shortcut subcommands =====

    private static async Task<int> CmdShortcut(string? ip, string port, Options opts, ILogger log)
    {
        var sub = opts.Positional.ElementAtOrDefault(0) ?? "show";
        var baseUrl = $"http://{ip}:{port}";

        switch (sub)
        {
            case "show":
                return await CallWebApi(baseUrl, "/api/shortcut-config", "GET", log);
            case "set":
                {
                    var trigger = opts.Get("trigger");
                    if (string.IsNullOrEmpty(trigger))
                    {
                        Console.Error.WriteLine("--trigger is required (e.g. HoldL1R1Square, Off, Custom)");
                        return 2;
                    }
                    var body = new { cheatManagerTrigger = trigger };
                    return await CallWebApi(baseUrl, "/api/shortcut-config", "POST", log, body);
                }
            case "record":
                {
                    var windowMs = opts.Get("window-ms") ?? "5000";
                    var body = new { windowMs = int.Parse(windowMs) };
                    return await CallWebApi(baseUrl, "/api/shortcut-config/record", "POST", log, body);
                }
            default:
                Console.Error.WriteLine($"unknown shortcut subcommand: {sub}");
                return 2;
        }
    }

    private static async Task<int> CallWebApi(string baseUrl, string path, string method, ILogger log, object? body = null)
    {
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
            var url = $"{baseUrl}{path}";

            HttpResponseMessage response;
            if (method == "GET")
            {
                response = await http.GetAsync(url);
            }
            else
            {
                var json = JsonSerializer.Serialize(body ?? new { });
                var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
                response = method switch
                {
                    "POST" => await http.PostAsync(url, content),
                    _ => throw new ArgumentException($"Unsupported method: {method}")
                };
            }

            var responseBody = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                Console.Error.WriteLine($"API error ({response.StatusCode}): {responseBody}");
                return 1;
            }

            // Pretty-print JSON
            try
            {
                var doc = JsonDocument.Parse(responseBody);
                var pretty = JsonSerializer.Serialize(doc.RootElement, new JsonSerializerOptions { WriteIndented = true });
                Console.WriteLine(pretty);
            }
            catch
            {
                Console.WriteLine(responseBody);
            }

            return 0;
        }
        catch (HttpRequestException ex)
        {
            Console.Error.WriteLine($"Failed to reach WebUI at {baseUrl}: {ex.Message}");
            Console.Error.WriteLine("Make sure the WebUI is running: dotnet run --project src/NexusCheatFramework.Web -- --ip <console-ip> --port 9080");
            return 1;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            return 1;
        }
    }

    private sealed class Options
    {
        public Dictionary<string, string?> Flags { get; } = new(StringComparer.OrdinalIgnoreCase);
        public List<string> Positional { get; } = new();
        public string? Get(string name) => Flags.TryGetValue(name, out var v) ? v : null;
        public bool Has(string name) => Flags.ContainsKey(name);
    }

    private static Options ParseOptions(IEnumerable<string> args)
    {
        var o = new Options();
        var list = args.ToList();
        for (int i = 0; i < list.Count; i++)
        {
            var a = list[i];
            if (a.StartsWith("--"))
            {
                var key = a.Substring(2);
                string? val = null;
                if (i + 1 < list.Count && !list[i + 1].StartsWith("--")) { val = list[i + 1]; i++; }
                o.Flags[key] = val;
            }
            else o.Positional.Add(a);
        }
        return o;
    }
}
