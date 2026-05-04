using System;
using System.IO;

namespace NexusCheatFramework.Formats
{
    /// <summary>
    /// Best-effort etaHEN-format cheat parser.
    ///
    /// Status: the etaHEN repository surfaces a "Cheats (WIP)" toolbox entry and
    /// a `libhijacker_cheats` switch in the daemon, but it does not ship a
    /// public, documented cheat-file format or a parser implementation that we
    /// can clean-room translate. Until that format is documented, this parser
    /// throws <see cref="NotSupportedException"/> rather than guess.
    ///
    /// See docs/CHEAT_FORMATS.md for the current compatibility matrix.
    /// </summary>
    public sealed class EtaHenCheatParser : ICheatFormatParser
    {
        public bool CanParse(string path)
        {
            if (string.IsNullOrEmpty(path)) return false;
            // etaHEN itself doesn't define a unique extension; treat .etahen / .ehc as hints.
            return path.EndsWith(".etahen", StringComparison.OrdinalIgnoreCase)
                || path.EndsWith(".ehc",    StringComparison.OrdinalIgnoreCase);
        }

        public CheatFile Parse(string path) => throw Unsupported();
        public CheatFile Parse(Stream stream, string? sourcePath = null) => throw Unsupported();

        private static NotSupportedException Unsupported() =>
            new(
                "etaHEN does not currently publish a documented cheat-file format. " +
                "Use JsonCheatParser with the native NexusCheatFramework JSON format instead. " +
                "If you have an etaHEN-format sample, please file an issue with the spec.");
    }
}
