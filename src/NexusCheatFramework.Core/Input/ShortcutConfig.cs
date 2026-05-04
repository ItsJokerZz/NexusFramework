using System;
using System.Collections.Generic;

namespace NexusCheatFramework.Input
{
    /// <summary>
    /// Cheat-menu shortcut presets, mirroring etaHEN's Cheats_Shortcut enum
    /// so existing user-facing labels stay familiar.
    /// </summary>
    public enum CheatsShortcutMode
    {
        Off = 0,
        HoldR3L3 = 1,
        HoldL2Triangle = 2,
        LongHoldOptions = 3,
        LongHoldShare = 4,
        SingleTapShare = 5,
    }

    /// <summary>
    /// Trigger modes for the cheat manager overlay open/close.
    /// Extends the existing shortcut concept with manager-specific triggers.
    /// </summary>
    public enum CheatManagerShortcut
    {
        /// <summary>No controller trigger — use WebUI hotkey or HTTP instead.</summary>
        Off = 0,
        /// <summary>Hold L1 + R1 + Square simultaneously.</summary>
        HoldL1R1Square = 1,
        /// <summary>Hold L1 + R1 + Triangle simultaneously.</summary>
        HoldL1R1Triangle = 2,
        /// <summary>Hold R3 + L3 simultaneously (same as CheatsShortcutMode.HoldR3L3).</summary>
        HoldR3L3 = 3,
        /// <summary>Hold L2 + Triangle simultaneously (same as CheatsShortcutMode.HoldL2Triangle).</summary>
        HoldL2Triangle = 4,
        /// <summary>Long-hold Options button for 2 seconds.</summary>
        LongHoldOptions = 5,
        /// <summary>Long-hold Share button for 2 seconds.</summary>
        LongHoldShare = 6,
        /// <summary>Custom chord defined by CustomOpenChord and CustomChordHoldMs.</summary>
        Custom = 7,
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

        // ----- Cheat Manager Shortcut properties (v1 config) -----

        /// <summary>Schema version for config persistence. 0 = legacy, 1 = current.</summary>
        public int SchemaVersion { get; set; } = 1;

        /// <summary>Controller trigger that opens the cheat manager overlay.</summary>
        public CheatManagerShortcut CheatManagerTrigger { get; set; } = CheatManagerShortcut.HoldL1R1Square;

        /// <summary>Controller trigger that closes the cheat manager overlay. Defaults to same as open.</summary>
        public CheatManagerShortcut CloseTrigger { get; set; } = CheatManagerShortcut.HoldL1R1Square;

        /// <summary>
        /// Custom chord: set of buttons that must all be held simultaneously.
        /// Only used when <see cref="CheatManagerTrigger"/> is <see cref="CheatManagerShortcut.Custom"/>.
        /// </summary>
        public IReadOnlyList<PadButton> CustomOpenChord { get; set; } = Array.Empty<PadButton>();

        /// <summary>
        /// How long (in ms) the custom chord must be held to trigger.
        /// Only used when <see cref="CheatManagerTrigger"/> is <see cref="CheatManagerShortcut.Custom"/>.
        /// </summary>
        public int CustomChordHoldMs { get; set; } = 200;

        /// <summary>
        /// Returns the effective hold duration for the cheat manager trigger.
        /// For LongHoldOptions and LongHoldShare, returns 2000ms.
        /// For Custom, returns CustomChordHoldMs.
        /// For all others, returns HoldDuration.
        /// </summary>
        public TimeSpan GetEffectiveHoldDuration(CheatManagerShortcut trigger)
        {
            return trigger switch
            {
                CheatManagerShortcut.LongHoldOptions => TimeSpan.FromMilliseconds(2000),
                CheatManagerShortcut.LongHoldShare => TimeSpan.FromMilliseconds(2000),
                CheatManagerShortcut.Custom => TimeSpan.FromMilliseconds(CustomChordHoldMs > 0 ? CustomChordHoldMs : 200),
                _ => HoldDuration,
            };
        }
    }
}
