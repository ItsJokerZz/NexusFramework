using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using NexusCheatFramework.Logging;

namespace NexusCheatFramework.Input
{
    /// <summary>
    /// Persists <see cref="ShortcutConfig"/> to a JSON file at
    /// <c><cheat-database-root>/shortcut-config.json</c>.
    ///
    /// Handles schema versioning:
    ///   v0 (legacy, no SchemaVersion field) — loads, fills defaults, rewrites as v1.
    ///   v1 (current) — full round-trip with all cheat-manager fields.
    /// </summary>
    public sealed class ShortcutConfigStore
    {
        private readonly string _filePath;
        private readonly ILogger _log;
        private static readonly JsonSerializerOptions JsonOpts = new()
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters = { new PadButtonListConverter() },
        };

        /// <summary>
        /// Creates a store rooted at <paramref name="cheatDbRoot"/>.
        /// The config file is stored at <c><cheatDbRoot>/shortcut-config.json</c>.
        /// </summary>
        public ShortcutConfigStore(string cheatDbRoot, ILogger? logger = null)
        {
            _log = logger ?? NullLogger.Instance;
            _filePath = Path.Combine(cheatDbRoot, "shortcut-config.json");
        }

        /// <summary>
        /// Loads the config from disk. If the file is missing, returns the default
        /// config and writes it. If the file exists but is missing new fields,
        /// fills defaults and rewrites.
        /// </summary>
        public ShortcutConfig Load()
        {
            if (!File.Exists(_filePath))
            {
                _log.Info("shortcut-config.json not found — creating default");
                var defaults = new ShortcutConfig();
                Save(defaults);
                return defaults;
            }

            try
            {
                var json = File.ReadAllText(_filePath);
                var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                // Detect schema version
                int schemaVersion = 0;
                if (root.TryGetProperty("schemaVersion", out var sv))
                {
                    schemaVersion = sv.GetInt32();
                }

                // Deserialize with upgrade
                var config = JsonSerializer.Deserialize<ShortcutConfig>(json, JsonOpts) ?? new ShortcutConfig();

                if (schemaVersion < 1)
                {
                    _log.Info("Upgrading shortcut-config.json from v0 to v1");
                    config.SchemaVersion = 1;
                    // Fill defaults for new fields if they weren't in the JSON
                    if (config.CheatManagerTrigger == 0 && !root.TryGetProperty("cheatManagerTrigger", out _))
                        config.CheatManagerTrigger = CheatManagerShortcut.HoldL1R1Square;
                    if (config.CloseTrigger == 0 && !root.TryGetProperty("closeTrigger", out _))
                        config.CloseTrigger = CheatManagerShortcut.HoldL1R1Square;
                    if (config.CustomChordHoldMs == 0 && !root.TryGetProperty("customChordHoldMs", out _))
                        config.CustomChordHoldMs = 200;
                    Save(config);
                }

                return config;
            }
            catch (JsonException ex)
            {
                _log.Error($"Failed to parse shortcut-config.json: {ex.Message}", ex);
                _log.Info("Falling back to default config");
                var defaults = new ShortcutConfig();
                Save(defaults);
                return defaults;
            }
            catch (Exception ex)
            {
                _log.Error($"Failed to load shortcut-config.json: {ex.Message}", ex);
                var defaults = new ShortcutConfig();
                return defaults;
            }
        }

        /// <summary>
        /// Saves the config to disk, creating the directory if needed.
        /// </summary>
        public void Save(ShortcutConfig config)
        {
            try
            {
                var dir = Path.GetDirectoryName(_filePath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                var json = JsonSerializer.Serialize(config, JsonOpts);
                File.WriteAllText(_filePath, json);
                _log.Info($"shortcut-config.json saved to {_filePath}");
            }
            catch (Exception ex)
            {
                _log.Error($"Failed to save shortcut-config.json", ex);
            }
        }

        /// <summary>
        /// Merges a partial config update into the current config and persists.
        /// Only non-null properties from <paramref name="update"/> are applied.
        /// </summary>
        public ShortcutConfig MergeAndSave(ShortcutConfigUpdate update)
        {
            var current = Load();

            if (update.CheatManagerTrigger.HasValue)
                current.CheatManagerTrigger = update.CheatManagerTrigger.Value;
            if (update.CloseTrigger.HasValue)
                current.CloseTrigger = update.CloseTrigger.Value;
            if (update.CustomChordHoldMs.HasValue)
                current.CustomChordHoldMs = update.CustomChordHoldMs.Value;
            if (update.CustomOpenChord != null)
                current.CustomOpenChord = update.CustomOpenChord;
            if (update.Enabled.HasValue)
                current.Enabled = update.Enabled.Value;
            if (update.Mode.HasValue)
                current.Mode = update.Mode.Value;
            if (update.HoldDurationMs.HasValue)
                current.HoldDuration = TimeSpan.FromMilliseconds(update.HoldDurationMs.Value);
            if (update.DebounceMs.HasValue)
                current.Debounce = TimeSpan.FromMilliseconds(update.DebounceMs.Value);

            Save(current);
            return current;
        }
    }

    /// <summary>
    /// Partial update model for merging into <see cref="ShortcutConfig"/>.
    /// All properties are nullable — only set properties are applied.
    /// </summary>
    public sealed record ShortcutConfigUpdate
    {
        public bool? Enabled { get; init; }
        public CheatsShortcutMode? Mode { get; init; }
        public int? HoldDurationMs { get; init; }
        public int? DebounceMs { get; init; }
        public CheatManagerShortcut? CheatManagerTrigger { get; init; }
        public CheatManagerShortcut? CloseTrigger { get; init; }
        public IReadOnlyList<PadButton>? CustomOpenChord { get; init; }
        public int? CustomChordHoldMs { get; init; }
    }

    /// <summary>
    /// JSON converter for <see cref="IReadOnlyList{PadButton}"/> that serializes
    /// as an array of strings (e.g. ["L1", "R1", "Square"]).
    /// </summary>
    internal sealed class PadButtonListConverter : System.Text.Json.Serialization.JsonConverter<IReadOnlyList<PadButton>>
    {
        public override IReadOnlyList<PadButton> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.StartArray)
                throw new JsonException("Expected array for PadButton list");

            var list = new List<PadButton>();
            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndArray)
                    break;
                if (reader.TokenType == JsonTokenType.String)
                {
                    if (Enum.TryParse<PadButton>(reader.GetString(), ignoreCase: true, out var btn))
                        list.Add(btn);
                }
            }
            return list.AsReadOnly();
        }

        public override void Write(Utf8JsonWriter writer, IReadOnlyList<PadButton> value, JsonSerializerOptions options)
        {
            writer.WriteStartArray();
            foreach (var btn in value)
                writer.WriteStringValue(btn.ToString());
            writer.WriteEndArray();
        }
    }
}
