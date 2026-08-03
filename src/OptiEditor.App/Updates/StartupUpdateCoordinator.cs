using Velopack;
using Velopack.Sources;
using OptiEditor.App.Services;

namespace OptiEditor.App.Updates;
public enum StartupUpdateResult { NotInstalled, NoUpdate, UpdateReadyForNextLaunch, Failed }
public interface IStartupUpdateCoordinator { Task<StartupUpdateResult> RunAsync(CancellationToken cancellationToken = default); }
public sealed class StartupUpdateCoordinator : IStartupUpdateCoordinator
{
    private const string RepositoryUrl = "https://github.com/onehoon/OptiEditor";
    private static int _started;

    public static bool TryApplyPendingUpdateAtLaunch()
    {
        try
        {
            var manager = CreateManager();
            if (!manager.IsInstalled) return false;

            var pending = manager.UpdatePendingRestart;
            if (pending is null) return false;

            StartupDiagnostics.Info("Applying pending OptiEditor update before WinUI startup.");
            manager.ApplyUpdatesAndRestart(pending);
            return true;
        }
        catch (Exception ex)
        {
            StartupDiagnostics.Error("Pending OptiEditor update could not be applied; launching the current version.", ex);
            return false;
        }
    }

    public async Task<StartupUpdateResult> RunAsync(CancellationToken cancellationToken = default)
    {
        if (Interlocked.Exchange(ref _started, 1) != 0) return StartupUpdateResult.Failed;
        try
        {
            var manager = CreateManager();
            if (!manager.IsInstalled) { StartupDiagnostics.Info("Velopack update skipped: development or portable build."); return StartupUpdateResult.NotInstalled; }
            StartupDiagnostics.Info("Startup update check started.");
            if (manager.UpdatePendingRestart is not null) { StartupDiagnostics.Info("OptiEditor update remains downloaded and will be applied on the next eligible launch."); return StartupUpdateResult.UpdateReadyForNextLaunch; }
            using var checkTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            checkTimeout.CancelAfter(TimeSpan.FromSeconds(15));
            var update = await manager.CheckForUpdatesAsync().WaitAsync(checkTimeout.Token); if (update is null) { StartupDiagnostics.Info("No OptiEditor update found."); return StartupUpdateResult.NoUpdate; }
            StartupDiagnostics.Info($"OptiEditor update found: {update.TargetFullRelease.Version}.");
            using var downloadTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            downloadTimeout.CancelAfter(TimeSpan.FromMinutes(15));
            await manager.DownloadUpdatesAsync(update, null, downloadTimeout.Token); StartupDiagnostics.Info("OptiEditor update downloaded and will be applied on the next eligible launch."); return StartupUpdateResult.UpdateReadyForNextLaunch;
        }
        catch (Exception ex) { StartupDiagnostics.Error("Startup update failed; launching current OptiEditor version.", ex); return StartupUpdateResult.Failed; }
    }

    private static UpdateManager CreateManager() => new(new GithubSource(RepositoryUrl, null, false));
}
