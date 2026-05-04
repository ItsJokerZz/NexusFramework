// Example: connect to a console, list cheats for the active TID, and enable one.
//
// Build:   dotnet run --project examples/BasicTrainer -- --ip <console-ip>
//
// This file is intentionally a free-standing csx-style example. To compile it,
// drop it into a project that references NexusCheatFramework.Core.

using System;
using System.Threading.Tasks;
using NexusCheatFramework.Core;
using NexusCheatFramework.Logging;
using NexusCheatFramework.Nexus;
using NexusCheatFramework.Services;
using NexusCheatFramework.UI;

internal static class BasicTrainer
{
    public static async Task<int> Run(string ip, string cheatDb, string? enableId = null)
    {
        var log = new ConsoleLogger(verbose: true);
        using var client = new HttpNexusClient(ip);
        await client.ConnectAsync(ip);

        var db = new CheatDatabaseService(logger: log);
        var engine = new CheatEngine(client, logger: log);
        var mgr = new CheatManager(client, db, engine, log);

        mgr.LoadDatabase(cheatDb);
        await mgr.RefreshActiveProcessAsync();

        ConsoleMenuRenderer.Render(CheatMenuModel.Build(mgr));

        if (!string.IsNullOrEmpty(enableId))
        {
            var r = await mgr.EnableAsync(enableId!);
            Console.WriteLine(r.Success ? $"OK {enableId}" : $"FAIL {r.Error}");
        }
        return 0;
    }
}
