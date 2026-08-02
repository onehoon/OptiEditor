using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using OptiEditor.App.Services;
using OptiEditor.Core.Models;
using OptiEditor.Core.Schema;
using OptiEditor.Core.Storage;

namespace OptiEditor.App.ViewModels;

public partial class EditorVisibilitySectionItemViewModel : ObservableObject
{
    public EditorVisibilitySectionItemViewModel(string section, EditorVisibility effective, bool isOverridden)
    { Section = section; IsVisible = effective == EditorVisibility.Visible; IsOverridden = isOverridden; }
    public string Section { get; }
    public bool DefaultVisible => true;
    [ObservableProperty] public partial bool IsVisible { get; set; }
    [ObservableProperty] public partial bool IsOverridden { get; set; }
}

public partial class EditorVisibilitySettingsViewModel : ObservableObject
{
    public ObservableCollection<EditorVisibilitySectionItemViewModel> Sections { get; } = [];
    [ObservableProperty] public partial OptiSchemaFamily SelectedFamily { get; set; } = OptiSchemaFamily.V09;
    [ObservableProperty] public partial string StatusText { get; set; } = "Loading editor visibility settings...";

    public async Task LoadAsync(OptiSchemaFamily family)
    {
        SelectedFamily = family;
        try
        {
            var preferences = await AppServices.EditorVisibility.LoadAsync(); var schema = AppServices.Schemas.Resolve(family);
            Sections.Clear();
            foreach (var section in schema.Settings.Select(x => x.IniKey.Section).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                var definition = schema.Settings.First(x => string.Equals(x.IniKey.Section, section, StringComparison.OrdinalIgnoreCase));
                var overridden = preferences.Any(x => x.Family == family && string.Equals(x.Section, section, StringComparison.OrdinalIgnoreCase));
                Sections.Add(new(section, EditorVisibilityPolicy.Resolve(definition, family, preferences), overridden));
            }
            StatusText = "Use the toggles to control which INI sections are shown in Editor. Hidden keys remain preserved in the INI.";
        }
        catch (Exception ex) { AppServices.Logger.Error("Editor visibility settings could not be loaded.", ex); StatusText = "Visibility settings could not be loaded. See logs for details."; }
    }

    public async Task SaveAsync()
    {
        try
        {
            var all = (await AppServices.EditorVisibility.LoadAsync()).Where(x => x.Family != SelectedFamily).ToList();
            foreach (var item in Sections)
            {
                var current = item.IsVisible ? EditorVisibility.Visible : EditorVisibility.Hidden;
                if (current != EditorVisibility.Visible) all.Add(new(SelectedFamily, item.Section, current));
            }
            await AppServices.EditorVisibility.SaveAsync(all); foreach (var item in Sections) item.IsOverridden = item.IsVisible != item.DefaultVisible;
            StatusText = "Section visibility preferences were saved.";
        }
        catch (Exception ex) { AppServices.Logger.Error("Editor visibility settings could not be saved.", ex); StatusText = "Visibility settings could not be saved. See logs for details."; }
    }

    public async Task ResetAsync()
    {
        try { var all = (await AppServices.EditorVisibility.LoadAsync()).Where(x => x.Family != SelectedFamily); await AppServices.EditorVisibility.SaveAsync(all); await LoadAsync(SelectedFamily); StatusText = "Section visibility was reset to the app defaults."; }
        catch (Exception ex) { AppServices.Logger.Error("Editor visibility settings could not be reset.", ex); StatusText = "Visibility settings could not be reset. See logs for details."; }
    }
}
