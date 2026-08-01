using OptiEditor.Core.Discovery;
using OptiEditor.Core.Models;
using OptiEditor.Core.Storage;

namespace OptiEditor.App.Services;

public interface IInstallationCatalog
{
    IReadOnlyList<OptiInstallation> Installations { get; }
    bool IsScanning { get; }
    event EventHandler? InstallationsChanged;
    Task<DiscoveryResult> ScanAllAsync(CancellationToken cancellationToken = default);
    Task<OptiInstallation?> RescanDirectoryAsync(string installDirectory, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<OptiInstallation>> RescanDirectoriesAsync(IEnumerable<string> installDirectories, CancellationToken cancellationToken = default);
}

public sealed class InstallationCatalog(ScanRootStore roots, InstallationDiscoveryScanner scanner) : IInstallationCatalog
{
    private readonly SemaphoreSlim _operationGate = new(1, 1);
    private IReadOnlyList<OptiInstallation> _installations = [];
    private bool _isScanning;

    public IReadOnlyList<OptiInstallation> Installations => _installations;
    public bool IsScanning => _isScanning;
    public event EventHandler? InstallationsChanged;

    public async Task<DiscoveryResult> ScanAllAsync(CancellationToken cancellationToken = default)
    {
        await _operationGate.WaitAsync(cancellationToken);
        try
        {
            SetScanning(true);
            var result = await scanner.ScanAsync(await roots.LoadAsync(cancellationToken), cancellationToken);
            _installations = result.Installations;
            InstallationsChanged?.Invoke(this, EventArgs.Empty);
            return result;
        }
        finally
        {
            SetScanning(false);
            _operationGate.Release();
        }
    }

    public async Task<OptiInstallation?> RescanDirectoryAsync(string installDirectory, CancellationToken cancellationToken = default)
    {
        var refreshed = await RescanDirectoriesAsync([installDirectory], cancellationToken);
        return refreshed.FirstOrDefault();
    }

    public async Task<IReadOnlyList<OptiInstallation>> RescanDirectoriesAsync(IEnumerable<string> installDirectories, CancellationToken cancellationToken = default)
    {
        var directories = installDirectories.Select(Path.GetFullPath).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        await _operationGate.WaitAsync(cancellationToken);
        try
        {
            SetScanning(true);
            var refreshed = new List<OptiInstallation>();
            foreach (var directory in directories)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var installation = await scanner.ScanDirectoryAsync(directory, cancellationToken);
                if (installation is not null) refreshed.Add(installation);
            }

            var replacement = new Dictionary<string, OptiInstallation>(refreshed.Select(x => new KeyValuePair<string, OptiInstallation>(x.InstallDirectory, x)), StringComparer.OrdinalIgnoreCase);
            _installations = _installations.Where(x => !directories.Contains(x.InstallDirectory, StringComparer.OrdinalIgnoreCase)).Concat(replacement.Values).OrderBy(x => x.GameDisplayName).ToArray();
            InstallationsChanged?.Invoke(this, EventArgs.Empty);
            return refreshed;
        }
        finally
        {
            SetScanning(false);
            _operationGate.Release();
        }
    }

    private void SetScanning(bool value)
    {
        if (_isScanning == value) return;
        _isScanning = value;
        InstallationsChanged?.Invoke(this, EventArgs.Empty);
    }
}
