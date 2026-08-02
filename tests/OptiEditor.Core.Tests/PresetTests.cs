using System.Text;
using OptiEditor.Core.Ini;
using OptiEditor.Core.Models;
using OptiEditor.Core.Presets;
using OptiEditor.Core.Schema;

namespace OptiEditor.Core.Tests;

public sealed class PresetTests
{
    [Fact]
    public void Built_in_catalog_is_empty() => Assert.Empty(new BuiltInPresetProvider().GetAll());

    [Fact]
    public void Preview_blocks_family_mismatch_and_skips_missing_keys()
    {
        var preset = Preset(OptiSchemaFamily.V09, [new("fsrfg.pacing", "true"), new("fsrfg.safety-margin", "0.1")]);
        var document = IniParser.Parse("[FSRFG]\nFramePacingTuning=auto\n", new UTF8Encoding(false), false);
        var service = new PresetPreviewService(new OptiSchemaResolver());

        Assert.NotNull(service.Create(preset, OptiSchemaFamily.V10, document).Error);

        var preview = service.Create(preset, OptiSchemaFamily.V09, document);
        Assert.Contains(preview.Items, x => x.Status == PresetApplicationStatus.WillChange);
        Assert.Contains(preview.Items, x => x.Status == PresetApplicationStatus.MissingInTargetIni);
    }

    [Fact]
    public async Task User_store_round_trips_without_builtin_entries()
    {
        var folder = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        try
        {
            var store = new UserPresetStore(folder);
            var preset = Preset(OptiSchemaFamily.V10, [new("framegen.enabled", "true")]);
            await store.SaveAsync([preset, preset with { Id = Guid.NewGuid(), Source = PresetSource.BuiltIn }]);

            var loaded = await store.LoadAsync();
            Assert.Single(loaded);
            Assert.Equal("Mine", loaded[0].Name);
        }
        finally
        {
            if (Directory.Exists(folder)) Directory.Delete(folder, true);
        }
    }

    [Fact]
    public async Task User_store_does_not_treat_unreadable_storage_as_an_empty_list()
    {
        var folder = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(Path.Combine(folder, "presets.json"));
            var store = new UserPresetStore(folder);
            await Assert.ThrowsAsync<PresetStoreException>(() => store.LoadAsync());
        }
        finally
        {
            if (Directory.Exists(folder)) Directory.Delete(folder, true);
        }
    }

    [Fact]
    public void Applying_selected_entries_changes_only_selected_document_values()
    {
        var document = IniParser.Parse("[FSRFG]\nFramePacingTuning=auto\nFPTSafetyMarginInMs=auto\nFPTVarianceFactor=auto\n", new UTF8Encoding(false), false);
        var preset = Preset(OptiSchemaFamily.V10, [new("fsrfg.pacing", "true"), new("fsrfg.safety-margin", "0.01"), new("fsrfg.variance", "0.3")]);
        var resolver = new OptiSchemaResolver();
        var preview = new PresetPreviewService(resolver).Create(preset, OptiSchemaFamily.V10, document);
        var selected = preview with { Items = preview.Items.Select(x => x with { IsSelected = x.Entry.SettingId == "fsrfg.variance" }).ToArray() };

        Assert.True(new PresetApplicationService(resolver).ApplySelected(selected, document));
        Assert.Equal("0.3", document.GetRawValue(new("FSRFG", "FPTVarianceFactor")));
        Assert.Equal("auto", document.GetRawValue(new("FSRFG", "FramePacingTuning")));
    }

    [Theory]
    [InlineData("1", "01")]
    [InlineData("1.0", "1")]
    [InlineData("0x2D", "45")]
    public void Preview_compares_numeric_and_shortcut_values_semantically(string current, string desired)
    {
        var schema = new Opti10SchemaProvider();
        var setting = desired == "45" ? schema.FindById("shortcuts.menu")! : schema.FindById("output.multiplier")!;
        var document = IniParser.Parse($"[{setting.IniKey.Section}]\n{setting.IniKey.Name}={current}\n", new UTF8Encoding(false), false);
        var preset = Preset(OptiSchemaFamily.V10, [new(setting.Id, desired)]);

        Assert.Equal(PresetApplicationStatus.NoChange, new PresetPreviewService(new OptiSchemaResolver()).Create(preset, OptiSchemaFamily.V10, document).Items.Single().Status);
    }

    private static PresetDefinition Preset(OptiSchemaFamily family, IReadOnlyList<PresetEntry> entries) =>
        new(Guid.NewGuid(), "Mine", null, family, PresetSource.User, entries, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);
}
