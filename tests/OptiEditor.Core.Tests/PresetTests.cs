using System.Text;
using OptiEditor.Core.Ini;
using OptiEditor.Core.Models;
using OptiEditor.Core.Presets;
using OptiEditor.Core.Schema;

namespace OptiEditor.Core.Tests;
public sealed class PresetTests
{
 [Fact] public void Built_ins_are_valid_for_each_target_schema() { var resolver = new OptiSchemaResolver(); var presets = new BuiltInPresetProvider().GetAll(); Assert.Equal(6, presets.Count); Assert.Equal(6, presets.Select(x => x.Id).Distinct().Count()); foreach (var preset in presets) foreach (var entry in preset.Entries) { var setting = resolver.Resolve(preset.Family).FindById(entry.SettingId); Assert.NotNull(setting); Assert.True(SettingValidator.Validate(setting!, entry.RawValue).IsValid); } }
 [Fact] public void Preview_blocks_family_mismatch_and_skips_missing_keys() { var preset = new BuiltInPresetProvider().GetAll().First(x => x.Family == OptiSchemaFamily.V09); var document = IniParser.Parse("[FSRFG]\nFramePacingTuning=auto\n", new UTF8Encoding(false), false); var service = new PresetPreviewService(new OptiSchemaResolver()); Assert.NotNull(service.Create(preset, OptiSchemaFamily.V10, document).Error); var preview = service.Create(preset, OptiSchemaFamily.V09, document); Assert.Contains(preview.Items, x => x.Status == PresetApplicationStatus.WillChange); Assert.Contains(preview.Items, x => x.Status == PresetApplicationStatus.MissingInTargetIni); }
 [Fact] public async Task User_store_round_trips_without_builtin_entries() { var folder = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")); try { var store = new UserPresetStore(folder); var p = new PresetDefinition(Guid.NewGuid(), "Mine", null, OptiSchemaFamily.V10, PresetSource.User, [new("framegen.enabled", "true")], DateTimeOffset.UtcNow, DateTimeOffset.UtcNow); await store.SaveAsync([p, new BuiltInPresetProvider().GetAll()[0]]); var loaded = await store.LoadAsync(); Assert.Single(loaded); Assert.Equal("Mine", loaded[0].Name); } finally { if (Directory.Exists(folder)) Directory.Delete(folder, true); } }
}
