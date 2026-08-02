using System.Text.Json;
using OptiEditor.Core.Models;
using OptiEditor.Core.Utilities;

namespace OptiEditor.Core.Storage;

public sealed class ScanRootStore
{
    private readonly string _filePath;
    private readonly IDiagnosticLogger? _logger;
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };
    public ScanRootStore(string? appDataDirectory = null, IDiagnosticLogger? logger = null)
    {
        var root = appDataDirectory ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OptiEditor");
        Directory.CreateDirectory(root);
        _filePath = Path.Combine(root, "scan-roots.json");
        _logger = logger;
    }
    public async Task<IReadOnlyList<ScanRoot>> LoadAsync(CancellationToken cancellationToken = default) => await JsonFileStore.LoadAsync<List<ScanRoot>>(_filePath, [], _logger, cancellationToken);
    public Task SaveAsync(IEnumerable<ScanRoot> roots, CancellationToken cancellationToken = default) => JsonFileStore.SaveAsync(_filePath, roots.ToArray(), Options, cancellationToken);
}
