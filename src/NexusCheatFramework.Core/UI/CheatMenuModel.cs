using System.Collections.Generic;
using NexusCheatFramework.Core;
using NexusCheatFramework.Formats;
using NexusCheatFramework.Nexus;

namespace NexusCheatFramework.UI
{
    /// <summary>
    /// View-model the CLI/WebUI/overlay all consume. Pure data — no rendering.
    /// </summary>
    public sealed class CheatMenuModel
    {
        public ProcessSnapshot? ActiveProcess { get; set; }
        public List<CheatRow> Rows { get; set; } = new();
        public string? LastError { get; set; }

        public static CheatMenuModel Build(CheatManager mgr)
        {
            var m = new CheatMenuModel { ActiveProcess = mgr.ActiveProcess };
            foreach (var c in mgr.ActiveCheats)
                m.Rows.Add(new CheatRow
                {
                    Id = c.Id,
                    Name = c.Name,
                    Description = c.Description,
                    Enabled = mgr.IsEnabled(c.Id),
                });
            return m;
        }
    }

    public sealed class CheatRow
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public bool Enabled { get; set; }
    }
}
