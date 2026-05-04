using System;
using System.IO;

namespace NexusCheatFramework.Formats
{
    /// <summary>
    /// Detects cheat file format by extension, content inspection, and known patterns.
    /// </summary>
    public sealed class CheatFormatDetector : ICheatFormatDetector
    {
        public CheatFormatDetectionResult Detect(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return new CheatFormatDetectionResult { SourcePath = path ?? "", Format = "unknown", IsSupported = false, Description = "Empty path." };

            var ext = Path.GetExtension(path)?.ToLowerInvariant() ?? "";
            var fileName = Path.GetFileName(path)?.ToLowerInvariant() ?? "";

            // .json or .txt — check if it's native or etaHEN-style JSON
            if (ext == ".json" || ext == ".txt")
            {
                try
                {
                    var content = File.ReadAllText(path).TrimStart();
                    if (content.StartsWith("{", StringComparison.Ordinal))
                    {
                        // Use quick heuristic: native has "titleId", etaHEN has "id"/"mods"
                        if (content.Contains("\"mods\"", StringComparison.OrdinalIgnoreCase) &&
                            !content.Contains("\"titleId\"", StringComparison.OrdinalIgnoreCase))
                        {
                            return new CheatFormatDetectionResult
                            {
                                SourcePath = path,
                                Format = "etahen-json",
                                IsSupported = true,
                                Description = "etaHEN-style JSON with mods array",
                            };
                        }
                        if (content.Contains("\"titleId\"", StringComparison.OrdinalIgnoreCase))
                        {
                            return new CheatFormatDetectionResult
                            {
                                SourcePath = path,
                                Format = "native-json",
                                IsSupported = true,
                                Description = "Native NexusCheatFramework JSON format",
                            };
                        }
                    }
                }
                catch { /* fall through */ }

                return new CheatFormatDetectionResult
                {
                    SourcePath = path,
                    Format = "json-unknown",
                    IsSupported = false,
                    Description = "JSON file but unrecognized schema",
                };
            }

            // .shn — XML trainer format
            if (ext == ".shn")
            {
                try
                {
                    var content = File.ReadAllText(path);
                    if (content.Contains("<Trainer", StringComparison.OrdinalIgnoreCase))
                    {
                        return new CheatFormatDetectionResult
                        {
                            SourcePath = path,
                            Format = "shn-xml",
                            IsSupported = true,
                            Description = "SHN XML trainer format",
                        };
                    }
                }
                catch { /* fall through */ }
                return new CheatFormatDetectionResult
                {
                    SourcePath = path,
                    Format = "shn-unknown",
                    IsSupported = false,
                    Description = "SHN file but not recognizable XML",
                };
            }

            // .mc4 — check for base64-encoded header
            if (ext == ".mc4")
            {
                try
                {
                    var raw = File.ReadAllText(path).Trim();
                    // Try base64 decode to check header
                    var decoded = System.Convert.FromBase64String(raw);
                    var header = System.Text.Encoding.UTF8.GetString(decoded, 0, Math.Min(decoded.Length, 100));
                    if (header.Contains("MC4", StringComparison.OrdinalIgnoreCase))
                    {
                        return new CheatFormatDetectionResult
                        {
                            SourcePath = path,
                            Format = "mc4",
                            IsSupported = false,
                            Description = "MC4 format — base64-encoded with custom/binary data after header",
                        };
                    }
                }
                catch { /* not base64 or still valid format */ }

                return new CheatFormatDetectionResult
                {
                    SourcePath = path,
                    Format = "mc4",
                    IsSupported = false,
                    Description = "MC4 format — detected by extension; content unrecognized",
                };
            }

            return new CheatFormatDetectionResult
            {
                SourcePath = path,
                Format = "unknown",
                IsSupported = false,
                Description = $"Unrecognized file format (extension: {ext})",
            };
        }
    }
}
