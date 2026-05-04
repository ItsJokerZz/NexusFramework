using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NexusCheatFramework.Formats;
using NexusCheatFramework.Logging;
using NexusCheatFramework.Nexus;
using NexusCheatFramework.Services;

namespace NexusCheatFramework.Core
{
    /// <summary>
    /// High-level orchestration: detect the active game, find matching cheat
    /// files in the database, surface them to UI/CLI, and bridge enable/disable
    /// requests to <see cref="CheatEngine"/>.
    /// </summary>
    public sealed class CheatManager
    {
        private readonly INexusClient _client;
        private readonly CheatDatabaseService _db;
        private readonly CheatEngine _engine;
        private readonly ILogger _log;

        private IReadOnlyList<CheatFile> _allFiles = Array.Empty<CheatFile>();
        private List<CheatFile> _activeFiles = new();
        private ProcessSnapshot? _activeProcess;

        public CheatManager(INexusClient client, CheatDatabaseService db, CheatEngine engine, ILogger? logger = null)
        {
            _client = client; _db = db; _engine = engine;
            _log = logger ?? NullLogger.Instance;
        }

        public ProcessSnapshot? ActiveProcess => _activeProcess;
        public IReadOnlyList<CheatFile> ActiveCheatFiles => _activeFiles;

        public IReadOnlyList<CheatDefinition> ActiveCheats =>
            _activeFiles.SelectMany(f => f.Cheats).ToList();

        public void LoadDatabase(string path) => _allFiles = _db.LoadDirectory(path);

        public async Task<ProcessSnapshot> RefreshActiveProcessAsync(CancellationToken ct = default)
        {
            _activeProcess = await _client.GetActiveProcessAsync(ct).ConfigureAwait(false);
            _activeFiles = _db.FilterByTitleId(_allFiles, _activeProcess.TitleId).ToList();
            _log.Info($"Active TID={_activeProcess.TitleId} ({_activeProcess.Name}) — {_activeFiles.Count} matching cheat file(s)");
            return _activeProcess;
        }

        public CheatDefinition? FindCheat(string id)
            => ActiveCheats.FirstOrDefault(c => string.Equals(c.Id, id, StringComparison.OrdinalIgnoreCase));

        public Task<CheatResult> EnableAsync(string cheatId, CancellationToken ct = default)
        {
            var c = FindCheat(cheatId);
            if (c == null) return Task.FromResult(new CheatResult
            { CheatId = cheatId, Success = false, Status = CheatStatus.Failed, Error = $"Cheat '{cheatId}' not loaded for active TID." });
            return _engine.EnableAsync(c, ct);
        }

        public Task<CheatResult> DisableAsync(string cheatId, CancellationToken ct = default)
            => _engine.DisableAsync(cheatId, ct);

        public bool IsEnabled(string cheatId) => _engine.IsEnabled(cheatId);
    }
}
