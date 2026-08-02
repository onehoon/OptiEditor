using System.Collections.Concurrent;
using System.Text.Json;

namespace OptiEditor.Core.Storage;

public static class StartupTabs
{
    public const string Games = "Games";
    public const string OptiScalerUpdate = "OptiScalerUpdate";
    public static bool IsSupported(string? tab) => tab is Games or OptiScalerUpdate;
}

public interface IStartupTabStore
{
    Task<string> LoadAsync(CancellationToken cancellationToken = default);
    Task SaveAsync(string tab, CancellationToken cancellationToken = default);
}

public sealed class StartupTabStore(string? appData = null) : IStartupTabStore
{
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> Locks = new(StringComparer.OrdinalIgnoreCase);
    private readonly string _path = Path.Combine(appData ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OptiEditor"), "startup-tab.json");

    public async Task<string> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_path)) return StartupTabs.Games;
        await using var stream = File.Open(_path, FileMode.Open, FileAccess.Read, FileShare.Read);
        var tab = await JsonSerializer.DeserializeAsync<string>(stream, cancellationToken: cancellationToken);
        return StartupTabs.IsSupported(tab) ? tab! : StartupTabs.Games;
    }

    public async Task SaveAsync(string tab, CancellationToken cancellationToken = default)
    {
        if (!StartupTabs.IsSupported(tab)) throw new ArgumentOutOfRangeException(nameof(tab));
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        var gate = Locks.GetOrAdd(_path, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken);
        var temporary = _path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            await using (var stream = File.Create(temporary)) await JsonSerializer.SerializeAsync(stream, tab, cancellationToken: cancellationToken);
            File.Move(temporary, _path, true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); gate.Release(); }
    }
}
