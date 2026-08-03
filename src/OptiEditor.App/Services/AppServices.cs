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
    private static readonly string DeletedBuiltInPresetsPath = Path.Combine(AppData, "deleted-built-in-presets.json");
    private static readonly SemaphoreSlim DeletedBuiltInPresetsLock = new(1, 1);

    private static readonly Lazy<IDiagnosticLogger> LoggerInstance = new(() => new FileDiagnosticLogger(AppData));
    private static readonly Lazy<ScanRootStore> ScanRootsInstance = new(() => new ScanRootStore(AppData, Logger));
    private static readonly Lazy<InstallationDiscoveryScanner> ScannerInstance = new(() => new InstallationDiscoveryScanner(new SystemFileVersionInfoProvider(), Logger));
    private static readonly Lazy<IInstallationCatalog> InstallationsInstance = new(() => new InstallationCatalog(ScanRoots, Scanner));
    private static readonly Lazy<IIniFileService> IniFilesInstance = new(() => new IniFileService());
    private static readonly Lazy<OptiSchemaResolver> SchemasInstance = new(() => new OptiSchemaResolver());
    private static readonly Lazy<IEditorVisibilityStore> EditorVisibilityInstance = new(() => new EditorVisibilityStore(AppData, Logger));
    private static readonly Lazy<IStartupTabStore> StartupTabInstance = new(() => new StartupTabStore(AppData, Logger));
    private static readonly Lazy<ISourceCommentVisibilityStore> SourceCommentsInstance = new(() => new SourceCommentVisibilityStore(AppData, Logger));
    private static readonly Lazy<IUserPresetStore> PresetsInstance = new(() => new UserPresetStore(AppData, Logger));
    private static readonly Lazy<IBuiltInPresetProvider> BuiltInPresetsInstance = new(() => new BuiltInPresetProvider());
    private static readonly Lazy<OptiScalerSourceValidator> OptiScalerSourceValidatorInstance = new(() => new OptiScalerSourceValidator(new SystemFileVersionInfoProvider()));
    private static readonly Lazy<OptiScalerReplacementService> OptiScalerReplacementInstance = new(() => new OptiScalerReplacementService(new SystemFileVersionInfoProvider(), OptiScalerSourceValidator, Logger, AppData));

    public static IDiagnosticLogger Logger => LoggerInstance.Value;
    public static ScanRootStore ScanRoots => ScanRootsInstance.Value;
    public static InstallationDiscoveryScanner Scanner => ScannerInstance.Value;
    public static IInstallationCatalog Installations => InstallationsInstance.Value;
    public static IIniFileService IniFiles => IniFilesInstance.Value;
    public static OptiSchemaResolver Schemas => SchemasInstance.Value;
    public static IEditorVisibilityStore EditorVisibility => EditorVisibilityInstance.Value;
    public static IStartupTabStore StartupTab => StartupTabInstance.Value;
    public static ISourceCommentVisibilityStore SourceComments => SourceCommentsInstance.Value;
    public static IUserPresetStore Presets => PresetsInstance.Value;
    public static IBuiltInPresetProvider BuiltInPresets => BuiltInPresetsInstance.Value;

    public static async Task<IReadOnlyList<PresetDefinition>> LoadPresetsAsync()
    {
        var user = (await Presets.LoadAsync()).ToList();
        var deleted = await LoadDeletedBuiltInPresetIdsAsync();
        var changed = user.RemoveAll(x => deleted.Contains(x.Id)) > 0;
        foreach (var builtIn in BuiltInPresets.GetAll())
        {
            if (deleted.Contains(builtIn.Id) || user.Any(x => x.Id == builtIn.Id) || user.Any(x => x.Family == builtIn.Family && string.Equals(x.Name, builtIn.Name, StringComparison.OrdinalIgnoreCase))) continue;
            user.Add(builtIn with { Source = PresetSource.User });
            changed = true;
        }
        if (changed) await Presets.SaveAsync(user);
        return user;
    }

    public static async Task MarkBuiltInPresetDeletedAsync(Guid id)
    {
        await DeletedBuiltInPresetsLock.WaitAsync();
        try
        {
            var deleted = await LoadDeletedBuiltInPresetIdsAsync();
            if (!deleted.Add(id)) return;
            Directory.CreateDirectory(AppData);
            var temporaryPath = $"{DeletedBuiltInPresetsPath}.{Guid.NewGuid():N}.tmp";
            try
            {
                await File.WriteAllTextAsync(temporaryPath, System.Text.Json.JsonSerializer.Serialize(deleted));
                File.Move(temporaryPath, DeletedBuiltInPresetsPath, true);
            }
            finally { if (File.Exists(temporaryPath)) File.Delete(temporaryPath); }
        }
        finally { DeletedBuiltInPresetsLock.Release(); }
    }

    private static async Task<HashSet<Guid>> LoadDeletedBuiltInPresetIdsAsync()
    {
        if (!File.Exists(DeletedBuiltInPresetsPath)) return [];
        try { await using var stream = File.OpenRead(DeletedBuiltInPresetsPath); return (await System.Text.Json.JsonSerializer.DeserializeAsync<HashSet<Guid>>(stream)) ?? []; }
        catch (Exception ex) when (ex is System.Text.Json.JsonException or IOException or UnauthorizedAccessException)
        {
            Logger.Error("Deleted built-in preset records could not be loaded safely.", ex);
            throw new PresetStoreException("Deleted built-in preset records could not be loaded safely.", ex);
        }
    }

    public static OptiScalerSourceValidator OptiScalerSourceValidator => OptiScalerSourceValidatorInstance.Value;
    public static OptiScalerReplacementService OptiScalerReplacement => OptiScalerReplacementInstance.Value;
}
