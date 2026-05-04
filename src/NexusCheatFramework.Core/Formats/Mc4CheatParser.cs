using System;
using System.IO;

namespace NexusCheatFramework.Formats
{
    /// <summary>
    /// Stub parser for ".mc4" cheat files. Same status as .shn — see
    /// <see cref="ShnCheatParser"/> and docs/CHEAT_FORMATS.md.
    /// </summary>
    public sealed class Mc4CheatParser : ICheatFormatParser
    {
        public bool CanParse(string path)
            => !string.IsNullOrEmpty(path) && path.EndsWith(".mc4", StringComparison.OrdinalIgnoreCase);

        public CheatFile Parse(string path) => throw Unsupported();
        public CheatFile Parse(Stream stream, string? sourcePath = null) => throw Unsupported();

        private static NotSupportedException Unsupported() =>
            new(".mc4 cheat format is not documented in the bundled upstream sources and is not implemented.");
    }
}
