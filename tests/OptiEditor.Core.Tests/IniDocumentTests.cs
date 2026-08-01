using System.Text;
using OptiEditor.Core.Ini;

namespace OptiEditor.Core.Tests;

public sealed class IniDocumentTests
{
    [Theory]
    [InlineData("OptiScaler-0.9.ini")]
    [InlineData("OptiScaler-0.10.ini")]
    public async Task Fixtures_round_trip_byte_for_byte_without_modification(string fixture)
    {
        var path = FixturePath(fixture); var loaded = await new IniFileService().LoadAsync(path);
        Assert.Equal(await File.ReadAllBytesAsync(path), loaded.Document.RenderBytes());
    }

    [Fact]
    public async Task V09_fixture_exposes_expected_entries()
    {
        var doc = (await new IniFileService().LoadAsync(FixturePath("OptiScaler-0.9.ini"))).Document;
        Assert.Equal("auto", doc.GetRawValue(new("Upscalers", "Dx12Upscaler"))); Assert.Equal("auto", doc.GetRawValue(new("FrameGen", "FGInput"))); Assert.Equal("auto", doc.GetRawValue(new("FrameGen", "FGOutput")));
        Assert.True(doc.Contains(new("FSR", "Fsr4Update"))); Assert.True(doc.Contains(new("FSR", "Fsr4ForceEnableInt8"))); Assert.True(doc.Contains(new("FSR", "Fsr4Preset"))); Assert.True(doc.Contains(new("Nukems", "MakeDepthCopy")));
        Assert.DoesNotContain(doc.GetSectionNames(), x => string.Equals(x, "DLSSG", StringComparison.OrdinalIgnoreCase)); Assert.Equal("auto", doc.GetRawValue(new("Libraries", "OptiDllPath"))); Assert.Equal("auto", doc.GetRawValue(new("Menu", "ShortcutKey")));
    }

    [Fact]
    public async Task V10_fixture_preserves_dispatch_flags_spacing_and_expected_entries()
    {
        var doc = (await new IniFileService().LoadAsync(FixturePath("OptiScaler-0.10.ini"))).Document;
        Assert.Equal("auto", doc.GetRawValue(new("Upscalers", "Dx12Upscaler"))); Assert.Equal("auto", doc.GetRawValue(new("FrameGen", "FGInput"))); Assert.Equal("auto", doc.GetRawValue(new("FrameGen", "FGOutput")));
        Assert.True(doc.Contains(new("DLSSG", "InterpolationCount"))); Assert.True(doc.Contains(new("FSR", "Fsr4ForceModel"))); Assert.True(doc.Contains(new("FSR", "Fsr4Preset"))); Assert.True(doc.Contains(new("NvngxFG", "MakeDepthCopy"))); Assert.True(doc.Contains(new("Sharpness", "Shader"))); Assert.True(doc.Contains(new("Magnifier", "Enabled")));
        Assert.DoesNotContain(doc.GetSectionNames(), x => string.Equals(x, "Nukems", StringComparison.OrdinalIgnoreCase));
        doc.ApplyPatch(new(new("DLSSG", "DispatchFlags"), "0x14100000")); Assert.Contains("DispatchFlags = 0x14100000", doc.RenderText());
    }

    [Fact]
    public void Parser_retains_duplicates_sections_unknown_lines_and_insertions()
    {
        var doc = IniParser.Parse("[One]\r\nEnabled=true\r\n[Two]\r\nEnabled=false\r\n[Test]\r\nValue=1\r\nValue=2\r\nThis is unknown\r\n", new UTF8Encoding(false), false);
        Assert.True(doc.TryGetBoolean(new("One", "Enabled"), out var one) && one); Assert.True(doc.TryGetBoolean(new("Two", "Enabled"), out var two) && !two); Assert.Equal("2", doc.GetRawValue(new("Test", "Value"))); Assert.Equal(2, doc.GetAllEntries(new("Test", "Value")).Count);
        var result = doc.ApplyPatch(new(new("Test", "NewKey"), "auto")); Assert.True(result.WasInserted); Assert.False(doc.ApplyPatch(new(new("Absent", "X"), "1")).WasChanged); Assert.Contains("This is unknown", doc.RenderText());
    }

    [Fact]
    public void Revert_restores_original_bytes_and_auto_is_a_distinct_value()
    {
        const string source = "[Test]\n  Key   =   auto   \n"; var doc = IniParser.Parse(source, new UTF8Encoding(false), false);
        Assert.Equal(IniValueMode.Auto, doc.GetValue(new("Test", "Key")).Mode); doc.ApplyPatch(new(new("Test", "Key"), "42")); Assert.True(doc.IsDirty); Assert.Contains("  Key   =   42   ", doc.RenderText());
        Assert.True(doc.Revert(new("Test", "Key"))); Assert.False(doc.IsDirty); Assert.Equal(source, doc.RenderText());
    }

    [Fact]
    public async Task Save_creates_backup_and_refuses_external_content_changes()
    {
        var temp = CreateTempCopy("OptiScaler-0.10.ini");
        try
        {
            var service = new IniFileService(); var loaded = await service.LoadAsync(temp); loaded.Document.ApplyPatch(new(new("DLSSG", "DispatchFlags"), "0x14"));
            var save = await service.SaveAsync(loaded.Document, loaded.Snapshot); Assert.True(save.Success); Assert.True(File.Exists(temp + ".optieditor.bak"));
            var reloaded = await service.LoadAsync(temp); Assert.Equal("0x14", reloaded.Document.GetRawValue(new("DLSSG", "DispatchFlags")));
            reloaded.Document.ApplyPatch(new(new("DLSSG", "DispatchFlags"), "0x15")); await File.AppendAllTextAsync(temp, "; external change\n");
            var rejected = await service.SaveAsync(reloaded.Document, reloaded.Snapshot); Assert.False(rejected.Success); Assert.Contains("externally", rejected.Error!, StringComparison.OrdinalIgnoreCase); Assert.Contains("external change", await File.ReadAllTextAsync(temp));
        }
        finally { var directory = Path.GetDirectoryName(temp)!; Directory.Delete(directory, true); }
    }

    private static string FixturePath(string name) => Path.Combine(AppContext.BaseDirectory, "Fixtures", name);
    private static string CreateTempCopy(string name) { var directory = Path.Combine(Path.GetTempPath(), "OptiEditorTests", Guid.NewGuid().ToString("N")); Directory.CreateDirectory(directory); var target = Path.Combine(directory, "OptiScaler.ini"); File.Copy(FixturePath(name), target); return target; }
}
