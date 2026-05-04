using System.IO;
using System.Text;
using NexusCheatFramework.Formats;
using Xunit;

namespace NexusCheatFramework.Tests;

public class JsonCheatParserV02Tests
{
    [Fact]
    public void Parses_basic_cheat_file()
    {
        var json = @"{
  ""titleId"": ""CUSA00001"",
  ""gameName"": ""Test Game"",
  ""version"": ""1.00"",
  ""region"": ""US"",
  ""cheats"": [
    {
      ""id"": ""inf_health"",
      ""name"": ""Infinite Health"",
      ""description"": ""Player health never drops below 999"",
      ""enabledByDefault"": false,
      ""codes"": [
        { ""type"": ""freeze_value"", ""address"": ""0x12345678"", ""valueType"": ""int"", ""value"": ""999"", ""freezeIntervalMs"": 250 }
      ]
    }
  ]
}";

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
        var parser = new JsonCheatParser();
        var file = parser.Parse(stream);

        Assert.Equal("CUSA00001", file.TitleId);
        Assert.Equal("Test Game", file.GameName);
        Assert.Single(file.Cheats);
        var cheat = file.Cheats[0];
        Assert.Equal("inf_health", cheat.Id);
        Assert.Equal("Infinite Health", cheat.Name);
        Assert.Single(cheat.Codes);
        var code = cheat.Codes[0];
        Assert.Equal(CheatCodeType.FreezeValue, code.Type);
        Assert.Equal((ulong)0x12345678, code.Address);
        Assert.Equal("int", code.ValueType);
        Assert.Equal("999", code.Value);
        Assert.Equal(250, code.FreezeIntervalMs);
    }

    [Fact]
    public void Parses_pointer_write()
    {
        var json = @"{
  ""titleId"": ""CUSA00002"",
  ""gameName"": ""Pointer Test"",
  ""cheats"": [
    {
      ""id"": ""ptr_ammo"",
      ""name"": ""Infinite Ammo (Pointer)"",
      ""codes"": [
        {
          ""type"": ""pointer_write_value"",
          ""address"": ""0x100000000"",
          ""pointerOffsets"": [""0x20"", ""0x18"", ""0x40""],
          ""valueType"": ""int"",
          ""value"": ""99""
        }
      ]
    }
  ]
}";

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
        var parser = new JsonCheatParser();
        var file = parser.Parse(stream);

        var code = file.Cheats[0].Codes[0];
        Assert.Equal(CheatCodeType.PointerWriteValue, code.Type);
        Assert.Equal((ulong)0x100000000, code.Address);
        Assert.Equal(3, code.PointerOffsets?.Count);
        Assert.Equal("0x20", code.PointerOffsets![0]);
        Assert.Equal("0x18", code.PointerOffsets[1]);
        Assert.Equal("0x40", code.PointerOffsets[2]);
        Assert.Equal("int", code.ValueType);
        Assert.Equal("99", code.Value);
    }

    [Fact]
    public void Parses_aob_pointer_write_bytes()
    {
        var json = @"{
  ""titleId"": ""CUSA00003"",
  ""cheats"": [
    {
      ""id"": ""aob_ptr"",
      ""name"": ""AOB Pointer NOP"",
      ""codes"": [
        {
          ""type"": ""aob_pointer_write_bytes"",
          ""aobPattern"": ""48 8B ?? ?? 89"",
          ""aobOffset"": 4,
          ""pointerOffsets"": [""0x10"", ""0x28""],
          ""bytes"": ""90 90 90""
        }
      ]
    }
  ]
}";

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
        var parser = new JsonCheatParser();
        var file = parser.Parse(stream);

        var code = file.Cheats[0].Codes[0];
        Assert.Equal(CheatCodeType.AobPointerWriteBytes, code.Type);
        Assert.Equal("48 8B ?? ?? 89", code.AobPattern);
        Assert.Equal(4, code.AobOffset);
        Assert.Equal(2, code.PointerOffsets?.Count);
        Assert.Equal(new byte[] { 0x90, 0x90, 0x90 }, code.Bytes);
    }

    [Fact]
    public void Parses_module_write()
    {
        var json = @"{
  ""titleId"": ""CUSA00004"",
  ""cheats"": [
    {
      ""id"": ""mod_nop"",
      ""name"": ""Module NOP"",
      ""codes"": [
        {
          ""type"": ""module_write_bytes"",
          ""moduleName"": ""eboot.bin"",
          ""offset"": ""0x1234"",
          ""bytes"": ""90""
        }
      ]
    }
  ]
}";

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
        var parser = new JsonCheatParser();
        var file = parser.Parse(stream);

        var code = file.Cheats[0].Codes[0];
        Assert.Equal(CheatCodeType.ModuleWriteBytes, code.Type);
        Assert.Equal("eboot.bin", code.ModuleName);
    }

    [Fact]
    public void Rejects_missing_titleId()
    {
        var json = @"{ ""cheats"": [] }";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
        var parser = new JsonCheatParser();
        Assert.Throws<System.FormatException>(() => parser.Parse(stream));
    }

    [Fact]
    public void Rejects_empty_titleId()
    {
        var json = @"{ ""titleId"": "" "", ""cheats"": [] }";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
        var parser = new JsonCheatParser();
        Assert.Throws<System.FormatException>(() => parser.Parse(stream));
    }

    [Fact]
    public void Rejects_unknown_cheat_type()
    {
        var json = @"{
  ""titleId"": ""CUSA00005"",
  ""cheats"": [{ ""id"": ""x"", ""name"": ""x"", ""codes"": [{ ""type"": ""invalid_type_xyz"" }] }]
}";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
        var parser = new JsonCheatParser();
        Assert.Throws<System.FormatException>(() => parser.Parse(stream));
    }
}
