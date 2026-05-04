using System.IO;
using System.Text;
using NexusCheatFramework.Formats;
using Xunit;

namespace NexusCheatFramework.Tests;

public class CheatParserTests
{
    private static CheatFile ParseString(string s)
    {
        using var ms = new MemoryStream(Encoding.UTF8.GetBytes(s));
        return new JsonCheatParser().Parse(ms);
    }

    [Fact]
    public void Parses_minimal_valid_file()
    {
        var f = ParseString(@"{
            ""titleId"": ""CUSA00000"",
            ""cheats"": [{
                ""id"":""x"", ""name"":""X"",
                ""codes"":[{""type"":""write_bytes"",""address"":""0x1000"",""bytes"":""90 90""}]
            }]
        }");
        Assert.Equal("CUSA00000", f.TitleId);
        Assert.Single(f.Cheats);
        Assert.Equal(CheatCodeType.WriteBytes, f.Cheats[0].Codes[0].Type);
        Assert.Equal(0x1000UL, f.Cheats[0].Codes[0].Address);
        Assert.Equal(new byte[] { 0x90, 0x90 }, f.Cheats[0].Codes[0].Bytes);
    }

    [Fact]
    public void Rejects_missing_title_id()
        => Assert.Throws<System.FormatException>(() => ParseString(@"{ ""cheats"": [] }"));

    [Fact]
    public void Rejects_invalid_json()
        => Assert.ThrowsAny<System.Exception>(() => ParseString("not-json"));

    [Fact]
    public void Rejects_cheat_with_no_codes()
        => Assert.Throws<System.FormatException>(() => ParseString(@"{
            ""titleId"":""CUSA00000"",
            ""cheats"":[{""id"":""x"",""name"":""X"",""codes"":[]}]
        }"));

    [Theory]
    [InlineData("aob_write_bytes", CheatCodeType.AobWriteBytes)]
    [InlineData("AobWriteBytes", CheatCodeType.AobWriteBytes)]
    [InlineData("write_value", CheatCodeType.WriteValue)]
    public void Accepts_snake_and_camel_type_names(string typeName, CheatCodeType expected)
    {
        var f = ParseString(@"{
            ""titleId"":""CUSA1"",
            ""cheats"":[{""id"":""x"",""name"":""X"",
                ""codes"":[{""type"":""" + typeName + @""",""address"":""0"",""bytes"":""90"",""valueType"":""i32"",""value"":""0""}]}]}");
        Assert.Equal(expected, f.Cheats[0].Codes[0].Type);
    }

    [Fact]
    public void Hex_byte_string_parses_with_separators()
    {
        Assert.Equal(new byte[] { 0xAA, 0xBB, 0xCC }, JsonCheatParser.ParseHexBytes("AA BB CC"));
        Assert.Equal(new byte[] { 0xAA, 0xBB, 0xCC }, JsonCheatParser.ParseHexBytes("AABBCC"));
        Assert.Equal(new byte[] { 0xAA, 0xBB }, JsonCheatParser.ParseHexBytes("AA-BB"));
    }
}
