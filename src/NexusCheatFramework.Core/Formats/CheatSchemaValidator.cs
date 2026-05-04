using System;
using System.Collections.Generic;
using System.Globalization;

namespace NexusCheatFramework.Formats
{
    /// <summary>
    /// Validates cheat files against the native NexusCheatFramework schema.
    /// </summary>
    public sealed class CheatSchemaValidator
    {
        /// <summary>
        /// Known valid title ID prefixes for PlayStation.
        /// </summary>
        private static readonly HashSet<string> KnownTitlePrefixes = new(StringComparer.OrdinalIgnoreCase)
        {
            "CUSA", "PPSA", "NPXS", "NPEA", "NPUA", "NPJB",
            "PCJS", "PCKS", "PCSB", "PCSH", "PCSA", "PCSC",
        };

        public CheatValidationResult Validate(CheatFile? file)
        {
            var issues = new List<CheatValidationIssue>();

            if (file == null)
            {
                issues.Add(new CheatValidationIssue { Severity = "Error", Path = "(root)", Message = "CheatFile is null." });
                return new CheatValidationResult { Success = false, Issues = issues };
            }

            // Top-level validation
            if (string.IsNullOrWhiteSpace(file.TitleId))
                issues.Add(new CheatValidationIssue { Severity = "Error", Path = "titleId", Message = "titleId is required." });
            else if (!IsValidTitleId(file.TitleId))
                issues.Add(new CheatValidationIssue { Severity = "Warning", Path = "titleId", Message = $"titleId '{file.TitleId}' may not be a standard PlayStation ID format." });

            if (file.Cheats == null || file.Cheats.Count == 0)
                issues.Add(new CheatValidationIssue { Severity = "Error", Path = "cheats", Message = "No cheats defined in file." });

            // Track duplicate IDs
            var cheatIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (file.Cheats != null)
            {
                foreach (var cheat in file.Cheats)
                {
                    ValidateCheat(cheat, cheatIds, issues);
                }
            }

            return new CheatValidationResult { Success = issues.Count == 0, Issues = issues };
        }

        private void ValidateCheat(CheatDefinition cheat, HashSet<string> cheatIds, List<CheatValidationIssue> issues)
        {
            var basePath = $"cheats[?]";

            if (string.IsNullOrWhiteSpace(cheat.Id))
                issues.Add(new CheatValidationIssue { Severity = "Error", Path = basePath, Message = "Cheat 'id' is required." });
            else if (!cheatIds.Add(cheat.Id))
                issues.Add(new CheatValidationIssue { Severity = "Error", Path = $"{basePath}/id", Message = $"Duplicate cheat ID '{cheat.Id}'." });

            if (string.IsNullOrWhiteSpace(cheat.Name))
                issues.Add(new CheatValidationIssue { Severity = "Error", Path = $"{basePath}['{cheat.Id}']/name", Message = "Cheat 'name' is required." });

            if (cheat.Codes == null || cheat.Codes.Count == 0)
            {
                issues.Add(new CheatValidationIssue { Severity = "Error", Path = $"{basePath}/codes", Message = $"Cheat '{cheat.Id}' has no codes." });
                return;
            }

            var codeIdx = 0;
            foreach (var code in cheat.Codes)
            {
                var codePath = $"{basePath}['{cheat.Id}']/codes[{codeIdx}]";
                ValidateCode(code, codePath, issues);
                codeIdx++;
            }
        }

        private void ValidateCode(CheatCode code, string path, List<CheatValidationIssue> issues)
        {
            // Validate type
            string typeName = code.Type.ToString();

            if (!Enum.IsDefined(typeof(CheatCodeType), code.Type))
            {
                issues.Add(new CheatValidationIssue { Severity = "Error", Path = path, Message = $"Unknown cheat code type '{code.Type}'." });
                return;
            }

            // Check required fields per type
            switch (code.Type)
            {
                case CheatCodeType.WriteBytes:
                case CheatCodeType.PointerWriteBytes:
                case CheatCodeType.AobPointerWriteBytes:
                case CheatCodeType.AobWriteBytes:
                case CheatCodeType.ModuleWriteBytes:
                    if (code.Bytes == null || code.Bytes.Length == 0)
                        issues.Add(new CheatValidationIssue { Severity = "Error", Path = $"{path}/bytes", Message = $"{typeName} requires non-empty 'bytes'." });
                    break;

                case CheatCodeType.WriteValue:
                case CheatCodeType.PointerWriteValue:
                case CheatCodeType.AobPointerWriteValue:
                case CheatCodeType.AobWriteValue:
                case CheatCodeType.ModuleWriteValue:
                case CheatCodeType.FreezeValue:
                    if (string.IsNullOrWhiteSpace(code.ValueType))
                        issues.Add(new CheatValidationIssue { Severity = "Error", Path = $"{path}/valueType", Message = $"{typeName} requires 'valueType'." });
                    if (string.IsNullOrWhiteSpace(code.Value))
                        issues.Add(new CheatValidationIssue { Severity = "Error", Path = $"{path}/value", Message = $"{typeName} requires 'value'." });
                    else if (!string.IsNullOrWhiteSpace(code.ValueType))
                    {
                        var parseOk = TryParseValue(code.ValueType, code.Value);
                        if (!parseOk)
                            issues.Add(new CheatValidationIssue { Severity = "Warning", Path = $"{path}/value", Message = $"Value '{code.Value}' could not be parsed as '{code.ValueType}'." });
                    }
                    break;
            }

            // Address validation
            if (code.Address.HasValue && code.Address.Value == 0)
                issues.Add(new CheatValidationIssue { Severity = "Warning", Path = $"{path}/address", Message = "Address is 0." });

            // Freeze interval validation
            if (code.Type == CheatCodeType.FreezeValue)
            {
                if (code.FreezeIntervalMs < 1 || code.FreezeIntervalMs > 60000)
                    issues.Add(new CheatValidationIssue { Severity = "Warning", Path = $"{path}/freezeIntervalMs", Message = $"freezeIntervalMs {code.FreezeIntervalMs} is outside recommended range 1-60000." });
            }

            // Module name required for module writes
            if (code.Type == CheatCodeType.ModuleWriteBytes || code.Type == CheatCodeType.ModuleWriteValue)
            {
                if (string.IsNullOrWhiteSpace(code.ModuleName))
                    issues.Add(new CheatValidationIssue { Severity = "Error", Path = $"{path}/moduleName", Message = $"{typeName} requires 'moduleName'." });
            }

            // AOB pattern validation (basic check)
            if (!string.IsNullOrWhiteSpace(code.AobPattern))
            {
                try
                {
                    Memory.AobPattern.Parse(code.AobPattern);
                }
                catch (Exception ex)
                {
                    issues.Add(new CheatValidationIssue { Severity = "Error", Path = $"{path}/aobPattern", Message = $"Invalid AOB pattern: {ex.Message}" });
                }
            }

            // Pointer offsets validation
            if (code.PointerOffsets != null)
            {
                foreach (var offset in code.PointerOffsets)
                {
                    try
                    {
                        var s = offset.Trim();
                        if (s.StartsWith("-")) s = s.Substring(1);
                        if (s.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                            long.Parse(s.Substring(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                        else
                            long.Parse(s, CultureInfo.InvariantCulture);
                    }
                    catch
                    {
                        issues.Add(new CheatValidationIssue { Severity = "Error", Path = $"{path}/pointerOffsets", Message = $"Invalid pointer offset '{offset}'." });
                    }
                }
            }
        }

        private static bool IsValidTitleId(string tid)
        {
            if (tid.Length < 9) return false;
            var prefix = tid.Substring(0, 4);
            return KnownTitlePrefixes.Contains(prefix);
        }

        private static bool TryParseValue(string valueType, string value)
        {
            try
            {
                switch (valueType?.ToLowerInvariant())
                {
                    case "int": case "int32": int.Parse(value, CultureInfo.InvariantCulture); return true;
                    case "uint": case "uint32": uint.Parse(value, CultureInfo.InvariantCulture); return true;
                    case "short": case "int16": short.Parse(value, CultureInfo.InvariantCulture); return true;
                    case "ushort": case "uint16": ushort.Parse(value, CultureInfo.InvariantCulture); return true;
                    case "byte": byte.Parse(value, CultureInfo.InvariantCulture); return true;
                    case "sbyte": sbyte.Parse(value, CultureInfo.InvariantCulture); return true;
                    case "long": case "int64": long.Parse(value, CultureInfo.InvariantCulture); return true;
                    case "ulong": case "uint64": ulong.Parse(value, CultureInfo.InvariantCulture); return true;
                    case "float": float.Parse(value, CultureInfo.InvariantCulture); return true;
                    case "double": double.Parse(value, CultureInfo.InvariantCulture); return true;
                    default: return true; // unknown type, can't verify
                }
            }
            catch { return false; }
        }
    }
}
