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
