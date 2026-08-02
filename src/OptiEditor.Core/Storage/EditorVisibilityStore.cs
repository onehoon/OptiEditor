using System.Text.Json.Serialization;
using OptiEditor.Core.Models;
using OptiEditor.Core.Schema;
using OptiEditor.Core.Utilities;

namespace OptiEditor.Core.Storage;

public sealed record EditorVisibilityPreference(
    OptiSchemaFamily Family,
    [property: JsonPropertyName("SettingId")] string Section,
    EditorVisibility Visibility);

public interface IEditorVisibilityStore
{
    Task<IReadOnlyList<EditorVisibilityPreference>> LoadAsync(CancellationToken cancellationToken = default);
    Task SaveAsync(IEnumerable<EditorVisibilityPreference> preferences, CancellationToken cancellationToken = default);
}

public sealed class EditorVisibilityStore(string? appData = null, IDiagnosticLogger? logger = null) : IEditorVisibilityStore
{
    private readonly string _path = Path.Combine(appData ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OptiEditor"), "editor-visibility.json");

    public async Task<IReadOnlyList<EditorVisibilityPreference>> LoadAsync(CancellationToken cancellationToken = default) => await JsonFileStore.LoadAsync<List<EditorVisibilityPreference>>(_path, [], logger, cancellationToken);

    public Task SaveAsync(IEnumerable<EditorVisibilityPreference> preferences, CancellationToken cancellationToken = default)
    {
        var distinct = preferences.GroupBy(x => (x.Family, x.Section), StringTupleComparer.Instance).Select(x => x.Last()).ToArray();
        return JsonFileStore.SaveAsync(_path, distinct, null, logger, cancellationToken);
    }

    private sealed class StringTupleComparer : IEqualityComparer<(OptiSchemaFamily Family, string SettingId)>
    {
        public static StringTupleComparer Instance { get; } = new();
        public bool Equals((OptiSchemaFamily Family, string SettingId) x, (OptiSchemaFamily Family, string SettingId) y) => x.Family == y.Family && string.Equals(x.SettingId, y.SettingId, StringComparison.OrdinalIgnoreCase);
        public int GetHashCode((OptiSchemaFamily Family, string SettingId) value) => HashCode.Combine(value.Family, StringComparer.OrdinalIgnoreCase.GetHashCode(value.SettingId));
    }
}

public static class EditorVisibilityPolicy
{
    public static EditorVisibility Resolve(SettingDefinition definition, OptiSchemaFamily family, IEnumerable<EditorVisibilityPreference> preferences) => preferences.LastOrDefault(x => x.Family == family && string.Equals(x.Section, definition.IniKey.Section, StringComparison.OrdinalIgnoreCase))?.Visibility ?? definition.DefaultEditorVisibility;
}
