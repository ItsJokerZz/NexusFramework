using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NexusCheatFramework.Core;
using NexusCheatFramework.Formats;
using Xunit;

namespace NexusCheatFramework.Tests;

public class CheatEngineTests
{
    private FakeNexusClient MakeClient(ulong baseAddr = 0x10000)
    {
        var buf = new byte[0x1000];
        // Fill with recognizable pattern
        for (int i = 0; i < buf.Length; i++)
            buf[i] = (byte)(i & 0xFF);
        return new FakeNexusClient(buf, baseAddr);
    }

    [Fact]
    public async Task Enable_write_bytes_applies_patch()
    {
        var client = MakeClient();
        var engine = new CheatEngine(client, new CheatRuntimeOptions { RestoreOriginalBytesOnDisable = true });
        var cheat = new CheatDefinition
        {
            Id = "test1",
            Name = "Test Cheat",
            Codes = { new CheatCode { Type = CheatCodeType.WriteBytes, Address = 0x10000, Bytes = new byte[] { 0xFF, 0xEE, 0xDD } } }
        };

        var r = await engine.EnableAsync(cheat);
        Assert.True(r.Success);
        Assert.Equal(CheatStatus.Enabled, r.Status);
        Assert.Equal(0xFF, client.Buffer[0]);
        Assert.Equal(0xEE, client.Buffer[1]);
        Assert.Equal(0xDD, client.Buffer[2]);
    }

    [Fact]
    public async Task Disable_restores_original_bytes()
    {
        var client = MakeClient();
        var engine = new CheatEngine(client, new CheatRuntimeOptions { RestoreOriginalBytesOnDisable = true });
        var cheat = new CheatDefinition
        {
            Id = "test2",
            Name = "Test",
            Codes = { new CheatCode { Type = CheatCodeType.WriteBytes, Address = 0x10000, Bytes = new byte[] { 0xFF } } }
        };

        await engine.EnableAsync(cheat);
        Assert.Equal(0xFF, client.Buffer[0]);

        var r = await engine.DisableAsync("test2");
        Assert.True(r.Success);
        Assert.Equal(0x00, client.Buffer[0]); // original value
    }

    [Fact]
    public async Task ExpectedBytes_mismatch_refuses_unless_force()
    {
        var client = MakeClient();
        var engine = new CheatEngine(client, new CheatRuntimeOptions { RequireExpectedBytes = true });
        var cheat = new CheatDefinition
        {
            Id = "test3",
            Name = "Test",
            Codes = { new CheatCode { Type = CheatCodeType.WriteBytes, Address = 0x10000, Bytes = new byte[] { 0xFF }, ExpectedBytes = new byte[] { 0x99 } } }
        };

        var r = await engine.EnableAsync(cheat);
        Assert.False(r.Success);
        Assert.Contains("ExpectedBytes mismatch", r.Error);
    }

    [Fact]
    public async Task ExpectedBytes_mismatch_force_applies()
    {
        var client = MakeClient();
        var engine = new CheatEngine(client, new CheatRuntimeOptions { RequireExpectedBytes = true, ForceApply = true });
        var cheat = new CheatDefinition
        {
            Id = "test4",
            Name = "Test",
            Codes = { new CheatCode { Type = CheatCodeType.WriteBytes, Address = 0x10000, Bytes = new byte[] { 0xFF }, ExpectedBytes = new byte[] { 0x99 } } }
        };

        var r = await engine.EnableAsync(cheat);
        Assert.True(r.Success);
        Assert.Equal(0xFF, client.Buffer[0]);
    }

    [Fact]
    public async Task Dry_run_does_not_write()
    {
        var client = MakeClient();
        var engine = new CheatEngine(client, new CheatRuntimeOptions { DryRun = true });
        var cheat = new CheatDefinition
        {
            Id = "test5",
            Name = "Test",
            Codes = { new CheatCode { Type = CheatCodeType.WriteBytes, Address = 0x10000, Bytes = new byte[] { 0xFF } } }
        };

        var r = await engine.EnableAsync(cheat);
        Assert.True(r.Success);
        Assert.NotEqual(0xFF, client.Buffer[0]); // should not have written
    }

    [Fact]
    public async Task Multiple_cheats_independent()
    {
        var client = MakeClient();
        var engine = new CheatEngine(client);

        var cheat1 = new CheatDefinition
        {
            Id = "c1",
            Name = "C1",
            Codes = { new CheatCode { Type = CheatCodeType.WriteBytes, Address = 0x10000, Bytes = new byte[] { 0xAA } } }
        };
        var cheat2 = new CheatDefinition
        {
            Id = "c2",
            Name = "C2",
            Codes = { new CheatCode { Type = CheatCodeType.WriteBytes, Address = 0x10010, Bytes = new byte[] { 0xBB } } }
        };

        Assert.True((await engine.EnableAsync(cheat1)).Success);
        Assert.True((await engine.EnableAsync(cheat2)).Success);
        Assert.Equal(0xAA, client.Buffer[0]);
        Assert.Equal(0xBB, client.Buffer[0x10]);
        Assert.True(engine.IsEnabled("c1"));
        Assert.True(engine.IsEnabled("c2"));

        await engine.DisableAsync("c1");
        Assert.False(engine.IsEnabled("c1"));
        Assert.True(engine.IsEnabled("c2"));
    }

    [Fact]
    public async Task Unknown_cheat_type_returns_error()
    {
        var client = MakeClient();
        var engine = new CheatEngine(client);
        var cheat = new CheatDefinition
        {
            Id = "bad",
            Name = "Bad",
            Codes = { new CheatCode { Type = (CheatCodeType)999 } }
        };

        var r = await engine.EnableAsync(cheat);
        Assert.False(r.Success);
    }

    [Fact]
    public async Task Disable_unapplied_cheat_returns_success()
    {
        var client = MakeClient();
        var engine = new CheatEngine(client);
        var r = await engine.DisableAsync("nonexistent");
        Assert.True(r.Success);
        Assert.Equal(CheatStatus.Disabled, r.Status);
    }

    [Fact]
    public async Task Freeze_value_repeatedly_writes()
    {
        var client = MakeClient();
        var engine = new CheatEngine(client);
        var cheat = new CheatDefinition
        {
            Id = "freeze1",
            Name = "Freeze HP",
            Codes =
            {
                new CheatCode
                {
                    Type = CheatCodeType.FreezeValue,
                    Address = 0x10020,
                    ValueType = "int",
                    Value = "999",
                    FreezeIntervalMs = 10
                }
            }
        };

        var r = await engine.EnableAsync(cheat);
        Assert.True(r.Success);

        // Write something else at the address
        client.Buffer[0x20] = 0x00; client.Buffer[0x21] = 0x00; client.Buffer[0x22] = 0x00; client.Buffer[0x23] = 0x00;

        await Task.Delay(50);
        engine.StopAllFreezes();

        // Should have been frozen back to 999 (0xE7 0x03 0x00 0x00)
        Assert.Equal(0xE7, client.Buffer[0x20]);
        Assert.Equal(0x03, client.Buffer[0x21]);
        Assert.Equal(0x00, client.Buffer[0x22]);
        Assert.Equal(0x00, client.Buffer[0x23]);
    }

    [Fact]
    public async Task Disable_cheat_stops_freeze_loop()
    {
        var client = MakeClient();
        var engine = new CheatEngine(client);
        var cheat = new CheatDefinition
        {
            Id = "freeze2",
            Name = "Freeze",
            Codes =
            {
                new CheatCode
                {
                    Type = CheatCodeType.FreezeValue,
                    Address = 0x10030,
                    ValueType = "int",
                    Value = "42",
                    FreezeIntervalMs = 10
                }
            }
        };

        await engine.EnableAsync(cheat);
        var writesBefore = client.Writes.Count;

        await engine.DisableAsync("freeze2");
        client.Buffer[0x30] = 0x00; client.Buffer[0x31] = 0x00; client.Buffer[0x32] = 0x00; client.Buffer[0x33] = 0x00;

        await Task.Delay(100);
        // Freeze should have stopped — value should NOT have been restored
        Assert.Equal(0x00, client.Buffer[0x30]);
        Assert.Equal(0x00, client.Buffer[0x31]);
    }
}
