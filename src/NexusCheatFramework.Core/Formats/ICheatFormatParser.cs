using System.IO;

namespace NexusCheatFramework.Formats
{
    public interface ICheatFormatParser
    {
        /// <summary>True if this parser believes it can parse the given file.</summary>
        bool CanParse(string path);

        CheatFile Parse(string path);
        CheatFile Parse(Stream stream, string? sourcePath = null);
    }
}
