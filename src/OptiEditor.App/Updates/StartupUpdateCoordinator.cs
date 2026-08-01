using Velopack;
using Velopack.Sources;
using OptiEditor.App.Services;

namespace OptiEditor.App.Updates;
public enum StartupUpdateResult { NotInstalled, NoUpdate, UpdateRestartStarted, Failed }
public interface IStartupUpdateCoordinator { Task<StartupUpdateResult> RunAsync(CancellationToken cancellationToken = default); }
public sealed class StartupUpdateCoordinator : IStartupUpdateCoordinator
{
    private static int _started;
    public async Task<StartupUpdateResult> RunAsync(CancellationToken cancellationToken = default)
    {
        if (Interlocked.Exchange(ref _started, 1) != 0) return StartupUpdateResult.Failed;
        try
        {
            var manager = new UpdateManager(new GithubSource("https://github.com/onehoon/OptiEditor", null, false));
            if (!manager.IsInstalled) { AppServices.Logger.Info("Velopack update skipped: development or portable build."); return StartupUpdateResult.NotInstalled; }
            AppServices.Logger.Info("Startup update check started.");
            if (manager.UpdatePendingRestart is { } pending) { AppServices.Logger.Info("Applying pending OptiEditor update."); manager.ApplyUpdatesAndRestart(pending); return StartupUpdateResult.UpdateRestartStarted; }
            using var checkTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            checkTimeout.CancelAfter(TimeSpan.FromSeconds(15));
            var update = await manager.CheckForUpdatesAsync().WaitAsync(checkTimeout.Token); if (update is null) { AppServices.Logger.Info("No OptiEditor update found."); return StartupUpdateResult.NoUpdate; }
            AppServices.Logger.Info($"OptiEditor update found: {update.TargetFullRelease.Version}.");
            using var downloadTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            downloadTimeout.CancelAfter(TimeSpan.FromMinutes(15));
            await manager.DownloadUpdatesAsync(update, null, downloadTimeout.Token); AppServices.Logger.Info("Applying downloaded OptiEditor update."); manager.ApplyUpdatesAndRestart(update.TargetFullRelease); return StartupUpdateResult.UpdateRestartStarted;
        }
        catch (Exception ex) { AppServices.Logger.Error("Startup update failed; launching current OptiEditor version.", ex); return StartupUpdateResult.Failed; }
    }
}
