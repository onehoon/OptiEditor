using System.Text;
using OptiEditor.Core.Ini;

namespace OptiEditor.Core.Tests;

public sealed class IniDocumentTests
{
    [Theory]
    [InlineData("[One]\r\nEnabled=true\r\n")]
    [InlineData("[One]\nEnabled=true\n")]
    public void Parser_round_trips_byte_for_byte_without_modification(string source)
    {
        var document = IniParser.Parse(source, new UTF8Encoding(false), false);
        Assert.Equal(new UTF8Encoding(false).GetBytes(source), document.RenderBytes());
    }

    [Fact]
    public void Parser_retains_duplicates_sections_unknown_lines_and_insertions()
    {
        var doc = IniParser.Parse("[One]\r\nEnabled=true\r\n[Two]\r\nEnabled=false\r\n[Test]\r\nValue=1\r\nValue=2\r\nThis is unknown\r\n", new UTF8Encoding(false), false);
        Assert.True(doc.TryGetBoolean(new("One", "Enabled"), out var one) && one); Assert.True(doc.TryGetBoolean(new("Two", "Enabled"), out var two) && !two); Assert.Equal("2", doc.GetRawValue(new("Test", "Value"))); Assert.Equal(2, doc.GetAllEntries(new("Test", "Value")).Count);
        var result = doc.ApplyPatch(new(new("Test", "NewKey"), "auto")); Assert.True(result.WasInserted); Assert.False(doc.ApplyPatch(new(new("Absent", "X"), "1")).WasChanged); Assert.Contains("This is unknown", doc.RenderText());
    }

    [Fact]
    public void Insert_after_a_file_with_no_trailing_newline_keeps_keys_on_separate_lines()
    {
        // The last physical line of a file with no trailing newline parses
        // with LineEnding = "". Inserting a new key right after it must not
        // concatenate the two keys onto the same rendered line.
        const string source = "[Section]\r\nExisting=1";
        var doc = IniParser.Parse(source, new UTF8Encoding(false), false);
        var result = doc.ApplyPatch(new(new("Section", "NewKey"), "2"));
        Assert.True(result.WasInserted);
        var rendered = doc.RenderText();
        Assert.Equal("[Section]\r\nExisting=1\r\nNewKey=2\r\n", rendered);
    }

    [Fact]
    public void Insert_into_an_empty_section_with_no_trailing_newline_keeps_header_and_key_separate()
    {
        const string source = "[Section]";
        var doc = IniParser.Parse(source, new UTF8Encoding(false), false);
        var result = doc.ApplyPatch(new(new("Section", "NewKey"), "2"));
        Assert.True(result.WasInserted);
        Assert.Equal("[Section]\r\nNewKey=2\r\n", doc.RenderText());
    }

    [Fact]
    public void Reverting_an_insert_after_a_file_with_no_trailing_newline_restores_exact_original_bytes()
    {
        // ApplyPatch backfills the preceding line's LineEnding from "" to
        // DominantLineEnding so the inserted key doesn't concatenate onto
        // it (see the test above). Reverting that insert must undo the
        // backfill too, or IsDirty/ModifiedKeys report a clean document
        // while RenderText still differs from the original bytes.
        const string source = "[Section]\r\nExisting=1";
        var doc = IniParser.Parse(source, new UTF8Encoding(false), false);
        doc.ApplyPatch(new(new("Section", "NewKey"), "2"));
        Assert.True(doc.Revert(new("Section", "NewKey")));
        Assert.False(doc.IsDirty);
        Assert.Empty(doc.ModifiedKeys);
        Assert.Equal(source, doc.RenderText());
    }

    [Fact]
    public void Reverting_one_of_two_trailing_inserts_keeps_the_remaining_insert_separated()
    {
        // If a second inserted key still follows the reverted one, the
        // backfilled ending on the line before it must NOT be restored to
        // "" -- that line is not the file's last line anymore, the
        // remaining inserted key is, and it still needs a real separator.
        const string source = "[Section]\r\nExisting=1";
        var doc = IniParser.Parse(source, new UTF8Encoding(false), false);
        doc.ApplyPatch(new(new("Section", "First"), "1"));
        doc.ApplyPatch(new(new("Section", "Second"), "2"));
        Assert.True(doc.Revert(new("Section", "First")));
        Assert.Equal("[Section]\r\nExisting=1\r\nSecond=2\r\n", doc.RenderText());
    }

    [Fact]
    public void RevertAll_after_multiple_trailing_inserts_restores_exact_original_bytes()
    {
        const string source = "[Section]\r\nExisting=1";
        var doc = IniParser.Parse(source, new UTF8Encoding(false), false);
        doc.ApplyPatch(new(new("Section", "First"), "1"));
        doc.ApplyPatch(new(new("Section", "Second"), "2"));
        doc.RevertAll();
        Assert.False(doc.IsDirty);
        Assert.Equal(source, doc.RenderText());
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
        var temp = CreateTempIni();
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

    [Fact]
    public async Task Save_refuses_change_made_while_creating_backup()
    {
        var temp = CreateTempIni();
        try
        {
            var loaded = await new IniFileService().LoadAsync(temp);
            loaded.Document.ApplyPatch(new(new("DLSSG", "DispatchFlags"), "0x14"));
            var service = new IniFileService(new MutatingBackupService("; external change during backup\n"));

            var result = await service.SaveAsync(loaded.Document, loaded.Snapshot);

            Assert.False(result.Success);
            Assert.Contains("externally", result.Error!, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("external change during backup", await File.ReadAllTextAsync(temp));
            Assert.NotEqual("0x14", (await new IniFileService().LoadAsync(temp)).Document.GetRawValue(new("DLSSG", "DispatchFlags")));
        }
        finally { var directory = Path.GetDirectoryName(temp)!; Directory.Delete(directory, true); }
    }

    private static string CreateTempIni()
    {
        var directory = Path.Combine(Path.GetTempPath(), "OptiEditorTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var target = Path.Combine(directory, "OptiScaler.ini");
        File.WriteAllText(target, "[DLSSG]\nDispatchFlags = auto\n", new UTF8Encoding(false));
        return target;
    }
    private sealed class MutatingBackupService(string text) : IIniBackupService
    {
        public async Task<string> CreateBackupAsync(string sourcePath, CancellationToken cancellationToken)
        {
            var backup = sourcePath + ".optieditor.bak";
            File.Copy(sourcePath, backup, true);
            await File.AppendAllTextAsync(sourcePath, text, cancellationToken);
            return backup;
        }
    }
}
