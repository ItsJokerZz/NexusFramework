namespace NexusCheatFramework.Formats
{
    /// <summary>
    /// Result of detecting the format of a cheat file.
    /// </summary>
    public sealed class CheatFormatDetectionResult
    {
        /// <summary>Path to the file that was inspected.</summary>
        public string SourcePath { get; init; } = string.Empty;

        /// <summary>Detected format name e.g. "native-json", "etahen-json", "shn-xml", "mc4", "unknown".</summary>
        public string Format { get; init; } = "unknown";

        /// <summary>Whether the format is supported by a parser.</summary>
        public bool IsSupported { get; init; }

        /// <summary>Human-readable description of the format.</summary>
        public string Description { get; init; } = string.Empty;

        /// <summary>Parsed metadata if available (title ID, game name, etc.).</summary>
        public CheatFile? Preview { get; init; }
    }
}
