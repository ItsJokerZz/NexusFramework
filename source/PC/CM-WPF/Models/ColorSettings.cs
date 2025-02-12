using System.Windows;
using System.Windows.Media;

namespace ConsoleManager.Models
{
    public class ColorSettings
    {
        private static readonly Properties.Settings Settings = Properties.Settings.Default;

        private static string? GetDefaultColor(string resourceKey) =>
            Application.Current?.Resources[resourceKey] is Color color
                ? $"#{color.R:X2}{color.G:X2}{color.B:X2}"
                : null;

        private static string? GetColor(string key, string resourceKey) =>
            Settings[key] as string ?? GetDefaultColor(resourceKey);

        private static void SetColor(string key, string value)
        {
            Settings[key] = value;
            Settings.Save();
        }

#pragma warning disable CS8604 // Possible null reference argument.
        public string? PrimaryColor { get => GetColor(nameof(Settings.PrimaryColor), "PrimaryColor"); set => SetColor(nameof(Settings.PrimaryColor), value); }
        public string? PrimaryColorDark { get; set; }
        public string? GrayColor { get => GetColor(nameof(Settings.GrayColor), "GrayColor"); set => SetColor(nameof(Settings.GrayColor), value); }
        public string? TextColor { get => GetColor(nameof(Settings.TextColor), "TextColor"); set => SetColor(nameof(Settings.TextColor), value); }
        public string? BackgroundColor { get => GetColor(nameof(Settings.BackgroundColor), "DarkColor"); set => SetColor(nameof(Settings.BackgroundColor), value); }
        public string? CardColor { get => GetColor(nameof(Settings.CardColor), "DarkerColor"); set => SetColor(nameof(Settings.CardColor), value); }
        public string? ShadowColor { get => GetColor(nameof(Settings.ShadowColor), "DarkestColor"); set => SetColor(nameof(Settings.ShadowColor), value); }
        public string? TextSecondaryColor { get => GetColor(nameof(Settings.TextSecondaryColor), "TextSecondaryColor"); set => SetColor(nameof(Settings.TextSecondaryColor), value); }
#pragma warning restore CS8604 // Possible null reference argument.

        public static bool IsDarkMode { get => Settings.IsDarkMode; set { Settings.IsDarkMode = value; Settings.Save(); } }

        public static ColorSettings Load() => new();
        
        public static void Save() => Settings.Save();
    }
}