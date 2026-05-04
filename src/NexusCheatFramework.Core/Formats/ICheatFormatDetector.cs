namespace NexusCheatFramework.Formats
{
    /// <summary>
    /// Detects the format of a cheat file from its path and/or content.
    /// </summary>
    public interface ICheatFormatDetector
    {
        CheatFormatDetectionResult Detect(string path);
    }
}
