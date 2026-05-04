using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace NexusCheatFramework.Formats
{
    /// <summary>
    /// Native JSON cheat format. See docs/CHEAT_FORMATS.md for the schema.
    /// Documented and stable for this project.
    /// </summary>
    public sealed class JsonCheatParser : ICheatFormatParser
    {
        public bool CanParse(string path) =>
            !string.IsNullOrEmpty(path) && path.EndsWith(".json", StringComparison.OrdinalIgnoreCase);

        public CheatFile Parse(string path)
        {
            using var s = File.OpenRead(path);
            return Parse(s, path);
        }

        public CheatFile Parse(Stream stream, string? sourcePath = null)
        {
            using var doc = JsonDocument.Parse(stream);
            var root = doc.RootElement;

            var file = new CheatFile
            {
                TitleId = GetString(root, "titleId") ?? throw new FormatException("Missing required 'titleId'."),
                GameName = GetString(root, "gameName"),
                Version  = GetString(root, "version"),
                Region   = GetString(root, "region"),
            };

            if (string.IsNullOrWhiteSpace(file.TitleId))
                throw new FormatException("'titleId' must not be empty.");

            if (root.TryGetProperty("cheats", out var cheats) && cheats.ValueKind == JsonValueKind.Array)
            {
                foreach (var cj in cheats.EnumerateArray())
                {
                    var def = new CheatDefinition
                    {
                        Id = GetString(cj, "id") ?? throw new FormatException("Cheat missing 'id'."),
                        Name = GetString(cj, "name") ?? throw new FormatException("Cheat missing 'name'."),
                        Description = GetString(cj, "description"),
                        EnabledByDefault = GetBool(cj, "enabledByDefault") ?? false,
                    };

                    if (cj.TryGetProperty("codes", out var codes) && codes.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var co in codes.EnumerateArray())
                            def.Codes.Add(ParseCode(co));
                    }

                    if (def.Codes.Count == 0)
                        throw new FormatException($"Cheat '{def.Id}' has no codes.");

                    file.Cheats.Add(def);
                }
            }

            return file;
        }

        private static CheatCode ParseCode(JsonElement co)
        {
            var typeStr = GetString(co, "type") ?? throw new FormatException("Code missing 'type'.");
            if (!Enum.TryParse<CheatCodeType>(MapTypeName(typeStr), true, out var type))
                throw new FormatException($"Unknown cheat code type '{typeStr}'.");

            return new CheatCode
            {
                Type = type,
                Address = ParseHexUlongOrNull(GetString(co, "address")),
                AobPattern = GetString(co, "aobPattern"),
                Offset = ParseLongOrZero(GetString(co, "offset")),
                Bytes = ParseHexBytes(GetString(co, "bytes")),
                ExpectedBytes = ParseHexBytes(GetString(co, "expectedBytes")),
                ValueType = GetString(co, "valueType"),
                Value = GetString(co, "value"),
                ModuleName = GetString(co, "moduleName"),
            };
        }

        private static string MapTypeName(string s)
        {
            // Accept snake_case and camelCase forms
            return s.Replace("_", "");
        }

        private static string? GetString(JsonElement parent, string name)
        {
            if (!parent.TryGetProperty(name, out var v)) return null;
            return v.ValueKind switch
            {
                JsonValueKind.String => v.GetString(),
                JsonValueKind.Number => v.ToString(),
                JsonValueKind.True => "true",
                JsonValueKind.False => "false",
                JsonValueKind.Null => null,
                _ => v.ToString(),
            };
        }

        private static bool? GetBool(JsonElement parent, string name)
        {
            if (!parent.TryGetProperty(name, out var v)) return null;
            return v.ValueKind switch
            {
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.String => bool.TryParse(v.GetString(), out var b) ? b : (bool?)null,
                _ => null,
            };
        }

        private static ulong? ParseHexUlongOrNull(string? s)
        {
            if (string.IsNullOrWhiteSpace(s)) return null;
            s = s.Trim();
            if (s.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                return ulong.Parse(s.Substring(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            return ulong.Parse(s, CultureInfo.InvariantCulture);
        }

        private static long ParseLongOrZero(string? s)
        {
            if (string.IsNullOrWhiteSpace(s)) return 0;
            s = s.Trim();
            bool neg = s.StartsWith("-");
            if (neg) s = s.Substring(1);
            long val;
            if (s.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                val = long.Parse(s.Substring(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            else
                val = long.Parse(s, CultureInfo.InvariantCulture);
            return neg ? -val : val;
        }

        public static byte[]? ParseHexBytes(string? s)
        {
            if (string.IsNullOrWhiteSpace(s)) return null;
            var trimmed = s!.Trim();
            string compact = new string(trimmed.Where(c => !char.IsWhiteSpace(c) && c != '-' && c != ',').ToArray());
            if (compact.Length % 2 != 0)
                throw new FormatException($"Hex byte string '{s}' has odd length.");
            var bytes = new byte[compact.Length / 2];
            for (int i = 0; i < bytes.Length; i++)
                bytes[i] = byte.Parse(compact.Substring(i * 2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            return bytes;
        }
    }
}
