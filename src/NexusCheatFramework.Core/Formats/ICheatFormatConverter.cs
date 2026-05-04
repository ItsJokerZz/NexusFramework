using System.Threading;
using System.Threading.Tasks;

namespace NexusCheatFramework.Formats
{
    /// <summary>
    /// Converts an external cheat format file to the native <see cref="CheatFile"/> model,
    /// with structured issues/warnings.
    /// </summary>
    public interface ICheatFormatConverter
    {
        bool CanConvert(string path);
        CheatConversionResult Convert(string path);
    }
}
