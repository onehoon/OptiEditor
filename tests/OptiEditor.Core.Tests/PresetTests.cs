using OptiEditor.Core.Models;
using OptiEditor.Core.Presets;

namespace OptiEditor.Core.Tests;

public sealed class PresetTests
{
    [Fact]
    public void Built_in_catalog_is_empty() => Assert.Empty(new BuiltInPresetProvider().GetAll());

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

    private static PresetDefinition Preset(OptiSchemaFamily family, IReadOnlyList<PresetEntry> entries) =>
        new(Guid.NewGuid(), "Mine", null, family, PresetSource.User, entries, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);
}
