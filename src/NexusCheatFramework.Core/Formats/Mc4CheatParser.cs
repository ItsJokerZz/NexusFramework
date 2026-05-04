using System;
using System.IO;
using System.Text;

namespace NexusCheatFramework.Formats
{
    /// <summary>
    /// MC4 parser. The MC4 format appears to be a base64-encoded payload
    /// containing a header section followed by binary/compressed cheat data.
    /// Without a public format spec, full parsing is not possible.
    /// This parser detects and extracts available metadata only.
    /// </summary>
    public sealed class Mc4CheatParser : ICheatFormatParser
    {
        public bool CanParse(string path) =>
            !string.IsNullOrEmpty(path) && path.EndsWith(".mc4", StringComparison.OrdinalIgnoreCase);

        public CheatFile? Parse(string path)
        {
            using var s = File.OpenRead(path);
            return Parse(s, path);
        }

        public CheatFile? Parse(Stream stream, string? sourcePath = null)
        {
            using var reader = new StreamReader(stream);
            var content = reader.ReadToEnd()?.Trim();
            if (string.IsNullOrWhiteSpace(content))
                throw new FormatException("MC4 file is empty.");

            // Try base64 decode
            byte[] decoded;
            try
            {
                decoded = Convert.FromBase64String(content);
            }
            catch (FormatException ex)
            {
                throw new FormatException($"MC4 file is not valid base64: {ex.Message}");
            }

            if (decoded.Length < 16)
                throw new FormatException("MC4 decoded data too short.");

            // Extract header text (up to first null or first 512 bytes)
            int headerLen = Math.Min(decoded.Length, 512);
            string header = Encoding.UTF8.GetString(decoded, 0, headerLen);
            int nullIdx = header.IndexOf('\0');
            if (nullIdx >= 0)
                header = header.Substring(0, nullIdx);

            // Parse header lines
            string titleId = "unknown";
            string? gameName = null;
            string? version = null;

            foreach (var line in header.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                var trimmed = line.Trim();
                if (trimmed.StartsWith("titleId=", StringComparison.OrdinalIgnoreCase))
                    titleId = trimmed.Substring("titleId=".Length).Trim();
                else if (trimmed.StartsWith("game=", StringComparison.OrdinalIgnoreCase))
                    gameName = trimmed.Substring("game=".Length).Trim();
                else if (trimmed.StartsWith("version=", StringComparison.OrdinalIgnoreCase))
                    version = trimmed.Substring("version=".Length).Trim();
            }

            return new CheatFile
            {
                TitleId = titleId,
                GameName = gameName,
                Version = version,
                Cheats = new System.Collections.Generic.List<CheatDefinition>(), // empty — cannot parse
            };
        }

        /// <summary>
        /// Extract metadata for inspect-format purposes without requiring full parsing.
        /// </summary>
        public static Mc4Metadata ExtractMetadata(string path)
        {
            try
            {
                var content = File.ReadAllText(path).Trim();
                var decoded = Convert.FromBase64String(content);
                int headerLen = Math.Min(decoded.Length, 512);
                string header = Encoding.UTF8.GetString(decoded, 0, headerLen);
                int nullIdx = header.IndexOf('\0');
                if (nullIdx >= 0)
                    header = header.Substring(0, nullIdx);

                string titleId = "unknown";
                string? gameName = null;
                string? version = null;

                foreach (var line in header.Split('\n', StringSplitOptions.RemoveEmptyEntries))
                {
                    var trimmed = line.Trim();
                    if (trimmed.StartsWith("titleId=", StringComparison.OrdinalIgnoreCase))
                        titleId = trimmed.Substring("titleId=".Length).Trim();
                    else if (trimmed.StartsWith("game=", StringComparison.OrdinalIgnoreCase))
                        gameName = trimmed.Substring("game=".Length).Trim();
                    else if (trimmed.StartsWith("version=", StringComparison.OrdinalIgnoreCase))
                        version = trimmed.Substring("version=".Length).Trim();
                }

                return new Mc4Metadata
                {
                    FileSize = new FileInfo(path).Length,
                    Base64DecodeSuccess = true,
                    DecodedLength = decoded.Length,
                    HeaderText = header,
                    TitleId = titleId,
                    GameName = gameName,
                    Version = version,
                    FirstDecodedBytes = decoded.Length >= 16
                        ? BitConverter.ToString(decoded, 0, 16).Replace("-", " ")
                        : BitConverter.ToString(decoded).Replace("-", " "),
                    Notes = "Header extracted. Payload data after header appears binary/compressed. Full parsing requires format spec or additional samples.",
                };
            }
            catch (Exception ex)
            {
                return new Mc4Metadata
                {
                    FileSize = File.Exists(path) ? new FileInfo(path).Length : 0,
                    Base64DecodeSuccess = false,
                    Notes = $"Failed to inspect: {ex.Message}",
                };
            }
        }
    }

    public sealed class Mc4Metadata
    {
        public long FileSize { get; init; }
        public bool Base64DecodeSuccess { get; init; }
        public int DecodedLength { get; init; }
        public string? HeaderText { get; init; }
        public string TitleId { get; init; } = "unknown";
        public string? GameName { get; init; }
        public string? Version { get; init; }
        public string? FirstDecodedBytes { get; init; }
        public string Notes { get; init; } = string.Empty;
    }
}
