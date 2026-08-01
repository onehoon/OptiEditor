using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using OptiEditor.App.Services;
using OptiEditor.Core.Models;
using OptiEditor.Core.Schema;
using OptiEditor.Core.Storage;

namespace OptiEditor.App.ViewModels;

public partial class EditorVisibilitySettingItemViewModel : ObservableObject
{
    public EditorVisibilitySettingItemViewModel(SettingDefinition definition, EditorVisibility effective, bool isOverridden)
    { Definition = definition; IsVisible = effective == EditorVisibility.Visible; IsOverridden = isOverridden; }
    public SettingDefinition Definition { get; }
    public string DisplayName => Definition.DisplayName;
    public string GroupName => Definition.GroupId;
    public string Description => Definition.Description;
    public bool DefaultVisible => Definition.DefaultEditorVisibility == EditorVisibility.Visible;
    [ObservableProperty] public partial bool IsVisible { get; set; }
    [ObservableProperty] public partial bool IsOverridden { get; set; }
}

public partial class EditorVisibilitySettingsViewModel : ObservableObject
{
    public ObservableCollection<EditorVisibilitySettingItemViewModel> Settings { get; } = [];
    [ObservableProperty] public partial OptiSchemaFamily SelectedFamily { get; set; } = OptiSchemaFamily.V10;
    [ObservableProperty] public partial string StatusText { get; set; } = "Loading editor visibility settings...";

    public async Task LoadAsync(OptiSchemaFamily family)
    {
        SelectedFamily = family;
        try
        {
            var preferences = await AppServices.EditorVisibility.LoadAsync(); var schema = AppServices.Schemas.Resolve(family);
            Settings.Clear(); foreach (var definition in schema.Settings) { var overridden = preferences.Any(x => x.Family == family && string.Equals(x.SettingId, definition.Id, StringComparison.OrdinalIgnoreCase)); Settings.Add(new(definition, EditorVisibilityPolicy.Resolve(definition, family, preferences), overridden)); }
            StatusText = "Use the toggles to control which existing keys are shown in Editor. Hidden keys remain preserved in the INI.";
        }
        catch (Exception ex) { AppServices.Logger.Error("Editor visibility settings could not be loaded.", ex); StatusText = "Visibility settings could not be loaded. See logs for details."; }
    }

    public async Task SaveAsync()
    {
        try
        {
            var all = (await AppServices.EditorVisibility.LoadAsync()).Where(x => x.Family != SelectedFamily).ToList();
            foreach (var item in Settings)
            {
                var current = item.IsVisible ? EditorVisibility.Visible : EditorVisibility.Hidden;
                if (current != item.Definition.DefaultEditorVisibility) all.Add(new(SelectedFamily, item.Definition.Id, current));
            }
            await AppServices.EditorVisibility.SaveAsync(all); foreach (var item in Settings) item.IsOverridden = item.IsVisible != item.DefaultVisible;
            StatusText = "Editor visibility preferences were saved.";
        }
        catch (Exception ex) { AppServices.Logger.Error("Editor visibility settings could not be saved.", ex); StatusText = "Visibility settings could not be saved. See logs for details."; }
    }

    public async Task ResetAsync()
    {
        try { var all = (await AppServices.EditorVisibility.LoadAsync()).Where(x => x.Family != SelectedFamily); await AppServices.EditorVisibility.SaveAsync(all); await LoadAsync(SelectedFamily); StatusText = "Editor visibility was reset to the app defaults."; }
        catch (Exception ex) { AppServices.Logger.Error("Editor visibility settings could not be reset.", ex); StatusText = "Visibility settings could not be reset. See logs for details."; }
    }
}
