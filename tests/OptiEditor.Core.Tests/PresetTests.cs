using OptiEditor.Core.Models;
using OptiEditor.Core.Presets;
using OptiEditor.Core.Schema;

namespace OptiEditor.Core.Tests;

public sealed class PresetTests
{
    [Fact]
    public void Built_in_catalog_contains_the_default_presets()
    {
        var presets = new BuiltInPresetProvider().GetAll();
        Assert.Equal(10, presets.Count);
        Assert.DoesNotContain(presets, x => x.Name == "FSR4.1.1 for AMD RDNA2");
        Assert.DoesNotContain(presets, x => x.Name == "FSR4.1.1 for Opti 0.9.x");
        Assert.Contains(presets, x => x.Name == "DLSSG to XeFG" && x.Family == OptiSchemaFamily.V09);
        Assert.Contains(presets, x => x.Name == "DLSSG to XeFG" && x.Family == OptiSchemaFamily.V10);
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
        finally { if (Directory.Exists(folder)) Directory.Delete(folder, true); }
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
        finally { if (Directory.Exists(folder)) Directory.Delete(folder, true); }
    }

    [Fact]
    public void Editing_a_preset_preserves_unchanged_legacy_entries_without_migration()
    {
        var oldPreset = Preset(OptiSchemaFamily.V10,
        [
            new("Legacy.RemovedSetting", "provider-value"),
            new("Menu.Scale", "2.5")
        ]);
        var validator = new PresetValidationService(new OptiSchemaResolver());

        Assert.Null(validator.Validate(oldPreset, [oldPreset]));
        Assert.NotNull(validator.Validate(oldPreset with
        {
            Entries = [new("Legacy.RemovedSetting", "changed")]
        }, [oldPreset]));
        Assert.NotNull(validator.Validate(Preset(OptiSchemaFamily.V10, [new("Legacy.RemovedSetting", "new")])));
    }

    private static PresetDefinition Preset(OptiSchemaFamily family, IReadOnlyList<PresetEntry> entries) =>
        new(Guid.NewGuid(), "Mine", null, family, PresetSource.User, entries, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);
}
