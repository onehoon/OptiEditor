using System.Collections.Concurrent;
using System.Text.Json;
using OptiEditor.Core.Ini;
using OptiEditor.Core.Schema;
using OptiEditor.Core.Models;
using OptiEditor.Core.Utilities;

namespace OptiEditor.Core.Presets;
public interface IUserPresetStore { Task<IReadOnlyList<PresetDefinition>> LoadAsync(CancellationToken token = default); Task SaveAsync(IEnumerable<PresetDefinition> presets, CancellationToken token = default); }
public sealed class UserPresetStore(string? appData = null, IDiagnosticLogger? logger = null) : IUserPresetStore
{
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> Locks = new(StringComparer.OrdinalIgnoreCase);
    private readonly string _path = Path.Combine(appData ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OptiEditor"), "presets.json");
    public async Task<IReadOnlyList<PresetDefinition>> LoadAsync(CancellationToken token = default)
    {
        if (!File.Exists(_path)) return []; try { await using var stream = File.OpenRead(_path); return (await JsonSerializer.DeserializeAsync<List<PresetDefinition>>(stream, cancellationToken: token) ?? []).Where(x => x.Source == PresetSource.User).ToArray(); }
        catch (JsonException ex)
        {
            try { var invalid = Path.Combine(Path.GetDirectoryName(_path)!, $"presets.invalid-{DateTime.UtcNow:yyyyMMddHHmmss}.json"); File.Move(_path, invalid, true); logger?.Error("Invalid user presets were moved aside.", ex); }
            catch (Exception recoveryException) when (recoveryException is IOException or UnauthorizedAccessException) { logger?.Error("Invalid user presets could not be moved aside.", recoveryException); }
            return [];
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException) { logger?.Error("User presets could not be loaded.", ex); return []; }
    }
    public async Task SaveAsync(IEnumerable<PresetDefinition> presets, CancellationToken token = default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!); var gate = Locks.GetOrAdd(_path, _ => new(1, 1)); await gate.WaitAsync(token); var tmp = _path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try { await using (var stream = File.Create(tmp)) await JsonSerializer.SerializeAsync(stream, presets.Where(x => x.Source == PresetSource.User), cancellationToken: token); File.Move(tmp, _path, true); } finally { if (File.Exists(tmp)) File.Delete(tmp); gate.Release(); }
    }
}
public sealed class PresetPreviewService(OptiSchemaResolver resolver)
{
    public PresetApplicationPreview Create(PresetDefinition preset, OptiSchemaFamily family, IniDocument document)
    {
        if (preset.Family != family) return new(preset, [], "This preset targets a different OptiScaler version family."); var schema = resolver.Resolve(family);
        var items = preset.Entries.Select(entry => { var definition = schema.FindById(entry.SettingId); if (definition is null) return new PresetApplicationItem(entry, PresetApplicationStatus.UnsupportedSetting, null, false); if (!SettingValidator.Validate(definition, entry.RawValue).IsValid) return new PresetApplicationItem(entry, PresetApplicationStatus.InvalidPresetValue, null, false); var current = document.GetRawValue(definition.IniKey); if (current is null) return new PresetApplicationItem(entry, PresetApplicationStatus.MissingInTargetIni, null, false); var changed = !SettingValueComparer.AreEquivalent(definition, current, entry.RawValue); return new PresetApplicationItem(entry, changed ? PresetApplicationStatus.WillChange : PresetApplicationStatus.NoChange, current, changed); }).ToArray(); return new(preset, items, null);
    }
}
public sealed class PresetValidationService(OptiSchemaResolver resolver)
{
    public string? Validate(PresetDefinition preset, IEnumerable<PresetDefinition>? existing = null)
    {
        if (preset.Source != PresetSource.User) return "Built-in presets cannot be edited.";
        if (string.IsNullOrWhiteSpace(preset.Name) || preset.Name.Trim().Length > 80 || preset.Name.IndexOfAny(['\r', '\n']) >= 0) return "Enter a valid preset name.";
        if (preset.Entries.Count == 0 || preset.Entries.Select(x => x.SettingId).Distinct(StringComparer.OrdinalIgnoreCase).Count() != preset.Entries.Count) return "A preset must contain unique settings.";
        if (existing?.Any(x => x.Id != preset.Id && x.Family == preset.Family && string.Equals(x.Name, preset.Name.Trim(), StringComparison.OrdinalIgnoreCase)) == true) return "A preset with this name already exists for this version family.";
        var schema = resolver.Resolve(preset.Family); foreach (var entry in preset.Entries) { var setting = schema.FindById(entry.SettingId); if (setting is null || !SettingValidator.Validate(setting, entry.RawValue).IsValid) return $"Invalid preset entry: {entry.SettingId}."; } return null;
    }
}
public sealed class PresetApplicationService(OptiSchemaResolver resolver)
{
    public bool ApplySelected(PresetApplicationPreview preview, IniDocument document)
    {
        if (!preview.CanApply) return false; var schema = resolver.Resolve(preview.Preset.Family); var clone = document.Clone();
        foreach (var item in preview.Items.Where(x => x.IsSelected && x.Status == PresetApplicationStatus.WillChange)) { var definition = schema.FindById(item.Entry.SettingId)!; if (!clone.ApplyPatch(new(definition.IniKey, item.Entry.RawValue)).WasChanged) return false; }
        foreach (var item in preview.Items.Where(x => x.IsSelected && x.Status == PresetApplicationStatus.WillChange)) { var definition = schema.FindById(item.Entry.SettingId)!; document.ApplyPatch(new(definition.IniKey, item.Entry.RawValue)); } return true;
    }
}
public sealed class PresetCaptureService(OptiSchemaResolver resolver)
{
    public PresetDefinition Capture(string name, string? description, OptiSchemaFamily family, IEnumerable<(string SettingId, string RawValue, bool IsModified)> values)
    {
        var schema = resolver.Resolve(family); var entries = values.Where(x => x.IsModified).Where(x => schema.FindById(x.SettingId) is { } definition && SettingValidator.Validate(definition, x.RawValue).IsValid).Select(x => new PresetEntry(x.SettingId, x.RawValue)).ToArray(); var now = DateTimeOffset.UtcNow;
        return new(Guid.NewGuid(), name.Trim(), description, family, PresetSource.User, entries, now, now);
    }
}
