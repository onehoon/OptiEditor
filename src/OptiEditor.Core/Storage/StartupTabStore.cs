using OptiEditor.Core.Utilities;

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

public sealed class StartupTabStore(string? appData = null, IDiagnosticLogger? logger = null) : IStartupTabStore
{
    private readonly string _path = Path.Combine(appData ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OptiEditor"), "startup-tab.json");

    public async Task<string> LoadAsync(CancellationToken cancellationToken = default)
    {
        var tab = await JsonFileStore.LoadAsync(_path, StartupTabs.Games, logger, cancellationToken);
        return StartupTabs.IsSupported(tab) ? tab : StartupTabs.Games;
    }

    public Task SaveAsync(string tab, CancellationToken cancellationToken = default)
    {
        if (!StartupTabs.IsSupported(tab)) throw new ArgumentOutOfRangeException(nameof(tab));
        return JsonFileStore.SaveAsync(_path, tab, null, cancellationToken);
    }
}
