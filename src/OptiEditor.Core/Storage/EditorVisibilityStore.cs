using System.Collections.Concurrent;
using System.Text.Json;
using OptiEditor.Core.Models;
using OptiEditor.Core.Schema;

namespace OptiEditor.Core.Storage;

public sealed record EditorVisibilityPreference(OptiSchemaFamily Family, string SettingId, EditorVisibility Visibility);

public interface IEditorVisibilityStore
{
    Task<IReadOnlyList<EditorVisibilityPreference>> LoadAsync(CancellationToken cancellationToken = default);
    Task SaveAsync(IEnumerable<EditorVisibilityPreference> preferences, CancellationToken cancellationToken = default);
}

public sealed class EditorVisibilityStore(string? appData = null) : IEditorVisibilityStore
{
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> Locks = new(StringComparer.OrdinalIgnoreCase);
    private readonly string _path = Path.Combine(appData ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OptiEditor"), "editor-visibility.json");

    public async Task<IReadOnlyList<EditorVisibilityPreference>> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_path)) return [];
        await using var stream = File.Open(_path, FileMode.Open, FileAccess.Read, FileShare.Read);
        return await JsonSerializer.DeserializeAsync<List<EditorVisibilityPreference>>(stream, cancellationToken: cancellationToken) ?? [];
    }

    public async Task SaveAsync(IEnumerable<EditorVisibilityPreference> preferences, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        var gate = Locks.GetOrAdd(_path, _ => new SemaphoreSlim(1, 1)); await gate.WaitAsync(cancellationToken);
        var temporary = _path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            var distinct = preferences.GroupBy(x => (x.Family, x.SettingId), StringTupleComparer.Instance).Select(x => x.Last()).ToArray();
            await using (var stream = File.Create(temporary)) await JsonSerializer.SerializeAsync(stream, distinct, cancellationToken: cancellationToken);
            File.Move(temporary, _path, true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); gate.Release(); }
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
    public static EditorVisibility Resolve(SettingDefinition definition, OptiSchemaFamily family, IEnumerable<EditorVisibilityPreference> preferences) => preferences.LastOrDefault(x => x.Family == family && string.Equals(x.SettingId, definition.Id, StringComparison.OrdinalIgnoreCase))?.Visibility ?? definition.DefaultEditorVisibility;
}
