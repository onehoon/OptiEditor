using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using OptiEditor.App.Services;
using OptiEditor.Core.Ini;
using OptiEditor.Core.Models;
using OptiEditor.Core.Schema;
using OptiEditor.Core.Storage;

namespace OptiEditor.App.ViewModels;

public partial class EditorSettingItemViewModel(SettingValueBinding binding) : ObservableObject
{
    public string DisplayName => binding.Definition.DisplayName;
    public string Description => binding.Definition.Description;
    public string GroupName => binding.Definition.GroupId;
    public string? GroupDisplayName { get; set; }
    public IReadOnlyList<SettingOption> Options => binding.Definition.Options;
    public string InputHint => binding.Definition.ValueKind switch { SettingValueKind.Shortcut => "Auto, -1, decimal key, or 0xNN", SettingValueKind.Double => NumericHint("Number"), SettingValueKind.Integer => NumericHint("Integer"), _ => "Value" };
    private string NumericHint(string label) => $"{label}{(binding.Definition.Minimum is null ? "" : $" (min {binding.Definition.Minimum})")}{(binding.Definition.Maximum is null ? "" : $" (max {binding.Definition.Maximum})")}";
    [ObservableProperty] public partial string CurrentRawValue { get; set; } = binding.CurrentRawValue;
    [ObservableProperty] public partial string? ValidationError { get; set; } = binding.ValidationError;
    public bool IsModified => binding.IsModified;
    public bool HasBlockingValidationError => binding.HasValidationError;
    public string? BlockingValidationMessage => HasBlockingValidationError ? ValidationError : null;
    public string? UnknownValueWarningMessage => binding.IsUnknownValue && !IsModified ? "Unknown value will be preserved unless changed." : null;
    public void Update() { binding.SetRawValue(CurrentRawValue); ValidationError = binding.ValidationError; OnPropertyChanged(nameof(IsModified)); OnPropertyChanged(nameof(HasBlockingValidationError)); OnPropertyChanged(nameof(BlockingValidationMessage)); OnPropertyChanged(nameof(UnknownValueWarningMessage)); }
    public void Revert() { binding.Revert(); CurrentRawValue = binding.CurrentRawValue; ValidationError = binding.ValidationError; OnPropertyChanged(nameof(IsModified)); OnPropertyChanged(nameof(HasBlockingValidationError)); OnPropertyChanged(nameof(BlockingValidationMessage)); OnPropertyChanged(nameof(UnknownValueWarningMessage)); }
    public SettingValueBinding Binding => binding;
}

public partial class EditorViewModel : ObservableObject
{
    public ObservableCollection<EditorSettingItemViewModel> Settings { get; } = [];
    [ObservableProperty] public partial OptiInstallation? Installation { get; set; }
    [ObservableProperty] public partial string StatusText { get; set; } = "Loading settings...";
    [ObservableProperty] public partial bool IsBusy { get; set; }
    private IniEditorSession? _session;
    public bool IsDirty => Settings.Any(x => x.IsModified);
    public bool CanSave => IsDirty && !Settings.Any(x => x.HasBlockingValidationError) && !IsBusy;
    public async Task LoadAsync(OptiInstallation installation)
    {
        IsBusy = true;
        try
        {
            Installation = installation;
            var provider = AppServices.Schemas.Resolve(installation.SchemaFamily);
            var visibility = await AppServices.EditorVisibility.LoadAsync();
            _session = await new IniEditorSessionService(AppServices.IniFiles).OpenSessionAsync(installation);
            Settings.Clear();
            string? previousGroup = null;
            foreach (var binding in SettingBindingFactory.CreateVisible(provider, _session.Document).Where(x => EditorVisibilityPolicy.Resolve(x.Definition, installation.SchemaFamily, visibility) == EditorVisibility.Visible))
            {
                var item = new EditorSettingItemViewModel(binding);
                if (!string.Equals(previousGroup, binding.Definition.GroupId, StringComparison.Ordinal))
                {
                    item.GroupDisplayName = provider.Groups.FirstOrDefault(x => x.Id == binding.Definition.GroupId)?.DisplayName ?? binding.Definition.GroupId;
                    previousGroup = binding.Definition.GroupId;
                }
                Settings.Add(item);
            }
            StatusText = $"{Settings.Count} supported settings are available.";
        }
        catch (Exception ex) { AppServices.Logger.Error("Editor load failed.", ex); StatusText = "Settings could not be loaded."; }
        finally { IsBusy = false; OnPropertyChanged(nameof(CanSave)); }
    }
    public void Update(EditorSettingItemViewModel item) { item.Update(); OnPropertyChanged(nameof(IsDirty)); OnPropertyChanged(nameof(CanSave)); }
    public void RevertAll() { foreach (var item in Settings) item.Revert(); StatusText = "Changes were reverted."; OnPropertyChanged(nameof(IsDirty)); OnPropertyChanged(nameof(CanSave)); }
    public void ResetAllManagedToAuto() { foreach (var item in Settings.Where(x => x.Binding.Definition.SupportsAuto)) { item.CurrentRawValue = "auto"; item.Update(); } StatusText = "Managed settings were reset to Auto. Select Save to update OptiScaler.ini."; OnPropertyChanged(nameof(IsDirty)); OnPropertyChanged(nameof(CanSave)); }
    public async Task SaveAsync()
    {
        if (_session is null) return;
        if (!CanSave) { if (Settings.Any(x => x.HasBlockingValidationError)) StatusText = "Correct invalid setting values before saving."; return; }
        IsBusy = true;
        try
        {
            // Patch a clone rather than the session document so a rejected save
            // (a failed patch or an external-change conflict) never leaves the
            // live session mutated; it stays reusable for retry or reload.
            var candidate = _session.Document.Clone();
            var failure = Settings.Where(x => x.IsModified).Select(item => candidate.ApplyPatch(item.Binding.ToPatch())).FirstOrDefault(x => x.Error is not null);
            if (failure is not null) { StatusText = $"'{failure.Key.Section}.{failure.Key.Name}' could not be saved: {failure.Error}"; return; }
            var result = await AppServices.IniFiles.SaveAsync(candidate, _session.Snapshot);
            if (!result.Success) { StatusText = result.Error ?? "Settings could not be saved."; return; }
            await LoadAsync(Installation!);
            StatusText = "OptiScaler settings were saved successfully.";
        }
        catch (Exception ex) { AppServices.Logger.Error("Editor save failed.", ex); StatusText = "Settings could not be saved."; }
        finally { IsBusy = false; OnPropertyChanged(nameof(CanSave)); }
    }
}
