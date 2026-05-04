using System.Collections.Generic;

namespace NexusCheatFramework.Formats
{
    /// <summary>
    /// Result of converting an external cheat file to the native format.
    /// </summary>
    public sealed class CheatConversionResult
    {
        public bool Success { get; init; }
        public string SourceFormat { get; init; } = string.Empty;
        public CheatFile? CheatFile { get; init; }
        public IReadOnlyList<CheatConversionIssue> Issues { get; init; } = System.Array.Empty<CheatConversionIssue>();
    }

    public sealed class CheatConversionIssue
    {
        public string Severity { get; init; } = "Warning"; // Info, Warning, Error
        public string Path { get; init; } = string.Empty;
        public string Message { get; init; } = string.Empty;
    }
}
