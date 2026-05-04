using System;

namespace NexusCheatFramework.Input
{
    /// <summary>
    /// Cheat-menu shortcut presets, mirroring etaHEN's Cheats_Shortcut enum
    /// so existing user-facing labels stay familiar.
    /// </summary>
    public enum CheatsShortcutMode
    {
        Off                = 0,
        HoldR3L3           = 1,
        HoldL2Triangle     = 2,
        LongHoldOptions    = 3,
        LongHoldShare      = 4,
        SingleTapShare     = 5,
    }

    public sealed class ShortcutConfig
    {
        public bool Enabled { get; set; } = true;
        public CheatsShortcutMode Mode { get; set; } = CheatsShortcutMode.Off;

        /// <summary>How long buttons must be held to trigger a "long" combo.</summary>
        public TimeSpan HoldDuration { get; set; } = TimeSpan.FromMilliseconds(750);

        /// <summary>Maximum gap for a "tap" on the share/create button.</summary>
        public TimeSpan TapMaxDuration { get; set; } = TimeSpan.FromMilliseconds(300);

        /// <summary>Suppresses repeated firing while buttons are held.</summary>
        public TimeSpan Debounce { get; set; } = TimeSpan.FromMilliseconds(1000);

        /// <summary>Pad polling interval.</summary>
        public TimeSpan PollInterval { get; set; } = TimeSpan.FromMilliseconds(33);
    }
}
