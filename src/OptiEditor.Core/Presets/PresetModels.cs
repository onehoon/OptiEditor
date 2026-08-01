using OptiEditor.Core.Models;

namespace OptiEditor.Core.Presets;
public enum PresetSource { BuiltIn, User }
public enum PresetApplicationStatus { WillChange, NoChange, MissingInTargetIni, UnsupportedSetting, InvalidPresetValue, FamilyMismatch }
public sealed record PresetEntry(string SettingId, string RawValue);
public sealed record PresetDefinition(Guid Id, string Name, string? Description, OptiSchemaFamily Family, PresetSource Source, IReadOnlyList<PresetEntry> Entries, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);
public sealed record PresetApplicationItem(PresetEntry Entry, PresetApplicationStatus Status, string? CurrentValue, bool IsSelected);
public sealed record PresetApplicationPreview(PresetDefinition Preset, IReadOnlyList<PresetApplicationItem> Items, string? Error) { public bool CanApply => Error is null && Items.Any(x => x.IsSelected && x.Status == PresetApplicationStatus.WillChange); }
