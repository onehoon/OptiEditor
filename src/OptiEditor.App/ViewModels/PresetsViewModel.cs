using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using OptiEditor.App.Services;
using OptiEditor.Core.Models;
using OptiEditor.Core.Presets;

namespace OptiEditor.App.ViewModels;

public sealed class PresetCardItem(PresetDefinition preset, IReadOnlyList<string> settingLines)
{
    public PresetDefinition Preset { get; } = preset;
    public string NameWithVersion => $"{Preset.Name} ({(Preset.Family == OptiSchemaFamily.V09 ? "v0.9" : "v0.10")})";
    public string? Description => Preset.Description;
    public IReadOnlyList<string> SettingLines { get; } = settingLines;
}

public partial class PresetsViewModel : ObservableObject
{
    public ObservableCollection<PresetCardItem> Presets { get; } = [];
    public IEnumerable<PresetCardItem> FilteredPresets => Presets.Where(MatchesSearch);
    [ObservableProperty] public partial string SearchText { get; set; } = "";
    [ObservableProperty] public partial string StatusText { get; set; } = "Loading presets...";

    public async Task LoadAsync()
    {
        try
        {
            var presets = await AppServices.LoadPresetsAsync();
            Presets.Clear();
            foreach (var preset in presets.OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase)) Presets.Add(CreateCardItem(preset));
            StatusText = Presets.Count == 0 ? "No presets are available." : $"{Presets.Count} presets available.";
            OnPropertyChanged(nameof(FilteredPresets));
        }
        catch (Exception ex) { AppServices.Logger.Error("Preset list could not be loaded.", ex); StatusText = "Presets could not be loaded. See logs for details."; }
    }

    public async Task DeleteAsync(PresetDefinition preset)
    {
        if (preset.Source != PresetSource.User) return;
        try { var users = (await AppServices.Presets.LoadAsync()).Where(x => x.Id != preset.Id).ToList(); await AppServices.Presets.SaveAsync(users); if (AppServices.BuiltInPresets.GetAll().Any(x => x.Id == preset.Id)) await AppServices.MarkBuiltInPresetDeletedAsync(preset.Id); await LoadAsync(); }
        catch (Exception ex) { AppServices.Logger.Error("Preset could not be deleted.", ex); StatusText = "Preset could not be deleted safely. See logs for details."; }
    }

    public async Task<string?> SaveAsync(PresetDefinition preset)
    {
        try
        {
            var users = (await AppServices.Presets.LoadAsync()).ToList();
            var error = new PresetValidationService(AppServices.Schemas).Validate(preset, users);
            if (error is not null) return error;
            var existing = users.FindIndex(x => x.Id == preset.Id);
            if (existing >= 0) users[existing] = preset; else users.Add(preset);
            await AppServices.Presets.SaveAsync(users);
            await LoadAsync();
            return null;
        }
        catch (Exception ex) { AppServices.Logger.Error("Preset could not be saved.", ex); return "Preset could not be saved safely. See logs for details."; }
    }

    partial void OnSearchTextChanged(string value) => OnPropertyChanged(nameof(FilteredPresets));
    private bool MatchesSearch(PresetCardItem preset) => string.IsNullOrWhiteSpace(SearchText) || preset.Preset.Name.Contains(SearchText.Trim(), StringComparison.OrdinalIgnoreCase) || preset.Description?.Contains(SearchText.Trim(), StringComparison.OrdinalIgnoreCase) == true || preset.NameWithVersion.Contains(SearchText.Trim(), StringComparison.OrdinalIgnoreCase);

    private static PresetCardItem CreateCardItem(PresetDefinition preset)
    {
        var schema = AppServices.Schemas.Resolve(preset.Family);
        var lines = preset.Entries
            .OrderBy(entry => schema.FindById(entry.SettingId)?.Order ?? int.MaxValue)
            .ThenBy(entry => entry.SettingId, StringComparer.OrdinalIgnoreCase)
            .Select(entry => schema.FindById(entry.SettingId) is { } definition ? $"[{definition.IniKey.Section}] {definition.IniKey.Name} = {entry.RawValue}" : $"[Unknown] {entry.SettingId} = {entry.RawValue}")
            .ToArray();
        return new PresetCardItem(preset, lines);
    }
}
