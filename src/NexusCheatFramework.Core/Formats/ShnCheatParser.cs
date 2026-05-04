using System;
using System.IO;

namespace NexusCheatFramework.Formats
{
    /// <summary>
    /// Stub parser for the Illusion-cheats ".shn" container.
    ///
    /// The format is not documented in either upstream repository (etaHEN /
    /// NexusFramework). We deliberately refuse to guess its layout. If a public
    /// spec or sample becomes available, implement parsing here behind a
    /// feature flag and ensure tests cover round-tripping.
    /// </summary>
    public sealed class ShnCheatParser : ICheatFormatParser
    {
        public bool CanParse(string path)
            => !string.IsNullOrEmpty(path) && path.EndsWith(".shn", StringComparison.OrdinalIgnoreCase);

        public CheatFile Parse(string path) => throw Unsupported();
        public CheatFile Parse(Stream stream, string? sourcePath = null) => throw Unsupported();

        private static NotSupportedException Unsupported() =>
            new("The .shn (Illusion) cheat format is not documented in the bundled upstream sources " +
                "and is not implemented. Convert to the native JSON format.");
    }
}
