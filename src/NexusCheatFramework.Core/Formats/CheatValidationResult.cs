using System.Collections.Generic;

namespace NexusCheatFramework.Formats
{
    public sealed class CheatValidationResult
    {
        public bool Success { get; init; }
        public IReadOnlyList<CheatValidationIssue> Issues { get; init; } = System.Array.Empty<CheatValidationIssue>();
    }

    public sealed class CheatValidationIssue
    {
        public string Severity { get; init; } = "Warning"; // Info, Warning, Error
        public string Path { get; init; } = string.Empty;
        public string Message { get; init; } = string.Empty;
    }
}
