using System.Text.Json;
using OptiEditor.Core.Models;

namespace OptiEditor.Core.Storage;

public sealed class ScanRootStore
{
    private readonly string _filePath;
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };
    public ScanRootStore(string? appDataDirectory = null)
    {
        var root = appDataDirectory ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OptiEditor");
        Directory.CreateDirectory(root);
        _filePath = Path.Combine(root, "scan-roots.json");
    }
    public async Task<IReadOnlyList<ScanRoot>> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_filePath)) return [];
        await using var stream = File.OpenRead(_filePath);
        return await JsonSerializer.DeserializeAsync<List<ScanRoot>>(stream, Options, cancellationToken) ?? [];
    }
    public async Task SaveAsync(IEnumerable<ScanRoot> roots, CancellationToken cancellationToken = default)
    {
        await using var stream = File.Create(_filePath);
        await JsonSerializer.SerializeAsync(stream, roots, Options, cancellationToken);
    }
}
