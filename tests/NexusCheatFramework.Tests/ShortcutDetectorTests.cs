using System;
using NexusCheatFramework.Input;
using Xunit;

namespace NexusCheatFramework.Tests;

public class ShortcutDetectorTests
{
    private static (ShortcutDetector det, Counter c) Build(CheatsShortcutMode mode, TimeSpan? hold = null, TimeSpan? debounce = null)
    {
        var cfg = new ShortcutConfig
        {
            Mode = mode,
            Enabled = true,
            HoldDuration = hold ?? TimeSpan.FromMilliseconds(500),
            Debounce = debounce ?? TimeSpan.FromMilliseconds(1000),
            TapMaxDuration = TimeSpan.FromMilliseconds(300),
        };
        var d = new ShortcutDetector(cfg);
        var c = new Counter();
        d.OnTrigger += () => c.Count++;
        return (d, c);
    }

    private sealed class Counter { public int Count; }

    [Fact]
    public void Hold_combo_triggers_after_hold_duration()
    {
        var (d, c) = Build(CheatsShortcutMode.HoldR3L3, hold: TimeSpan.FromMilliseconds(500));
        var t = new DateTime(2024, 1, 1);
        d.Update(PadButton.R3 | PadButton.L3, t);
        Assert.Equal(0, c.Count);
        d.Update(PadButton.R3 | PadButton.L3, t.AddMilliseconds(200));
        Assert.Equal(0, c.Count);
        d.Update(PadButton.R3 | PadButton.L3, t.AddMilliseconds(600));
        Assert.Equal(1, c.Count);
    }

    [Fact]
    public void Long_options_requires_full_hold()
    {
        var (d, c) = Build(CheatsShortcutMode.LongHoldOptions, hold: TimeSpan.FromMilliseconds(750));
        var t = new DateTime(2024, 1, 1);
        d.Update(PadButton.Options, t);
        d.Update(PadButton.Options, t.AddMilliseconds(700));
        Assert.Equal(0, c.Count);
        d.Update(PadButton.Options, t.AddMilliseconds(800));
        Assert.Equal(1, c.Count);
    }

    [Fact]
    public void Single_tap_share_triggers_on_release()
    {
        var (d, c) = Build(CheatsShortcutMode.SingleTapShare);
        var t = new DateTime(2024, 1, 1);
        d.Update(PadButton.Share, t);
        Assert.Equal(0, c.Count);
        d.Update(PadButton.None, t.AddMilliseconds(150));
        Assert.Equal(1, c.Count);
    }

    [Fact]
    public void Single_tap_long_press_does_not_trigger()
    {
        var (d, c) = Build(CheatsShortcutMode.SingleTapShare);
        var t = new DateTime(2024, 1, 1);
        d.Update(PadButton.Share, t);
        d.Update(PadButton.None, t.AddSeconds(2));
        Assert.Equal(0, c.Count);
    }

    [Fact]
    public void Debounce_prevents_repeated_fires()
    {
        var (d, c) = Build(CheatsShortcutMode.HoldR3L3,
            hold: TimeSpan.FromMilliseconds(100),
            debounce: TimeSpan.FromMilliseconds(2000));
        var t = new DateTime(2024, 1, 1);
        d.Update(PadButton.R3 | PadButton.L3, t);
        d.Update(PadButton.R3 | PadButton.L3, t.AddMilliseconds(150));
        Assert.Equal(1, c.Count);
        d.Update(PadButton.None, t.AddMilliseconds(160));
        d.Update(PadButton.R3 | PadButton.L3, t.AddMilliseconds(200));
        d.Update(PadButton.R3 | PadButton.L3, t.AddMilliseconds(400));
        Assert.Equal(1, c.Count); // still one — within debounce window
    }
}
