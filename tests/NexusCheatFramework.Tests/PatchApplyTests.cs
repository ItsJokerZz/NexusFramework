using System.Threading.Tasks;
using NexusCheatFramework.Core;
using NexusCheatFramework.Formats;
using Xunit;

namespace NexusCheatFramework.Tests;

public class PatchApplyTests
{
    [Fact]
    public async Task Stores_original_bytes_and_writes_patch()
    {
        var buf = new byte[] { 0x00, 0xF3, 0x0F, 0x11, 0x83, 0x00, 0x00, 0x00, 0x00, 0x55 };
        var fake = new FakeNexusClient(buf);
        var engine = new CheatEngine(fake);
        var def = new CheatDefinition
        {
            Id = "test",
            Name = "test",
            Codes = { new CheatCode {
                Type = CheatCodeType.WriteBytes,
                Address = fake.Base + 1,
                Bytes = new byte[] { 0x90, 0x90, 0x90, 0x90 }
            } }
        };
        var r = await engine.EnableAsync(def);
        Assert.True(r.Success);
        Assert.Single(r.AppliedCodes);
        Assert.Equal(new byte[] { 0xF3, 0x0F, 0x11, 0x83 }, r.AppliedCodes[0].OriginalBytes);
        Assert.Equal(new byte[] { 0x90, 0x90, 0x90, 0x90 }, r.AppliedCodes[0].PatchBytes);
        Assert.Equal(0x90, buf[1]);
    }

    [Fact]
    public async Task Restore_writes_original_bytes_back()
    {
        var buf = new byte[] { 0x11, 0x22, 0x33 };
        var fake = new FakeNexusClient(buf);
        var engine = new CheatEngine(fake);
        var def = new CheatDefinition
        {
            Id = "t",
            Name = "t",
            Codes = { new CheatCode { Type = CheatCodeType.WriteBytes, Address = fake.Base, Bytes = new byte[] { 0xAA } } },
        };
        await engine.EnableAsync(def);
        Assert.Equal(0xAA, buf[0]);
        var r = await engine.DisableAsync("t");
        Assert.True(r.Success);
        Assert.Equal(0x11, buf[0]);
    }

    [Fact]
    public async Task Refuses_when_expected_bytes_mismatch()
    {
        var buf = new byte[] { 0xDE, 0xAD };
        var fake = new FakeNexusClient(buf);
        var engine = new CheatEngine(fake);
        var def = new CheatDefinition
        {
            Id = "t",
            Name = "t",
            Codes = { new CheatCode {
                Type = CheatCodeType.WriteBytes,
                Address = fake.Base,
                Bytes = new byte[] { 0x90, 0x90 },
                ExpectedBytes = new byte[] { 0xCA, 0xFE },
            } }
        };
        var r = await engine.EnableAsync(def);
        Assert.False(r.Success);
        Assert.Equal(0xDE, buf[0]); // unchanged
    }

    [Fact]
    public async Task Force_apply_ignores_expected_mismatch()
    {
        var buf = new byte[] { 0xDE, 0xAD };
        var fake = new FakeNexusClient(buf);
        var engine = new CheatEngine(fake, new CheatRuntimeOptions { ForceApply = true });
        var def = new CheatDefinition
        {
            Id = "t",
            Name = "t",
            Codes = { new CheatCode {
                Type = CheatCodeType.WriteBytes,
                Address = fake.Base,
                Bytes = new byte[] { 0x90, 0x90 },
                ExpectedBytes = new byte[] { 0xCA, 0xFE },
            } }
        };
        var r = await engine.EnableAsync(def);
        Assert.True(r.Success);
        Assert.Equal(0x90, buf[0]);
    }
}
