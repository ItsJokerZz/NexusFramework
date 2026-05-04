using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NexusCheatFramework.Core;
using NexusCheatFramework.Formats;
using NexusCheatFramework.Nexus;
using Xunit;

namespace NexusCheatFramework.Tests;

public class CheatEngineTests
{
    private FakeNexusClient MakeClient(ulong baseAddr = 0x10000)
    {
        var buf = new byte[0x1000];
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
        Assert.Equal(0x00, client.Buffer[0]);
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
        Assert.NotEqual(0xFF, client.Buffer[0]);
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

        client.Buffer[0x20] = 0x00; client.Buffer[0x21] = 0x00;
        client.Buffer[0x22] = 0x00; client.Buffer[0x23] = 0x00;

        await Task.Delay(50);
        engine.StopAllFreezes();

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
        await engine.DisableAsync("freeze2");
        client.Buffer[0x30] = 0x00; client.Buffer[0x31] = 0x00;
        client.Buffer[0x32] = 0x00; client.Buffer[0x33] = 0x00;

        await Task.Delay(100);
        Assert.Equal(0x00, client.Buffer[0x30]);
        Assert.Equal(0x00, client.Buffer[0x31]);
    }

    // ============ Pointer chain tests ============

    [Fact]
    public async Task Pointer_write_value_resolves_chain_and_writes()
    {
        var buf = new byte[0x5000];
        var client = new FakeNexusClient(buf, 0x10000);

        Array.Copy(BitConverter.GetBytes((ulong)0x10200), 0, buf, 0x100, 8);
        Array.Copy(BitConverter.GetBytes((ulong)0x10300), 0, buf, 0x220, 8);

        var engine = new CheatEngine(client);
        var cheat = new CheatDefinition
        {
            Id = "ptr1",
            Name = "Pointer Chain Value",
            Codes =
            {
                new CheatCode
                {
                    Type = CheatCodeType.PointerWriteValue,
                    Address = 0x10000,
                    PointerOffsets = new List<string> { "0x100", "0x20", "0x0" },
                    ValueType = "int",
                    Value = "9999"
                }
            }
        };

        var r = await engine.EnableAsync(cheat);
        Assert.True(r.Success);
        Assert.Equal(0x0F, buf[0x300]);
        Assert.Equal(0x27, buf[0x301]);
    }

    [Fact]
    public async Task Pointer_write_bytes_resolves_chain_and_writes()
    {
        var buf = new byte[0x5000];
        var client = new FakeNexusClient(buf, 0x10000);

        Array.Copy(BitConverter.GetBytes((ulong)0x10100), 0, buf, 0x50, 8);

        var engine = new CheatEngine(client);
        var cheat = new CheatDefinition
        {
            Id = "ptr2",
            Name = "Pointer Chain Bytes",
            Codes =
            {
                new CheatCode
                {
                    Type = CheatCodeType.PointerWriteBytes,
                    Address = 0x10000,
                    PointerOffsets = new List<string> { "0x50", "0x10" },
                    Bytes = new byte[] { 0xAA, 0xBB, 0xCC }
                }
            }
        };

        var r = await engine.EnableAsync(cheat);
        Assert.True(r.Success);
        Assert.Equal(0xAA, buf[0x110]);
        Assert.Equal(0xBB, buf[0x111]);
        Assert.Equal(0xCC, buf[0x112]);
    }

    [Fact]
    public async Task Pointer_write_bytes_null_pointer_fails()
    {
        var buf = new byte[0x5000];
        var client = new FakeNexusClient(buf, 0x10000);

        Array.Copy(BitConverter.GetBytes((ulong)0), 0, buf, 0x100, 8);

        var engine = new CheatEngine(client);
        var cheat = new CheatDefinition
        {
            Id = "ptr_null",
            Name = "Null Pointer",
            Codes =
            {
                new CheatCode
                {
                    Type = CheatCodeType.PointerWriteBytes,
                    Address = 0x10000,
                    PointerOffsets = new List<string> { "0x100", "0x0" },
                    Bytes = new byte[] { 0xFF }
                }
            }
        };

        var r = await engine.EnableAsync(cheat);
        Assert.False(r.Success);
        Assert.Contains("null pointer", r.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Pointer_write_bytes_invalid_final_address_fails()
    {
        var buf = new byte[0x100];
        var client = new FakeNexusClient(buf, 0x10000);

        Array.Copy(BitConverter.GetBytes((ulong)0x20000), 0, buf, 0x50, 8);

        var engine = new CheatEngine(client);
        var cheat = new CheatDefinition
        {
            Id = "ptr_bad_addr",
            Name = "Bad Final Address",
            Codes =
            {
                new CheatCode
                {
                    Type = CheatCodeType.PointerWriteBytes,
                    Address = 0x10000,
                    PointerOffsets = new List<string> { "0x50", "0x0" },
                    Bytes = new byte[] { 0xFF }
                }
            }
        };

        var r = await engine.EnableAsync(cheat);
        Assert.False(r.Success);
    }

    [Fact]
    public async Task Pointer_write_value_negative_offsets()
    {
        var buf = new byte[0x2000];
        var client = new FakeNexusClient(buf, 0x10000);

        Array.Copy(BitConverter.GetBytes((ulong)0x10200), 0, buf, 0x100, 8);

        var engine = new CheatEngine(client);
        var cheat = new CheatDefinition
        {
            Id = "ptr_neg",
            Name = "Negative Offset",
            Codes =
            {
                new CheatCode
                {
                    Type = CheatCodeType.PointerWriteValue,
                    Address = 0x10000,
                    PointerOffsets = new List<string> { "0x100", "-0x20" },
                    ValueType = "int",
                    Value = "42"
                }
            }
        };

        var r = await engine.EnableAsync(cheat);
        Assert.True(r.Success);
        Assert.Equal(0x2A, buf[0x1E0]);
    }

    [Fact]
    public async Task Aob_pointer_write_value_resolves_and_writes()
    {
        var buf = new byte[0x2000];
        var client = new FakeNexusClient(buf, 0x10000);
        buf[0x00] = 0x48; buf[0x01] = 0x8B; buf[0x02] = 0x05; buf[0x03] = 0x89;

        Array.Copy(BitConverter.GetBytes((ulong)0x10100), 0, buf, 0x04, 8);

        var engine = new CheatEngine(client);
        var cheat = new CheatDefinition
        {
            Id = "aob_ptr1",
            Name = "AOB Ptr Value",
            Codes =
            {
                new CheatCode
                {
                    Type = CheatCodeType.AobPointerWriteValue,
                    AobPattern = "48 8B 05 89",
                    AobOffset = 4,
                    PointerOffsets = new List<string> { "0x0", "0x0" },
                    ValueType = "int",
                    Value = "777"
                }
            }
        };

        var r = await engine.EnableAsync(cheat);
        Assert.True(r.Success);
        Assert.Equal(0x09, buf[0x100]);
        Assert.Equal(0x03, buf[0x101]);
    }

    [Fact]
    public async Task Aob_pointer_write_bytes_resolves_and_writes()
    {
        var buf = new byte[0x2000];
        var client = new FakeNexusClient(buf, 0x10000);
        buf[0x00] = 0x48; buf[0x01] = 0x8B; buf[0x02] = 0x05; buf[0x03] = 0x89;

        Array.Copy(BitConverter.GetBytes((ulong)0x10100), 0, buf, 0x04, 8);

        var engine = new CheatEngine(client);
        var cheat = new CheatDefinition
        {
            Id = "aob_ptr2",
            Name = "AOB Ptr Bytes",
            Codes =
            {
                new CheatCode
                {
                    Type = CheatCodeType.AobPointerWriteBytes,
                    AobPattern = "48 8B 05 89",
                    AobOffset = 4,
                    PointerOffsets = new List<string> { "0x0", "0x0" },
                    Bytes = new byte[] { 0x11, 0x22, 0x33 }
                }
            }
        };

        var r = await engine.EnableAsync(cheat);
        Assert.True(r.Success);
        Assert.Equal(0x11, buf[0x100]);
        Assert.Equal(0x22, buf[0x101]);
        Assert.Equal(0x33, buf[0x102]);
    }

    // ============ Freeze dynamic resolution tests ============

    [Fact]
    public async Task Freeze_with_moduleName_resolves_and_freezes()
    {
        var buf = new byte[0x1000];
        var client = new FakeNexusClient(buf, 0x10000);
        client.SetExtraRegion("fake.eboot.text", 0x10000, 0x11000, MemoryProtection.Read | MemoryProtection.Execute);
        buf[0x400] = 0x00; buf[0x401] = 0x00;

        var engine = new CheatEngine(client);
        var cheat = new CheatDefinition
        {
            Id = "freeze_mod",
            Name = "Freeze via Module",
            Codes =
            {
                new CheatCode
                {
                    Type = CheatCodeType.FreezeValue,
                    ModuleName = "fake.eboot.text",
                    Offset = 0x400,
                    ValueType = "short",
                    Value = "256",
                    FreezeIntervalMs = 10
                }
            }
        };

        var r = await engine.EnableAsync(cheat);
        Assert.True(r.Success);

        buf[0x400] = 0x00; buf[0x401] = 0x00;
        await Task.Delay(50);
        engine.StopAllFreezes();

        Assert.Equal(0x00, buf[0x400]);
        Assert.Equal(0x01, buf[0x401]);
    }

    [Fact]
    public async Task Freeze_with_aobPattern_resolves_and_freezes()
    {
        var buf = new byte[0x1000];
        var client = new FakeNexusClient(buf, 0x10000);
        buf[0x00] = 0x48; buf[0x01] = 0x8B; buf[0x02] = 0x05; buf[0x03] = 0x89;
        buf[0x100] = 0x00; buf[0x101] = 0x00;

        var engine = new CheatEngine(client);
        var cheat = new CheatDefinition
        {
            Id = "freeze_aob",
            Name = "Freeze via AOB",
            Codes =
            {
                new CheatCode
                {
                    Type = CheatCodeType.FreezeValue,
                    AobPattern = "48 8B 05 89",
                    Offset = 0x100,
                    ValueType = "short",
                    Value = "999",
                    FreezeIntervalMs = 10
                }
            }
        };

        var r = await engine.EnableAsync(cheat);
        Assert.True(r.Success);

        buf[0x100] = 0x00; buf[0x101] = 0x00;
        await Task.Delay(50);
        engine.StopAllFreezes();

        Assert.Equal(0xE7, buf[0x100]);
        Assert.Equal(0x03, buf[0x101]);
    }

    [Fact]
    public async Task Freeze_with_pointerOffsets_resolves_and_freezes()
    {
        var buf = new byte[0x2000];
        var client = new FakeNexusClient(buf, 0x10000);

        Array.Copy(BitConverter.GetBytes((ulong)0x10200), 0, buf, 0x100, 8);
        buf[0x250] = 0x00;

        var engine = new CheatEngine(client);
        var cheat = new CheatDefinition
        {
            Id = "freeze_ptr",
            Name = "Freeze via Pointer",
            Codes =
            {
                new CheatCode
                {
                    Type = CheatCodeType.FreezeValue,
                    Address = 0x10000,
                    PointerOffsets = new List<string> { "0x100", "0x50" },
                    ValueType = "byte",
                    Value = "128",
                    FreezeIntervalMs = 10
                }
            }
        };

        var r = await engine.EnableAsync(cheat);
        Assert.True(r.Success);

        buf[0x250] = 0x00;
        await Task.Delay(50);
        engine.StopAllFreezes();

        Assert.Equal(128, buf[0x250]);
    }

    [Fact]
    public async Task Freeze_with_aobPattern_and_pointerOffsets_resolves_and_freezes()
    {
        var buf = new byte[0x2000];
        var client = new FakeNexusClient(buf, 0x10000);
        buf[0x00] = 0x48; buf[0x01] = 0x8B; buf[0x02] = 0x05; buf[0x03] = 0x89;

        Array.Copy(BitConverter.GetBytes((ulong)0x10100), 0, buf, 0x04, 8);
        // Write to the final resolved address: 0x10100 + 0x20 = 0x10120 → buf[0x120]
        buf[0x120] = 0x00;

        var engine = new CheatEngine(client);
        var cheat = new CheatDefinition
        {
            Id = "freeze_aob_ptr",
            Name = "Freeze via AOB+Ptr",
            Codes =
            {
                new CheatCode
                {
                    Type = CheatCodeType.FreezeValue,
                    AobPattern = "48 8B 05 89",
                    // AOB matches at 0x10000, start pointer chain at AOB base + 0 = 0x10000
                    // First offset dereferences at 0x10000 + 0x4 = 0x10004 → reads 0x10100
                    // Last offset adds: 0x10100 + 0x20 → final address = 0x10120
                    AobOffset = 0,
                    PointerOffsets = new List<string> { "0x4", "0x20" },
                    ValueType = "byte",
                    Value = "255",
                    FreezeIntervalMs = 10
                }
            }
        };

        var r = await engine.EnableAsync(cheat);
        Assert.True(r.Success);

        buf[0x120] = 0x00;
        await Task.Delay(50);
        engine.StopAllFreezes();

        Assert.Equal(255, buf[0x120]);
    }
}
