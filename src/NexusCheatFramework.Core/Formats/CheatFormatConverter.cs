using System;
using System.Collections.Generic;
using System.IO;

namespace NexusCheatFramework.Formats
{
    /// <summary>
    /// Orchestrates detection, parsing, and conversion of various cheat file formats
    /// into the native NexusCheatFramework format.
    /// </summary>
    public sealed class CheatFormatConverter : ICheatFormatConverter
    {
        private readonly ICheatFormatDetector _detector;
        private readonly Dictionary<string, ICheatFormatParser> _parsers = new(StringComparer.OrdinalIgnoreCase);

        public CheatFormatConverter(ICheatFormatDetector? detector = null)
        {
            _detector = detector ?? new CheatFormatDetector();
            _parsers["native"] = new JsonCheatParser();
            _parsers["etahen"] = new EtaHenJsonCheatParser();
            _parsers["shn"] = new ShnXmlCheatParser();
        }

        public bool CanConvert(string path)
        {
            if (!File.Exists(path)) return false;
            var result = _detector.Detect(path);
            return result.IsSupported;
        }

        public CheatConversionResult Convert(string path)
        {
            var issues = new List<CheatConversionIssue>();

            try
            {
                var detection = _detector.Detect(path);
                if (!detection.IsSupported)
                {
                    issues.Add(new CheatConversionIssue
                    {
                        Severity = "Error",
                        Path = path,
                        Message = $"Unsupported format: {detection.Format}. {detection.Description}",
                    });
                    return new CheatConversionResult
                    {
                        Success = false,
                        SourceFormat = detection.Format,
                        Issues = issues,
                    };
                }

                ICheatFormatParser? parser = detection.Format switch
                {
                    "native-json" => _parsers["native"],
                    "etahen-json" => _parsers["etahen"],
                    "shn-xml" => _parsers["shn"],
                    _ => null,
                };

                if (parser == null)
                {
                    issues.Add(new CheatConversionIssue { Severity = "Error", Path = path, Message = $"No parser for format '{detection.Format}'." });
                    return new CheatConversionResult { Success = false, SourceFormat = detection.Format, Issues = issues };
                }

                var cheatFile = parser.Parse(path);
                if (cheatFile == null)
                {
                    issues.Add(new CheatConversionIssue { Severity = "Error", Path = path, Message = "Parser returned null." });
                    return new CheatConversionResult { Success = false, SourceFormat = detection.Format, Issues = issues };
                }

                // Validate the converted result
                var validator = new CheatSchemaValidator();
                var validation = validator.Validate(cheatFile);
                foreach (var vi in validation.Issues)
                {
                    issues.Add(new CheatConversionIssue { Severity = vi.Severity, Path = vi.Path, Message = vi.Message });
                }

                return new CheatConversionResult
                {
                    Success = true,
                    SourceFormat = detection.Format,
                    CheatFile = cheatFile,
                    Issues = issues,
                };
            }
            catch (Exception ex)
            {
                issues.Add(new CheatConversionIssue { Severity = "Error", Path = path, Message = $"Conversion failed: {ex.Message}" });
                return new CheatConversionResult { Success = false, SourceFormat = "unknown", Issues = issues };
            }
        }

        /// <summary>
        /// Serializes a CheatFile to the native JSON format.
        /// </summary>
        public static string SerializeToNativeJson(CheatFile file)
        {
            using var stream = new MemoryStream();
            var writer = new System.Text.Json.Utf8JsonWriter(stream, new System.Text.Json.JsonWriterOptions { Indented = true });

            writer.WriteStartObject();
            writer.WriteString("titleId", file.TitleId);
            if (file.GameName != null) writer.WriteString("gameName", file.GameName);
            if (file.Version != null) writer.WriteString("version", file.Version);
            if (file.Region != null) writer.WriteString("region", file.Region);

            writer.WriteStartArray("cheats");
            foreach (var cheat in file.Cheats)
            {
                writer.WriteStartObject();
                writer.WriteString("id", cheat.Id);
                writer.WriteString("name", cheat.Name);
                if (cheat.Description != null) writer.WriteString("description", cheat.Description);
                if (cheat.EnabledByDefault) writer.WriteBoolean("enabledByDefault", true);

                writer.WriteStartArray("codes");
                foreach (var code in cheat.Codes)
                {
                    writer.WriteStartObject();
                    writer.WriteString("type", code.Type.ToString());
                    if (code.Address.HasValue) writer.WriteString("address", $"0x{code.Address.Value:X}");
                    if (code.AobPattern != null) writer.WriteString("aobPattern", code.AobPattern);
                    if (code.Offset != 0) writer.WriteString("offset", $"0x{code.Offset:X}");
                    if (code.Bytes != null) writer.WriteString("bytes", BitConverter.ToString(code.Bytes).Replace("-", " "));
                    if (code.ExpectedBytes != null) writer.WriteString("expectedBytes", BitConverter.ToString(code.ExpectedBytes).Replace("-", " "));
                    if (code.RestoreBytes != null) writer.WriteString("restoreBytes", BitConverter.ToString(code.RestoreBytes).Replace("-", " "));
                    if (code.ValueType != null) writer.WriteString("valueType", code.ValueType);
                    if (code.Value != null) writer.WriteString("value", code.Value);
                    if (code.ModuleName != null) writer.WriteString("moduleName", code.ModuleName);
                    if (code.FreezeIntervalMs != 250) writer.WriteNumber("freezeIntervalMs", code.FreezeIntervalMs);
                    if (code.PointerOffsets != null)
                    {
                        writer.WriteStartArray("pointerOffsets");
                        foreach (var po in code.PointerOffsets) writer.WriteStringValue(po);
                        writer.WriteEndArray();
                    }
                    if (code.AobOffset != 0) writer.WriteNumber("aobOffset", code.AobOffset);
                    if (code.ActivationType != null) writer.WriteString("activationType", code.ActivationType);
                    writer.WriteEndObject();
                }
                writer.WriteEndArray();
                writer.WriteEndObject();
            }
            writer.WriteEndArray();
            writer.WriteEndObject();
            writer.Flush();

            return System.Text.Encoding.UTF8.GetString(stream.ToArray());
        }
    }
}
