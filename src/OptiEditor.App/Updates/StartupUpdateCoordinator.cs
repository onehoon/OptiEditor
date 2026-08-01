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
            var update = await manager.CheckForUpdatesAsync(); if (update is null) { AppServices.Logger.Info("No OptiEditor update found."); return StartupUpdateResult.NoUpdate; }
            AppServices.Logger.Info($"OptiEditor update found: {update.TargetFullRelease.Version}."); await manager.DownloadUpdatesAsync(update, null, cancellationToken); AppServices.Logger.Info("Applying downloaded OptiEditor update."); manager.ApplyUpdatesAndRestart(update.TargetFullRelease); return StartupUpdateResult.UpdateRestartStarted;
        }
        catch (Exception ex) { AppServices.Logger.Error("Startup update failed; launching current OptiEditor version.", ex); return StartupUpdateResult.Failed; }
    }
}
