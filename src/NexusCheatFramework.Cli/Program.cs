using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using NexusCheatFramework.Core;
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
        var verbose = opts.Has("verbose");
        ILogger log = new ConsoleLogger(verbose);

        switch (cmd)
        {
            case "help": case "-h": case "--help": PrintHelp(); return 0;
            case "info": return await CmdInfo(ip, log);
            case "scan": return await CmdScan(ip, opts, log);
            case "cheats": return await CmdCheats(ip, opts, log);
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
