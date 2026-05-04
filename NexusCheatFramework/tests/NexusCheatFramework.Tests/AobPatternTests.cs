using System;
using NexusCheatFramework.Memory;
using Xunit;

namespace NexusCheatFramework.Tests;

public class AobPatternTests
{
    [Fact] public void Parses_exact_bytes() {
        var p = AobPattern.Parse("48 8B 05");
        Assert.Equal(3, p.Length);
        Assert.Equal(0x48, p.Bytes[0]); Assert.Equal(0x8B, p.Bytes[1]); Assert.Equal(0x05, p.Bytes[2]);
        Assert.Equal(0, p.FirstSolidIndex);
    }

    [Theory]
    [InlineData("48 8B ?? ?? 89")]
    [InlineData("48 8B ? ? 89")]
    [InlineData("48 8B ** ** 89")]
    public void Parses_wildcards(string s) {
        var p = AobPattern.Parse(s);
        Assert.Equal(5, p.Length);
        Assert.Null(p.Bytes[2]); Assert.Null(p.Bytes[3]);
        Assert.Equal(0x48, p.Bytes[0]); Assert.Equal(0x89, p.Bytes[4]);
    }

    [Fact] public void Parses_run_together() {
        var p = AobPattern.Parse("488B????89");
        Assert.Equal(5, p.Length);
        Assert.Equal(0x48, p.Bytes[0]); Assert.Null(p.Bytes[2]); Assert.Equal(0x89, p.Bytes[4]);
    }

    [Fact] public void Empty_pattern_throws() => Assert.Throws<ArgumentException>(() => AobPattern.Parse(""));

    [Theory]
    [InlineData("48 ZZ")]
    [InlineData("48 8")]
    [InlineData("XYZ")]
    public void Invalid_hex_throws(string s) => Assert.Throws<ArgumentException>(() => AobPattern.Parse(s));

    [Fact] public void Matches_against_buffer() {
        var p = AobPattern.Parse("48 8B ?? 89");
        var buf = new byte[] { 0x00, 0x48, 0x8B, 0x05, 0x89, 0xFF };
        Assert.True(p.Matches(buf, 1));
        Assert.False(p.Matches(buf, 0));
    }

    [Fact] public void All_wildcard_pattern_has_no_solid_anchor() {
        var p = AobPattern.Parse("?? ?? ??");
        Assert.Equal(-1, p.FirstSolidIndex);
        Assert.True(p.Matches(new byte[] { 0xAA, 0xBB, 0xCC }, 0));
    }
}
