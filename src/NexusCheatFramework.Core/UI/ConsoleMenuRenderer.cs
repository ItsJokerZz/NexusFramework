using System;
using System.IO;

namespace NexusCheatFramework.UI
{
    /// <summary>
    /// Minimal text renderer for <see cref="CheatMenuModel"/>. Used by the CLI
    /// and as a baseline reference for richer renderers (WebUI, overlay).
    /// </summary>
    public static class ConsoleMenuRenderer
    {
        public static void Render(CheatMenuModel model, TextWriter? @out = null)
        {
            @out ??= Console.Out;

            if (model.ActiveProcess is { } p)
                @out.WriteLine($"Active: {p.Name} [TID={p.TitleId} v={p.Version} region={p.Region}]");
            else
                @out.WriteLine("Active: <no game detected>");

            @out.WriteLine($"Cheats: {model.Rows.Count}");
            foreach (var r in model.Rows)
            {
                var mark = r.Enabled ? "[x]" : "[ ]";
                @out.WriteLine($"  {mark} {r.Id} — {r.Name}");
                if (!string.IsNullOrEmpty(r.Description))
                    @out.WriteLine($"      {r.Description}");
            }

            if (!string.IsNullOrEmpty(model.LastError))
                @out.WriteLine($"! {model.LastError}");
        }
    }
}
