using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace NexusCheatFramework.Formats
{
    /// <summary>
    /// Parses a cheat file from a given format into the native <see cref="CheatFile"/> model.
    /// </summary>
    public interface ICheatFormatParser
    {
        bool CanParse(string path);
        CheatFile? Parse(string path);
        CheatFile? Parse(Stream stream, string? sourcePath = null);
    }
}
