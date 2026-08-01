using OptiEditor.Core.Discovery;
using OptiEditor.Core.Storage;
using OptiEditor.Core.Utilities;

namespace OptiEditor.App.Services;

public static class AppServices
{
    private static readonly string AppData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OptiEditor");
    public static ScanRootStore ScanRoots { get; } = new(AppData);
    public static IDiagnosticLogger Logger { get; } = new FileDiagnosticLogger(AppData);
    public static InstallationDiscoveryScanner Scanner { get; } = new(new SystemFileVersionInfoProvider(), Logger);
}
