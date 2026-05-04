using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NexusCheatFramework.Formats;
using NexusCheatFramework.Logging;

namespace NexusCheatFramework.Services
{
    /// <summary>
    /// Loads cheat files from a directory tree. Files are dispatched to the
    /// first registered <see cref="ICheatFormatParser"/> that claims to handle
    /// them.
    /// </summary>
    public sealed class CheatDatabaseService
    {
        private readonly ILogger _log;
        private readonly List<ICheatFormatParser> _parsers;

        public CheatDatabaseService(IEnumerable<ICheatFormatParser>? parsers = null, ILogger? logger = null)
        {
            _log = logger ?? NullLogger.Instance;
            _parsers = parsers?.ToList() ?? new List<ICheatFormatParser>
            {
                new JsonCheatParser(),
                new EtaHenCheatParser(),
                new ShnCheatParser(),
                new Mc4CheatParser(),
            };
        }

        public IReadOnlyList<CheatFile> LoadDirectory(string root)
        {
            var results = new List<CheatFile>();
            if (!Directory.Exists(root)) return results;

            foreach (var path in Directory.EnumerateFiles(root, "*.*", SearchOption.AllDirectories))
            {
                var parser = _parsers.FirstOrDefault(p => p.CanParse(path));
                if (parser == null) continue;
                try { results.Add(parser.Parse(path)); }
                catch (NotSupportedException ex) { _log.Warn($"Skipping unsupported {path}: {ex.Message}"); }
                catch (Exception ex) { _log.Error($"Failed to parse {path}", ex); }
            }
            return results;
        }

        public IReadOnlyList<CheatFile> FilterByTitleId(IEnumerable<CheatFile> all, string titleId)
            => all.Where(c => string.Equals(c.TitleId, titleId, StringComparison.OrdinalIgnoreCase)).ToList();
    }
}
