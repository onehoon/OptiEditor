using Microsoft.UI.Xaml;
using Velopack;
using OptiEditor.App.Services;

namespace OptiEditor.App;
public static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        StartupDiagnostics.Info($"Process startup. Arguments: {string.Join(' ', args)}");

        // Held for the whole process lifetime (released on process exit / Main
        // return). Prevents two OptiEditor processes from running concurrently,
        // since concurrent processes writing the same scan-root/preset/settings
        // JSON files or the same OptiScaler.ini would race past each other's
        // per-path save gates (those gates are per-process, in-memory only) and
        // corrupt state.
        using var singleInstanceMutex = new Mutex(initiallyOwned: true, name: "OptiEditor-SingleInstance", out var createdNew);
        if (!createdNew)
        {
            StartupDiagnostics.Info("Another OptiEditor instance is already running. Exiting.");
            return;
        }

        AppDomain.CurrentDomain.UnhandledException += (_, eventArgs) => StartupDiagnostics.Error("Unhandled AppDomain exception.", eventArgs.ExceptionObject as Exception);
        TaskScheduler.UnobservedTaskException += (_, eventArgs) => { StartupDiagnostics.Error("Unobserved task exception.", eventArgs.Exception); eventArgs.SetObserved(); };
        try
        {
            StartupDiagnostics.Info("Starting Velopack lifecycle.");
            VelopackApp.Build().SetAutoApplyOnStartup(true).Run();
            StartupDiagnostics.Info("Velopack lifecycle completed. Initializing WinRT wrappers.");
            WinRT.ComWrappersSupport.InitializeComWrappers();
            Application.Start(_ =>
            {
                try
                {
                    StartupDiagnostics.Info("Creating WinUI application instance.");
                    var context = new Microsoft.UI.Dispatching.DispatcherQueueSynchronizationContext(Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread());
                    SynchronizationContext.SetSynchronizationContext(context);
                    new App();
                }
                catch (Exception ex) { StartupDiagnostics.Error("WinUI application creation failed.", ex); throw; }
            });
        }
        catch (Exception ex) { StartupDiagnostics.Error("Fatal startup failure.", ex); throw; }
    }
}
