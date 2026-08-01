using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Dispatching;
using OptiEditor.App.Services;
using OptiEditor.Core.Models;
using OptiEditor.Core.OptiScalerUpdate;

namespace OptiEditor.App.ViewModels;

public partial class OptiScalerUpdateViewModel : ObservableObject, IDisposable
{
    private bool _changingBulk;
    private CancellationTokenSource? _operationCancellation;
    private readonly DispatcherQueue? _dispatcher = DispatcherQueue.GetForCurrentThread();
    public ObservableCollection<OptiScalerUpdateItemViewModel> Items { get; } = [];
    [ObservableProperty] public partial SourceOptiScalerBinary? Source { get; set; }
    [ObservableProperty] public partial string? SourceError { get; set; }
    [ObservableProperty] public partial bool IsV09Selected { get; set; }
    [ObservableProperty] public partial bool IsV10Selected { get; set; }
    [ObservableProperty] public partial bool IsAllSelected { get; set; }
    [ObservableProperty] public partial string SearchText { get; set; } = "";
    [ObservableProperty] public partial bool IsBusy { get; set; }
    [ObservableProperty] public partial string StatusText { get; set; } = "Scanning installed OptiScaler instances...";
    [ObservableProperty] public partial int ProgressCurrent { get; set; }
    [ObservableProperty] public partial int ProgressTotal { get; set; }
    [ObservableProperty] public partial IReadOnlyList<OptiScalerReplacementResult> Results { get; set; } = [];

    public OptiScalerUpdateViewModel()
    {
        AppServices.Installations.InstallationsChanged += OnInstallationsChanged;
        RefreshCatalog();
    }
    public void Dispose()
    {
        _operationCancellation?.Cancel();
        AppServices.Installations.InstallationsChanged -= OnInstallationsChanged;
        foreach (var item in Items) item.PropertyChanged -= OnItemPropertyChanged;
    }
    private void OnInstallationsChanged(object? sender, EventArgs args)
    {
        if (_dispatcher is null || _dispatcher.HasThreadAccess) RefreshCatalog();
        else _dispatcher.TryEnqueue(RefreshCatalog);
    }

    public bool IsBulkMode => IsAllSelected || IsV09Selected || IsV10Selected;
    public bool IsIndividualSelectionEnabled => !IsBulkMode && !IsBusy && !AppServices.Installations.IsScanning;
    public int SelectedCount => Items.Count(x => x.IsSelected);
    public bool CanReplace => Source is not null && !IsBusy && !AppServices.Installations.IsScanning && SelectedCount > 0;
    public string SourceDetails => Source is null ? "No source file selected" : $"{Source.Path}\nFile version: {Source.FileVersion}\nProduct version: {Source.ProductVersion ?? "Not available"}\nSize: {Source.FileSize:N0} bytes";
    public IEnumerable<OptiScalerUpdateItemViewModel> FilteredItems => Items.Where(MatchesSearch);

    public void SelectSource(string path)
    {
        var validation = AppServices.OptiScalerSourceValidator.Validate(path);
        Source = validation.Source;
        SourceError = validation.Error;
        StatusText = validation.IsValid ? "Source OptiScaler binary selected." : "Source selection failed.";
        OnPropertyChanged(nameof(CanReplace));
    }
    partial void OnSourceChanged(SourceOptiScalerBinary? value) { OnPropertyChanged(nameof(SourceDetails)); OnPropertyChanged(nameof(CanReplace)); }

    public async Task<IReadOnlyList<OptiScalerReplacementResult>> ReplaceAsync(CancellationToken cancellationToken = default)
    {
        if (!CanReplace || Source is null) return [];
        IsBusy = true; _operationCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        try
        {
            StatusText = "Preparing replacement...";
            var plan = await BuildPlanAsync(_operationCancellation.Token);
            if (plan.Targets.Count == 0) { Results = plan.SkippedTargets; StatusText = "No selected installations remain valid."; return Results; }
            ProgressTotal = plan.Targets.Count; ProgressCurrent = 0;
            var progress = new Progress<OptiScalerReplacementProgress>(p => { ProgressCurrent = p.Completed; ProgressTotal = p.Total; StatusText = $"Replacing OptiScaler binaries... {p.Completed} of {p.Total}"; });
            Results = await AppServices.OptiScalerReplacement.ReplaceAsync(plan, progress, _operationCancellation.Token);
            var refreshDirectories = Results.Where(x => x.Status is OptiScalerReplacementStatus.Replaced or OptiScalerReplacementStatus.Failed).Select(x => x.InstallDirectory).Distinct(StringComparer.OrdinalIgnoreCase);
            await AppServices.Installations.RescanDirectoriesAsync(refreshDirectories, CancellationToken.None);
            StatusText = "Replacement completed.";
            return Results;
        }
        catch (OperationCanceledException) { StatusText = "Replacement canceled."; return Results; }
        catch (Exception ex) { AppServices.Logger.Error("OptiScaler replacement preparation failed.", ex); SourceError = ex.Message; StatusText = "Replacement could not start."; return Results; }
        finally { _operationCancellation?.Dispose(); _operationCancellation = null; IsBusy = false; OnPropertiesChanged(); }
    }

    public void Cancel() => _operationCancellation?.Cancel();

    private async Task<OptiScalerReplacementPlan> BuildPlanAsync(CancellationToken token)
    {
        var selectedDirectories = IsBulkMode ? AppServices.Installations.Installations.Select(x => x.InstallDirectory) : Items.Where(x => x.IsSelected).Select(x => x.DirectoryIdentity);
        var requested = selectedDirectories.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var refreshed = await AppServices.Installations.RescanDirectoriesAsync(requested, token);
        var valid = refreshed.Where(x => IsAllSelected || (IsV09Selected && x.SchemaFamily == OptiSchemaFamily.V09) || (IsV10Selected && x.SchemaFamily == OptiSchemaFamily.V10) || !IsBulkMode).ToArray();
        var missing = requested.Where(directory => !refreshed.Any(x => string.Equals(x.InstallDirectory, directory, StringComparison.OrdinalIgnoreCase)))
            .Select(directory => new OptiScalerReplacementResult(directory, null, "Unknown", null, null, OptiScalerReplacementStatus.Skipped, OptiScalerReplacementReason.TargetMissing, "The installation is no longer valid."));
        return new(Source!, valid.Select(x => new OptiScalerReplacementTarget(x)).ToArray(), missing.ToArray());
    }

    partial void OnIsAllSelectedChanged(bool value)
    {
        if (_changingBulk) return;
        _changingBulk = true; if (value) { IsV09Selected = false; IsV10Selected = false; } _changingBulk = false;
        ApplyBulkSelection();
    }
    partial void OnIsV09SelectedChanged(bool value) { if (!_changingBulk && value && IsAllSelected) { _changingBulk = true; IsAllSelected = false; _changingBulk = false; } ApplyBulkSelection(); }
    partial void OnIsV10SelectedChanged(bool value) { if (!_changingBulk && value && IsAllSelected) { _changingBulk = true; IsAllSelected = false; _changingBulk = false; } ApplyBulkSelection(); }
    partial void OnSearchTextChanged(string value) => OnPropertyChanged(nameof(FilteredItems));

    private void ApplyBulkSelection()
    {
        foreach (var item in Items)
        {
            item.IsSelectionEnabled = !IsBulkMode && !IsBusy && !AppServices.Installations.IsScanning;
            item.IsSelected = IsAllSelected || (IsV09Selected && item.Installation.SchemaFamily == OptiSchemaFamily.V09) || (IsV10Selected && item.Installation.SchemaFamily == OptiSchemaFamily.V10);
        }
        OnPropertiesChanged();
    }
    private void RefreshCatalog()
    {
        var existing = Items.ToDictionary(x => x.DirectoryIdentity, StringComparer.OrdinalIgnoreCase);
        foreach (var installation in AppServices.Installations.Installations)
        {
            if (existing.Remove(installation.InstallDirectory, out var item)) item.Update(installation); else AddItem(installation);
        }
        foreach (var obsolete in existing.Values) Items.Remove(obsolete);
        if (AppServices.Installations.IsScanning) StatusText = "Scanning installed OptiScaler instances...";
        ApplyBulkSelection(); OnPropertiesChanged();
    }
    private bool MatchesSearch(OptiScalerUpdateItemViewModel item)
    {
        var search = SearchText.Trim(); if (search.Length == 0) return true;
        var x = item.Installation;
        return x.GameDisplayName.Contains(search, StringComparison.OrdinalIgnoreCase) || x.GameExeName?.Contains(search, StringComparison.OrdinalIgnoreCase) == true || x.OptiBinaryFileName.Contains(search, StringComparison.OrdinalIgnoreCase) || x.InstallDirectory.Contains(search, StringComparison.OrdinalIgnoreCase) || x.FileVersion.ToString().Contains(search, StringComparison.OrdinalIgnoreCase);
    }
    private void AddItem(OptiInstallation installation)
    {
        var item = new OptiScalerUpdateItemViewModel(installation);
        item.PropertyChanged += OnItemPropertyChanged;
        Items.Add(item);
    }
    private void OnItemPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs args) { if (args.PropertyName == nameof(OptiScalerUpdateItemViewModel.IsSelected)) OnPropertiesChanged(); }
    private void OnPropertiesChanged()
    {
        OnPropertyChanged(nameof(IsBulkMode)); OnPropertyChanged(nameof(IsIndividualSelectionEnabled)); OnPropertyChanged(nameof(SelectedCount)); OnPropertyChanged(nameof(CanReplace)); OnPropertyChanged(nameof(FilteredItems));
    }
}
