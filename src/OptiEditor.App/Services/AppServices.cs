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
    public static IDiagnosticLogger Logger { get; } = new FileDiagnosticLogger(AppData);
    public static ScanRootStore ScanRoots { get; } = new(AppData, Logger);
    public static InstallationDiscoveryScanner Scanner { get; } = new(new SystemFileVersionInfoProvider(), Logger);
    public static IInstallationCatalog Installations { get; } = new InstallationCatalog(ScanRoots, Scanner);
    public static IIniFileService IniFiles { get; } = new IniFileService();
    public static OptiSchemaResolver Schemas { get; } = new();
    public static IEditorVisibilityStore EditorVisibility { get; } = new EditorVisibilityStore(AppData, Logger);
    public static IStartupTabStore StartupTab { get; } = new StartupTabStore(AppData, Logger);
    public static ISourceCommentVisibilityStore SourceComments { get; } = new SourceCommentVisibilityStore(AppData, Logger);
    public static IUserPresetStore Presets { get; } = new UserPresetStore(AppData, Logger);
    public static IBuiltInPresetProvider BuiltInPresets { get; } = new BuiltInPresetProvider();
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
    public static OptiScalerSourceValidator OptiScalerSourceValidator { get; } = new(new SystemFileVersionInfoProvider());
    public static OptiScalerReplacementService OptiScalerReplacement { get; } = new(new SystemFileVersionInfoProvider(), OptiScalerSourceValidator, Logger, AppData);
}
