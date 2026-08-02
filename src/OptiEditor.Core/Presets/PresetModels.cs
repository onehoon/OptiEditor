using OptiEditor.Core.Models;

namespace OptiEditor.Core.Presets;
public enum PresetSource { BuiltIn, User }
public sealed record PresetEntry(string SettingId, string RawValue);
public sealed record PresetDefinition(Guid Id, string Name, string? Description, OptiSchemaFamily Family, PresetSource Source, IReadOnlyList<PresetEntry> Entries, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);
