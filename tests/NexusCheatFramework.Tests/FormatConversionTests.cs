using System.IO;
using System.Linq;
using System.Text;
using NexusCheatFramework.Formats;
using Xunit;

namespace NexusCheatFramework.Tests;

public class FormatConversionTests
{
    // ============ Format Detection ============

    [Fact]
    public void Detects_native_json()
    {
        var path = CreateTempJson("{\"titleId\":\"CUSA00001\",\"cheats\":[{\"id\":\"c1\",\"name\":\"C1\",\"codes\":[{\"type\":\"write_bytes\",\"address\":\"0x10000\",\"bytes\":\"90\"}]}]}");
        var detector = new CheatFormatDetector();
        var result = detector.Detect(path);
        Assert.Equal("native-json", result.Format);
        Assert.True(result.IsSupported);
    }

    [Fact]
    public void Detects_etahen_json()
    {
        var path = CreateTempJson("{\"name\":\"Test Game\",\"id\":\"CUSA00004\",\"mods\":[{\"name\":\"Godmode\",\"type\":\"checkbox\",\"memory\":[{\"offset\":\"1234\",\"on\":\"90\",\"off\":\"C4\"}]}]}");
        var detector = new CheatFormatDetector();
        var result = detector.Detect(path);
        Assert.Equal("etahen-json", result.Format);
        Assert.True(result.IsSupported);
    }

    [Fact]
    public void Detects_shn_xml()
    {
        var path = Path.GetTempFileName() + ".shn";
        File.WriteAllText(path, "<?xml version=\"1.0\"?><Trainer Cusa=\"CUSA00009\" Game=\"Test\"></Trainer>");
        try
        {
            var detector = new CheatFormatDetector();
            var result = detector.Detect(path);
            Assert.Equal("shn-xml", result.Format);
            Assert.True(result.IsSupported);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Detects_mc4()
    {
        var path = Path.GetTempFileName() + ".mc4";
        File.WriteAllText(path, "UE9XRVJQQUNLIE1DNCB2MS4w");
        try
        {
            var detector = new CheatFormatDetector();
            var result = detector.Detect(path);
            Assert.Equal("mc4", result.Format);
            Assert.False(result.IsSupported);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Detects_unknown_format()
    {
        var path = Path.GetTempFileName() + ".xyz";
        File.WriteAllText(path, "some data");
        try
        {
            var detector = new CheatFormatDetector();
            var result = detector.Detect(path);
            Assert.Equal("unknown", result.Format);
            Assert.False(result.IsSupported);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Detects_unknown_json()
    {
        var path = CreateTempJson("{\"someKey\":\"someValue\"}");
        var detector = new CheatFormatDetector();
        var result = detector.Detect(path);
        Assert.Equal("json-unknown", result.Format);
        Assert.False(result.IsSupported);
    }

    // ============ etaHEN JSON Conversion ============

    [Fact]
    public void Converts_etahen_json_to_native()
    {
        var path = CreateTempJson(@"{
            ""name"": ""inFAMOUS: Second Son"",
            ""id"": ""CUSA00004"",
            ""version"": ""01.07"",
            ""process"": ""eboot.bin"",
            ""mods"": [{
                ""name"": ""Godmode"",
                ""hint"": null,
                ""type"": ""checkbox"",
                ""memory"": [{
                    ""offset"": ""615c17"",
                    ""on"": ""909090909090909090"",
                    ""off"": ""C4C17A119650EE0100""
                }]
            }]
        }");

        var converter = new CheatFormatConverter();
        var result = converter.Convert(path);
        Assert.True(result.Success);
        Assert.Equal("etahen-json", result.SourceFormat);

        var file = result.CheatFile!;
        Assert.Equal("CUSA00004", file.TitleId);
        Assert.Equal("inFAMOUS: Second Son", file.GameName);
        Assert.Single(file.Cheats);
        var cheat = file.Cheats[0];
        Assert.Equal("godmode", cheat.Id);
        Assert.Equal(CheatActivationType.Checkbox, cheat.Codes[0].ActivationType);
        Assert.Equal(CheatCodeType.ModuleWriteBytes, cheat.Codes[0].Type);
        Assert.Equal("eboot.bin", cheat.Codes[0].ModuleName);
        Assert.Equal(new byte[] { 0x90, 0x90, 0x90, 0x90, 0x90, 0x90, 0x90, 0x90, 0x90 }, cheat.Codes[0].Bytes);
        Assert.Equal(new byte[] { 0xC4, 0xC1, 0x7A, 0x11, 0x96, 0x50, 0xEE, 0x01, 0x00 }, cheat.Codes[0].ExpectedBytes);
        Assert.Equal(cheat.Codes[0].ExpectedBytes, cheat.Codes[0].RestoreBytes);
    }

    [Fact]
    public void Converts_etahen_button_mods()
    {
        var path = CreateTempJson(@"{
            ""id"": ""CUSA00005"",
            ""mods"": [{
                ""name"": ""One Shot"",
                ""type"": ""button"",
                ""memory"": [{
                    ""offset"": ""1234"",
                    ""on"": ""A0E68745"",
                    ""off"": ""A0E68745""
                }]
            }]
        }");

        var converter = new CheatFormatConverter();
        var result = converter.Convert(path);
        Assert.True(result.Success);
        var cheat = result.CheatFile!.Cheats[0];
        Assert.Equal(CheatActivationType.Button, cheat.Codes[0].ActivationType);
    }

    [Fact]
    public void Converts_etahen_all_memory_entries()
    {
        var path = CreateTempJson(@"{
            ""id"": ""CUSA00006"",
            ""mods"": [{
                ""name"": ""Multi Entry"",
                ""type"": ""checkbox"",
                ""memory"": [
                    {""offset"": ""100"", ""on"": ""90"", ""off"": ""C4""},
                    {""offset"": ""200"", ""on"": ""FF"", ""off"": ""00""}
                ]
            }]
        }");

        var converter = new CheatFormatConverter();
        var result = converter.Convert(path);
        Assert.True(result.Success);
        Assert.Equal(2, result.CheatFile!.Cheats[0].Codes.Count);
    }

    [Fact]
    public void Etahen_conversion_stable_id()
    {
        var path = CreateTempJson(@"{
            ""id"": ""CUSA00007"",
            ""mods"": [{
                ""name"": ""Max Level True Hero (turn off Infamous)"",
                ""type"": ""checkbox"",
                ""memory"": [{""offset"": ""100"", ""on"": ""90"", ""off"": ""C4""}]
            }]
        }");

        var converter = new CheatFormatConverter();
        var result = converter.Convert(path);
        Assert.True(result.Success);
        Assert.Equal("max_level_true_hero_turn_off_infamous", result.CheatFile!.Cheats[0].Id);
    }

    // ============ SHN XML Conversion ============

    [Fact]
    public void Converts_shn_xml_to_native()
    {
        var path = Path.GetTempFileName() + ".shn";
        // Write as UTF-16 with BOM to match the encoding declaration in the XML
        var xmlContent = @"<?xml version=""1.0"" encoding=""utf-16""?>
<Trainer Game=""Assassins Creed - Black Flag"" Moder=""TLH"" Cusa=""CUSA00009"" Version=""01.04"" Process=""eboot.bin"">
  <Cheat Text=""Infinite Money"" Description="""">
    <Cheatline>
      <Offset>27693CA</Offset>
      <Section>0</Section>
      <ValueOn>90-90-90</ValueOn>
      <ValueOff>89-47-10</ValueOff>
    </Cheatline>
  </Cheat>
</Trainer>";
        File.WriteAllBytes(path, Encoding.Unicode.GetPreamble()
            .Concat(Encoding.Unicode.GetBytes(xmlContent)).ToArray());
        try
        {
            var converter = new CheatFormatConverter();
            var result = converter.Convert(path);
            Assert.True(result.Success, string.Join("; ", result.Issues.Select(i => i.Message)));
            Assert.Equal("shn-xml", result.SourceFormat);

            var file = result.CheatFile!;
            Assert.Equal("CUSA00009", file.TitleId);
            Assert.Equal("Assassins Creed - Black Flag", file.GameName);
            Assert.Single(file.Cheats);
            var cheat = file.Cheats[0];
            Assert.Equal("infinite_money", cheat.Id);
            Assert.Single(cheat.Codes);
            var code = cheat.Codes[0];
            Assert.Equal(CheatCodeType.ModuleWriteBytes, code.Type);
            Assert.Equal(new byte[] { 0x90, 0x90, 0x90 }, code.Bytes);
            Assert.Equal(new byte[] { 0x89, 0x47, 0x10 }, code.ExpectedBytes);
            Assert.Equal(code.ExpectedBytes, code.RestoreBytes);
            Assert.Equal(0x27693CA, code.Offset);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Converts_shn_multiple_cheats()
    {
        var path = Path.GetTempFileName() + ".shn";
        File.WriteAllText(path, @"<?xml version=""1.0""?>
<Trainer Cusa=""CUSA00010"" Game=""Test"" Process=""eboot.bin"">
  <Cheat Text=""Health"" Description="""">
    <Cheatline><Offset>100</Offset><Section>0</Section><ValueOn>90</ValueOn><ValueOff>FF</ValueOff></Cheatline>
  </Cheat>
  <Cheat Text=""Ammo"" Description="""">
    <Cheatline><Offset>200</Offset><Section>1</Section><ValueOn>00-00</ValueOn><ValueOff>AA-BB</ValueOff></Cheatline>
  </Cheat>
</Trainer>");
        try
        {
            var converter = new CheatFormatConverter();
            var result = converter.Convert(path);
            Assert.True(result.Success);
            Assert.Equal(2, result.CheatFile!.Cheats.Count);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Shn_xml_invalid_returns_structured_error()
    {
        var path = Path.GetTempFileName() + ".shn";
        File.WriteAllText(path, "not xml at all");
        try
        {
            var parser = new ShnXmlCheatParser();
            Assert.Throws<System.FormatException>(() => parser.Parse(path));
        }
        finally { File.Delete(path); }
    }

    // ============ MC4 Detection ============

    [Fact]
    public void Mc4_parser_extracts_metadata()
    {
        var path = Path.GetTempFileName() + ".mc4";
        File.WriteAllText(path, "UE9XRVJQQUNLIE1DNCB2MS4wCnRpdGxlSWQ9UFBTQTAxMzkwCmdhbWU9VGVzdAp2ZXJzaW9uPTAxLjAwCnByb2Nlc3M9ZWJvb3QuYmluCg==");
        try
        {
            var meta = Mc4CheatParser.ExtractMetadata(path);
            Assert.True(meta.Base64DecodeSuccess);
            Assert.Equal("PPSA01390", meta.TitleId);
            Assert.Equal("Test", meta.GameName);
            Assert.Equal("01.00", meta.Version);
            Assert.Contains("Header extracted", meta.Notes);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Mc4_parser_returns_unsupported_error()
    {
        var path = Path.GetTempFileName() + ".mc4";
        File.WriteAllText(path, "UE9XRVJQQUNLIE1DNCB2MS4w");
        try
        {
            var parser = new Mc4CheatParser();
            var result = parser.Parse(path);
            Assert.NotNull(result);
            Assert.Equal(0, result.Cheats.Count);
        }
        finally { File.Delete(path); }
    }

    // ============ Validation ============

    [Fact]
    public void Valid_converted_file_passes()
    {
        var file = new CheatFile
        {
            TitleId = "CUSA00001",
            GameName = "Test",
            Cheats = { new CheatDefinition { Id = "c1", Name = "Cheat 1", Codes = { new CheatCode { Type = CheatCodeType.WriteBytes, Address = 0x10000, Bytes = new byte[] { 0x90 } } } } }
        };
        var validator = new CheatSchemaValidator();
        var result = validator.Validate(file);
        Assert.True(result.Success);
    }

    [Fact]
    public void Missing_titleId_fails()
    {
        var file = new CheatFile { Cheats = { new CheatDefinition { Id = "c1", Name = "C1", Codes = { new CheatCode { Type = CheatCodeType.WriteBytes } } } } };
        var validator = new CheatSchemaValidator();
        var result = validator.Validate(file);
        Assert.False(result.Success);
        Assert.Contains(result.Issues, i => i.Path == "titleId" && i.Severity == "Error");
    }

    [Fact]
    public void Duplicate_cheat_ids_rejected()
    {
        var file = new CheatFile
        {
            TitleId = "CUSA00001",
            Cheats =
            {
                new CheatDefinition { Id = "dup", Name = "A", Codes = { new CheatCode { Type = CheatCodeType.WriteBytes, Address = 0x10000, Bytes = new byte[] { 0x90 } } } },
                new CheatDefinition { Id = "dup", Name = "B", Codes = { new CheatCode { Type = CheatCodeType.WriteBytes, Address = 0x10000, Bytes = new byte[] { 0x90 } } } },
            }
        };
        var validator = new CheatSchemaValidator();
        var result = validator.Validate(file);
        Assert.False(result.Success);
        Assert.Contains(result.Issues, i => i.Message.Contains("Duplicate"));
    }

    [Fact]
    public void Missing_cheat_id_fails()
    {
        var file = new CheatFile { TitleId = "CUSA00001", Cheats = { new CheatDefinition { Id = "", Name = "C1", Codes = { new CheatCode { Type = CheatCodeType.WriteBytes, Address = 0x10000, Bytes = new byte[] { 0x90 } } } } } };
        var validator = new CheatSchemaValidator();
        var result = validator.Validate(file);
        Assert.False(result.Success);
    }

    [Fact]
    public void No_cheats_fails()
    {
        var file = new CheatFile { TitleId = "CUSA00001" };
        var validator = new CheatSchemaValidator();
        var result = validator.Validate(file);
        Assert.False(result.Success);
    }

    [Fact]
    public void Invalid_bytes_format_warns()
    {
        var file = new CheatFile { TitleId = "CUSA00001", Cheats = { new CheatDefinition { Id = "c1", Name = "C1", Codes = { new CheatCode { Type = CheatCodeType.WriteBytes, Address = 0x10000 } } } } };
        var validator = new CheatSchemaValidator();
        var result = validator.Validate(file);
        Assert.False(result.Success);
    }

    [Fact]
    public void Serialize_deserialize_roundtrip()
    {
        var original = new CheatFile
        {
            TitleId = "CUSA00001",
            GameName = "Test Game",
            Version = "1.00",
            Cheats =
            {
                new CheatDefinition
                {
                    Id = "test1",
                    Name = "Test Cheat",
                    Description = "A test cheat",
                    Codes =
                    {
                        new CheatCode
                        {
                            Type = CheatCodeType.FreezeValue,
                            Address = 0x12345678,
                            ValueType = "int",
                            Value = "999",
                            FreezeIntervalMs = 250,
                        }
                    }
                }
            }
        };

        var json = CheatFormatConverter.SerializeToNativeJson(original);
        Assert.Contains("\"titleId\": \"CUSA00001\"", json);
        Assert.Contains("\"gameName\": \"Test Game\"", json);

        var parser = new JsonCheatParser();
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
        var parsed = parser.Parse(stream);
        Assert.Equal(original.TitleId, parsed.TitleId);
        Assert.Equal(original.Cheats[0].Id, parsed.Cheats[0].Id);
        Assert.Equal(original.Cheats[0].Codes[0].Address, parsed.Cheats[0].Codes[0].Address);
    }

    private static string CreateTempJson(string content)
    {
        var path = Path.GetTempFileName() + ".json";
        File.WriteAllText(path, content);
        return path;
    }
}
