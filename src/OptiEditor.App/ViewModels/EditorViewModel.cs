using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using OptiEditor.App.Services;
using OptiEditor.Core.Ini;
using OptiEditor.Core.Models;
using OptiEditor.Core.Schema;

namespace OptiEditor.App.ViewModels;

public partial class EditorSettingItemViewModel(SettingValueBinding binding) : ObservableObject
{
    public string DisplayName => binding.Definition.DisplayName;
    [ObservableProperty] public partial string CurrentRawValue { get; set; } = binding.CurrentRawValue;
    [ObservableProperty] public partial string? ValidationError { get; set; } = binding.ValidationError;
    public bool IsUnknownValue => binding.IsUnknownValue;
    public bool IsModified => binding.IsModified;
    public void Update() { binding.SetRawValue(CurrentRawValue); ValidationError = binding.ValidationError; OnPropertyChanged(nameof(IsModified)); OnPropertyChanged(nameof(IsUnknownValue)); }
    public void Revert() { binding.Revert(); CurrentRawValue = binding.CurrentRawValue; ValidationError = null; OnPropertyChanged(nameof(IsModified)); }
    public SettingValueBinding Binding => binding;
}

public partial class EditorViewModel : ObservableObject
{
    public ObservableCollection<EditorSettingItemViewModel> Settings { get; } = [];
    [ObservableProperty] public partial OptiInstallation? Installation { get; set; }
    [ObservableProperty] public partial string StatusText { get; set; } = "Loading settings...";
    [ObservableProperty] public partial bool IsBusy { get; set; }
    private IniEditorSession? _session;
    public bool IsDirty => Settings.Any(x => x.IsModified); public bool CanSave => IsDirty && !Settings.Any(x => x.ValidationError is not null) && !IsBusy;
    public async Task LoadAsync(OptiInstallation installation)
    {
        IsBusy = true; try { Installation = installation; var provider = AppServices.Schemas.Resolve(installation.SchemaFamily); _session = await new IniEditorSessionService(AppServices.IniFiles).OpenSessionAsync(installation); Settings.Clear(); foreach (var binding in SettingBindingFactory.CreateVisible(provider, _session.Document)) Settings.Add(new(binding)); StatusText = $"{Settings.Count} supported settings are available."; }
        catch (Exception ex) { AppServices.Logger.Error("Editor load failed.", ex); StatusText = "Settings could not be loaded."; }
        finally { IsBusy = false; OnPropertyChanged(nameof(CanSave)); }
    }
    public void Update(EditorSettingItemViewModel item) { item.Update(); OnPropertyChanged(nameof(IsDirty)); OnPropertyChanged(nameof(CanSave)); }
    public void RevertAll() { foreach (var item in Settings) item.Revert(); StatusText = "Changes were reverted."; OnPropertyChanged(nameof(IsDirty)); OnPropertyChanged(nameof(CanSave)); }
    public async Task SaveAsync()
    {
        if (_session is null || !CanSave) return; IsBusy = true;
        try { foreach (var item in Settings.Where(x => x.IsModified)) _session.Document.ApplyPatch(item.Binding.ToPatch()); var result = await AppServices.IniFiles.SaveAsync(_session.Document, _session.Snapshot); if (!result.Success) { StatusText = result.Error ?? "Settings could not be saved."; return; } await LoadAsync(Installation!); StatusText = "OptiScaler settings were saved successfully."; }
        catch (Exception ex) { AppServices.Logger.Error("Editor save failed.", ex); StatusText = "Settings could not be saved."; }
        finally { IsBusy = false; OnPropertyChanged(nameof(CanSave)); }
    }
}
