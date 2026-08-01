using OptiEditor.Core.Discovery;
using OptiEditor.Core.Storage;
using OptiEditor.Core.Utilities;
using OptiEditor.Core.Ini;
using OptiEditor.Core.Schema;
using OptiEditor.Core.Presets;
using OptiEditor.Core.OptiScalerUpdate;

namespace OptiEditor.App.Services;

public static class AppServices
{
    private static readonly string AppData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OptiEditor");
    public static ScanRootStore ScanRoots { get; } = new(AppData);
    public static IDiagnosticLogger Logger { get; } = new FileDiagnosticLogger(AppData);
    public static InstallationDiscoveryScanner Scanner { get; } = new(new SystemFileVersionInfoProvider(), Logger);
    public static IInstallationCatalog Installations { get; } = new InstallationCatalog(ScanRoots, Scanner);
    public static IIniFileService IniFiles { get; } = new IniFileService();
    public static OptiSchemaResolver Schemas { get; } = new();
    public static IEditorVisibilityStore EditorVisibility { get; } = new EditorVisibilityStore(AppData);
    public static IUserPresetStore Presets { get; } = new UserPresetStore(AppData, Logger);
    public static IBuiltInPresetProvider BuiltInPresets { get; } = new BuiltInPresetProvider();
    public static OptiScalerSourceValidator OptiScalerSourceValidator { get; } = new(new SystemFileVersionInfoProvider());
    public static OptiScalerReplacementService OptiScalerReplacement { get; } = new(new SystemFileVersionInfoProvider(), OptiScalerSourceValidator, Logger, AppData);
}
