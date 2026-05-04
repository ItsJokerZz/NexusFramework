using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;

namespace NexusCheatFramework.Formats
{
    /// <summary>
    /// Parses SHN XML trainer cheat files.
    /// Schema: <Trainer Game="..." Moder="..." Cusa="..." Version="..." Process="...">
    ///            <Cheat Text="..." Description="">
    ///              <Cheatline><Offset>...</Offset><Section>...</Section>
    ///                <ValueOn>...</ValueOn><ValueOff>...</ValueOff>
    ///              </Cheatline>
    ///            </Cheat>
    ///          </Trainer>
    /// </summary>
    public sealed class ShnXmlCheatParser : ICheatFormatParser
    {
        public bool CanParse(string path) =>
            !string.IsNullOrEmpty(path) && path.EndsWith(".shn", StringComparison.OrdinalIgnoreCase);

        public CheatFile? Parse(string path)
        {
            using var s = File.OpenRead(path);
            return Parse(s, path);
        }

        public CheatFile? Parse(Stream stream, string? sourcePath = null)
        {
            XDocument doc;
            try
            {
                doc = XDocument.Load(stream);
            }
            catch (XmlException ex)
            {
                throw new FormatException($"Invalid XML in SHN file: {ex.Message}", ex);
            }

            var trainer = doc.Root;
            if (trainer == null || trainer.Name.LocalName != "Trainer")
                throw new FormatException("SHN file missing root <Trainer> element.");

            var titleId = (string?)trainer.Attribute("Cusa") ?? throw new FormatException("Missing 'Cusa' attribute on <Trainer>.");
            var gameName = (string?)trainer.Attribute("Game");
            var version = (string?)trainer.Attribute("Version");
            var process = (string?)trainer.Attribute("Process") ?? "eboot.bin";

            var file = new CheatFile
            {
                TitleId = titleId,
                GameName = gameName,
                Version = version,
            };

            var usedIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var cheatEl in trainer.Elements("Cheat"))
            {
                var text = (string?)cheatEl.Attribute("Text") ?? "Unnamed Cheat";
                var desc = (string?)cheatEl.Attribute("Description");
                var cheatId = GenerateId(text, usedIds);

                var cheat = new CheatDefinition
                {
                    Id = cheatId,
                    Name = text,
                    Description = desc,
                };

                foreach (var line in cheatEl.Elements("Cheatline"))
                {
                    var offsetStr = (string?)line.Element("Offset") ?? "0";
                    var sectionStr = (string?)line.Element("Section");
                    var valueOnStr = (string?)line.Element("ValueOn") ?? "";
                    var valueOffStr = (string?)line.Element("ValueOff") ?? "";

                    var offset = ParseHexLong(offsetStr);
                    var onBytes = ParseDashHex(valueOnStr);
                    var offBytes = ParseDashHex(valueOffStr);

                    var code = new CheatCode
                    {
                        Type = CheatCodeType.ModuleWriteBytes,
                        ModuleName = process,
                        Offset = offset,
                        Bytes = onBytes,
                        ExpectedBytes = offBytes,
                        RestoreBytes = offBytes,
                        ActivationType = CheatActivationType.Checkbox,
                    };
                    cheat.Codes.Add(code);
                }

                if (cheat.Codes.Count > 0)
                    file.Cheats.Add(cheat);
            }

            return file;
        }

        private static string GenerateId(string name, HashSet<string> usedIds)
        {
            var sb = new System.Text.StringBuilder();
            foreach (var c in name)
            {
                if (char.IsLetterOrDigit(c))
                    sb.Append(char.ToLowerInvariant(c));
                else if (c == ' ' || c == '_' || c == '-')
                    sb.Append('_');
            }
            var id = sb.ToString().Trim('_');
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

        private static byte[]? ParseDashHex(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return null;
            var compact = s.Trim().Replace("-", "").Replace(" ", "");
            if (compact.Length % 2 != 0)
                throw new FormatException($"Invalid hex byte string '{s}'");
            var bytes = new byte[compact.Length / 2];
            for (int i = 0; i < bytes.Length; i++)
                bytes[i] = byte.Parse(compact.Substring(i * 2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            return bytes;
        }
    }
}
