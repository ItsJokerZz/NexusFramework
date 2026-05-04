using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace NexusCheatFramework.Formats
{
    /// <summary>
    /// Parses etaHEN-style JSON cheat files.
    /// Schema: { name, id, version, process, mods: [{ name, hint, type, memory: [{ offset, on, off }] }], credits }
    /// </summary>
    public sealed class EtaHenJsonCheatParser : ICheatFormatParser
    {
        public bool CanParse(string path) =>
            !string.IsNullOrEmpty(path) && path.EndsWith(".json", StringComparison.OrdinalIgnoreCase);

        public CheatFile? Parse(string path)
        {
            using var s = File.OpenRead(path);
            return Parse(s, path);
        }

        public CheatFile? Parse(Stream stream, string? sourcePath = null)
        {
            using var doc = JsonDocument.Parse(stream);
            var root = doc.RootElement;

            var gameName = GetString(root, "name") ?? "Unknown Game";
            var titleId = GetString(root, "id") ?? throw new FormatException("Missing required 'id' in etaHEN JSON.");
            var version = GetString(root, "version");
            var process = GetString(root, "process") ?? "eboot.bin";

            var file = new CheatFile
            {
                TitleId = titleId,
                GameName = gameName,
                Version = version,
            };

            if (root.TryGetProperty("mods", out var mods) && mods.ValueKind == JsonValueKind.Array)
            {
                var usedIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var mod in mods.EnumerateArray())
                {
                    var modName = GetString(mod, "name") ?? "Unnamed Mod";
                    var hint = GetString(mod, "hint");
                    var type = GetString(mod, "type") ?? "checkbox";
                    var cheatId = GenerateId(modName, usedIds);

                    var cheat = new CheatDefinition
                    {
                        Id = cheatId,
                        Name = modName,
                        Description = hint,
                        EnabledByDefault = false,
                    };

                    if (mod.TryGetProperty("memory", out var memory) && memory.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var mem in memory.EnumerateArray())
                        {
                            var offsetStr = GetString(mem, "offset") ?? "0";
                            var onStr = GetString(mem, "on") ?? "";
                            var offStr = GetString(mem, "off") ?? "";

                            var offset = ParseHexLong(offsetStr);
                            var onBytes = ParseCompactHex(onStr);
                            var offBytes = ParseCompactHex(offStr);

                            var code = new CheatCode
                            {
                                Type = CheatCodeType.ModuleWriteBytes,
                                ModuleName = process,
                                Offset = offset,
                                Bytes = onBytes,
                                ExpectedBytes = offBytes,
                                RestoreBytes = offBytes,
                                ActivationType = type,
                            };
                            cheat.Codes.Add(code);
                        }
                    }

                    if (cheat.Codes.Count > 0)
                        file.Cheats.Add(cheat);
                }
            }

            return file;
        }

        private static string GenerateId(string name, HashSet<string> usedIds)
        {
            // Normalize: lowercase, spaces to underscores, remove unsafe chars
            var sb = new System.Text.StringBuilder();
            foreach (var c in name)
            {
                if (char.IsLetterOrDigit(c))
                    sb.Append(char.ToLowerInvariant(c));
                else if (c == ' ' || c == '_' || c == '-')
                    sb.Append('_');
                // skip other chars
            }
            var id = sb.ToString().Trim('_');
            // Ensure uniqueness
            if (string.IsNullOrWhiteSpace(id)) id = "cheat";
            var unique = id;
            int counter = 2;
            while (!usedIds.Add(unique))
                unique = $"{id}_{counter++}";
            return unique;
        }

        private static long ParseHexLong(string s)
        {
            s = s.Trim();
            if (s.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                return long.Parse(s.Substring(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            return long.Parse(s, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        }

        private static byte[]? ParseCompactHex(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return null;
            var compact = s.Trim().Replace(" ", "").Replace("-", "");
            if (compact.Length % 2 != 0)
                throw new FormatException($"Invalid hex byte string '{s}'");
            var bytes = new byte[compact.Length / 2];
            for (int i = 0; i < bytes.Length; i++)
                bytes[i] = byte.Parse(compact.Substring(i * 2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            return bytes;
        }

        private static string? GetString(JsonElement parent, string name)
        {
            if (!parent.TryGetProperty(name, out var v)) return null;
            return v.ValueKind switch
            {
                JsonValueKind.String => v.GetString(),
                JsonValueKind.Null => null,
                _ => v.ToString(),
            };
        }
    }
}
