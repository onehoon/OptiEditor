using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using OptiEditor.App.Updates;
using OptiEditor.App.Services;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace OptiEditor.App;

/// <summary>
/// Provides application-specific behavior to supplement the default Application class.
/// </summary>
public partial class App : Application
{
    /// <summary>
    /// The main application window. Use <c>App.Window</c> from any class that needs
    /// the window reference (for dialogs, pickers, interop, etc.).
    /// </summary>
    public static Window Window { get; private set; } = null!;

    /// <summary>
    /// The UI thread dispatcher. Use <c>App.DispatcherQueue</c> to marshal calls
    /// to the UI thread. Fully qualified to avoid CS0104 ambiguity with
    /// <see cref="Windows.System.DispatcherQueue"/>.
    /// </summary>
    public static Microsoft.UI.Dispatching.DispatcherQueue DispatcherQueue { get; private set; } = null!;

    /// <summary>
    /// The native window handle (HWND). Use for file pickers,
    /// <c>DataTransferManager</c>, and any WinRT interop that requires
    /// <c>InitializeWithWindow</c>.
    /// </summary>
    public static nint WindowHandle =>
        WinRT.Interop.WindowNative.GetWindowHandle(Window);

    /// <summary>
    /// Initializes the singleton application object.
    /// </summary>
    public App()
    {
        UnhandledException += (_, eventArgs) => StartupDiagnostics.Error("Unhandled WinUI exception.", eventArgs.Exception);
        InitializeComponent();
    }

    /// <summary>
    /// Invoked when the application is launched.
    /// </summary>
    /// <param name="args">Details about the launch request and process.</param>
    protected override async void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
    {
        try
        {
            StartupDiagnostics.Info("WinUI launch activated. Starting application update check.");
            var updateResult = await new StartupUpdateCoordinator().RunAsync();
            StartupDiagnostics.Info($"Application update decision: {updateResult}.");
            if (updateResult == StartupUpdateResult.UpdateRestartStarted) return;
            StartupDiagnostics.Info("Creating main window.");
            Window = new MainWindow();
            DispatcherQueue = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();
            Window.Activate();
            StartupDiagnostics.Info("Main window activated. Starting installation scan.");
            _ = StartInstallationScanAsync();
        }
        catch (Exception ex) { StartupDiagnostics.Error("Main window startup failed.", ex); throw; }
    }

    private static async Task StartInstallationScanAsync()
    {
        try { await OptiEditor.App.Services.AppServices.Installations.ScanAllAsync(); }
        catch (Exception ex) { OptiEditor.App.Services.AppServices.Logger.Error("Startup installation scan failed.", ex); }
    }
}
