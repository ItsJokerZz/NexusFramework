using System.Windows;
using System.Windows.Media;

namespace ConsoleManager.Models
{
    public class ColorSettings
    {
        private static Properties.Settings Settings => Properties.Settings.Default;

        private static string GetDefaultColor(string resourceKey) =>
            Application.Current?.Resources[resourceKey] is Color color
                ? $"#{color.R:X2}{color.G:X2}{color.B:X2}"
                : null;

        private string GetColorSetting(string key, string resourceKey) =>
            typeof(Properties.Settings).GetProperty(key)?.GetValue(Settings) as string ?? GetDefaultColor(resourceKey);

        private void SetColorSetting(string key, string value)
        {
            typeof(Properties.Settings).GetProperty(key)?.SetValue(Settings, value);
            Settings.Save();
        }

        public string PrimaryColor { get => GetColorSetting(nameof(Settings.PrimaryColor), "PrimaryColor"); set => SetColorSetting(nameof(Settings.PrimaryColor), value); }
        public string GrayColor { get => GetColorSetting(nameof(Settings.GrayColor), "GrayColor"); set => SetColorSetting(nameof(Settings.GrayColor), value); }
        public string TextColor { get => GetColorSetting(nameof(Settings.TextColor), "TextColor"); set => SetColorSetting(nameof(Settings.TextColor), value); }
        public string BackgroundColor { get => GetColorSetting(nameof(Settings.BackgroundColor), "DarkColor"); set => SetColorSetting(nameof(Settings.BackgroundColor), value); }
        public string CardColor { get => GetColorSetting(nameof(Settings.CardColor), "DarkerColor"); set => SetColorSetting(nameof(Settings.CardColor), value); }
        public string ShadowColor { get => GetColorSetting(nameof(Settings.ShadowColor), "DarkestColor"); set => SetColorSetting(nameof(Settings.ShadowColor), value); }
        public string TextSecondaryColor { get => GetColorSetting(nameof(Settings.TextSecondaryColor), "TextSecondaryColor"); set => SetColorSetting(nameof(Settings.TextSecondaryColor), value); }

        public bool IsDarkMode
        {
            get => Settings.IsDarkMode;
            set { Settings.IsDarkMode = value; Settings.Save(); }
        }

        public static ColorSettings Load() => new ColorSettings();
        public void Save() => Settings.Save();
    }
}
