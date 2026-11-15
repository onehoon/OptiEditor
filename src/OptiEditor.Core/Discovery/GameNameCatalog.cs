using System.Text.Json;

namespace OptiEditor.Core.Discovery;

public interface IGameNameCatalog
{
    string? Find(string executableName, IEnumerable<string>? availableFileNames = null);
    void Replace(IEnumerable<GameNameCatalogEntry> entries);
}

public sealed record GameNameCatalogEntry(string Name, IReadOnlyList<string> RequiredFiles);

public sealed class GameNameCatalog : IGameNameCatalog
{
    private readonly object _gate = new();
    private readonly IReadOnlyList<GameNameCatalogEntry> _embeddedEntries;
    private IReadOnlyList<GameNameCatalogEntry> _entries = [];

    public GameNameCatalog()
    {
        _embeddedEntries = BuildEntries(GameMasterNameMap.Entries.Select(pair => new GameNameCatalogEntry(pair.Value, [pair.Key])));
        _entries = _embeddedEntries;
    }

    public string? Find(string executableName, IEnumerable<string>? availableFileNames = null)
    {
        var normalizedExecutable = Normalize(executableName);
        if (normalizedExecutable.Length == 0 || !normalizedExecutable.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) return null;
        var available = availableFileNames is null
            ? new HashSet<string>(StringComparer.OrdinalIgnoreCase) { normalizedExecutable }
            : availableFileNames.Select(Normalize).Where(x => x.Length > 0).ToHashSet(StringComparer.OrdinalIgnoreCase);

        lock (_gate)
        {
            return _entries
                .Where(entry => entry.RequiredFiles.Contains(normalizedExecutable, StringComparer.OrdinalIgnoreCase))
                .Where(entry => entry.RequiredFiles.All(available.Contains))
                .OrderByDescending(entry => entry.RequiredFiles.Count)
                .ThenBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase)
                .Select(entry => entry.Name)
                .FirstOrDefault();
        }
    }

    public void Replace(IEnumerable<GameNameCatalogEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        var replacement = BuildEntries(entries.Concat(_embeddedEntries));
        if (replacement.Count == 0) return;
        lock (_gate) _entries = replacement;
    }

    public static IReadOnlyList<GameNameCatalogEntry> ParseRuntimeJson(string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        if (root.GetProperty("schema_version").GetInt32() != 1) throw new JsonException("Unsupported runtime schema version.");
        var games = root.GetProperty("data").GetProperty("game_master");
        var entries = new List<GameNameCatalogEntry>();
        foreach (var game in games.EnumerateArray())
        {
            var name = game.TryGetProperty("game_name_en", out var nameElement) ? nameElement.GetString()?.Trim() : null;
            var match = game.TryGetProperty("match_exe", out var matchElement) ? matchElement.GetString() : null;
            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(match)) continue;
            var requiredFiles = match.Split(['|', ';', ',', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(Normalize).Where(x => x.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            if (requiredFiles.Any(x => x.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))) entries.Add(new(name, requiredFiles));
        }
        return entries;
    }

    private static IReadOnlyList<GameNameCatalogEntry> BuildEntries(IEnumerable<GameNameCatalogEntry> entries) => entries
        .Select(entry => new GameNameCatalogEntry(entry.Name.Trim(), entry.RequiredFiles.Select(Normalize).Where(x => x.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToArray()))
        .Where(entry => entry.Name.Length > 0 && entry.RequiredFiles.Any(x => x.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)))
        .ToArray();

    private static string Normalize(string value) => Path.GetFileName((value ?? string.Empty).Trim().Trim('"', '\''));
}
