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
    private CancellationTokenSource? _scanCancellation;
    private readonly DispatcherQueue? _dispatcher = DispatcherQueue.GetForCurrentThread();
    public ObservableCollection<OptiScalerUpdateItemViewModel> Items { get; } = [];
    public IReadOnlyList<OptiScalerUpdateRow> UpdateRows { get; private set; } = [];
    [ObservableProperty] public partial SourceOptiScalerBinary? Source { get; set; }
    [ObservableProperty] public partial string? SourceError { get; set; }
    [ObservableProperty] public partial bool IsV09Selected { get; set; }
    [ObservableProperty] public partial bool IsV10Selected { get; set; }
    [ObservableProperty] public partial bool IsAllSelected { get; set; }
    [ObservableProperty] public partial string SearchText { get; set; } = "";
    [ObservableProperty] public partial bool IsBusy { get; set; }
    [ObservableProperty] public partial bool IsScanning { get; set; }
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
        _scanCancellation?.Cancel();
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
    public bool CanScan => !IsBusy && !IsScanning && !AppServices.Installations.IsScanning;
    public bool IsWorking => IsBusy || IsScanning;
    public bool CanCancel => IsWorking;
    public string SourceDetails => Source is null ? "No source file selected" : $"{Source.Path}\nFile version: {Source.FileVersion}\nProduct version: {Source.ProductVersion ?? "Not available"}";
    public bool HasSourceError => !string.IsNullOrWhiteSpace(SourceError);
    public bool HasStatusText => !string.IsNullOrWhiteSpace(StatusText);
    public IEnumerable<OptiScalerUpdateItemViewModel> FilteredItems => Items.Where(MatchesSearch);

    public void SelectSource(string path)
    {
        var validation = AppServices.OptiScalerSourceValidator.Validate(path);
        Source = validation.Source;
        SourceError = validation.Error;
        StatusText = validation.IsValid ? "" : "Source selection failed.";
        OnPropertyChanged(nameof(CanReplace));
    }
    public async Task ScanAsync()
    {
        if (!CanScan) return;
        IsScanning = true;
        StatusText = "Scanning installed OptiScaler instances...";
        _scanCancellation = new CancellationTokenSource();
        try
        {
            var result = await AppServices.Installations.ScanAllAsync(_scanCancellation.Token);
            RefreshCatalog();
            StatusText = $"Scan complete: {result.Summary.ValidInstallations} installation(s) found.";
        }
        catch (OperationCanceledException) { StatusText = "Scan cancelled."; }
        catch (Exception ex) { AppServices.Logger.Error("Unexpected update scan error.", ex); StatusText = "Scan completed with an unexpected error. See logs for details."; }
        finally
        {
            _scanCancellation?.Dispose(); _scanCancellation = null; IsScanning = false;
            OnPropertiesChanged();
        }
    }
    partial void OnSourceChanged(SourceOptiScalerBinary? value) { OnPropertyChanged(nameof(SourceDetails)); OnPropertyChanged(nameof(CanReplace)); }
    partial void OnIsBusyChanged(bool value) => NotifyWorkStateChanged();
    partial void OnIsScanningChanged(bool value) => NotifyWorkStateChanged();

    private void NotifyWorkStateChanged()
    {
        OnPropertyChanged(nameof(IsWorking));
        OnPropertyChanged(nameof(CanCancel));
        OnPropertyChanged(nameof(CanScan));
        OnPropertyChanged(nameof(CanReplace));
        OnPropertyChanged(nameof(IsIndividualSelectionEnabled));
    }
    partial void OnSourceErrorChanged(string? value) => OnPropertyChanged(nameof(HasSourceError));
    partial void OnStatusTextChanged(string value) => OnPropertyChanged(nameof(HasStatusText));

    // Split from the previous single ReplaceAsync so the caller (the page) can
    // interject a version-family confirmation and/or a multiple-proxy-DLL
    // notice between preparation and execution. IsBusy is set here and stays
    // set across both the dialog(s) shown in between and ExecuteReplacementAsync
    // itself; either AbortPreparedReplacement or ExecuteReplacementAsync must be
    // called afterward to release it and the cancellation source.
    public async Task<OptiScalerUpdatePreparation?> PrepareReplacementAsync(CancellationToken cancellationToken = default)
    {
        if (!CanReplace || Source is null) return null;
        IsBusy = true; _operationCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        try
        {
            StatusText = "Preparing replacement...";
            // ApplyBulkSelection already applies the 0.9/0.10/All filter to
            // IsSelected, so reading it back here (rather than re-deriving the
            // target set from the full catalog in bulk mode) is what keeps a
            // version-only bulk selection from also sweeping in installations
            // the user never selected.
            var requested = Items.Where(x => x.IsSelected).Select(x => x.DirectoryIdentity)
                .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();

            // Check every requested folder for multiple identified OptiScaler
            // proxy DLLs directly, before rescanning. The installation scanner
            // treats differing proxy versions in the same folder as a conflict
            // and excludes the installation entirely, which would otherwise
            // hide a multi-DLL folder behind a generic "no longer valid"
            // result instead of the specific manual-cleanup guidance.
            var displayNameByDirectory = AppServices.Installations.Installations.ToDictionary(x => x.InstallDirectory, x => x.GameDisplayName, StringComparer.OrdinalIgnoreCase);
            var multiProxyTargets = new List<OptiScalerReplacementResult>();
            var singleProxyDirectories = new List<string>();
            foreach (var directory in requested)
            {
                _operationCancellation.Token.ThrowIfCancellationRequested();
                var detected = AppServices.OptiScalerReplacement.DetectProxyBinaries(directory);
                if (detected.Count > 1)
                {
                    var names = string.Join(", ", detected);
                    multiProxyTargets.Add(new(directory, displayNameByDirectory.GetValueOrDefault(directory), names, null, null, OptiScalerReplacementStatus.Skipped, OptiScalerReplacementReason.MultipleOptiScalerBinaries, $"Multiple OptiScaler DLLs were detected: {names}. Remove the unnecessary DLLs manually and scan again.", DetectedBinaryNames: detected));
                }
                else singleProxyDirectories.Add(directory);
            }

            var plan = await BuildPlanAsync(singleProxyDirectories, displayNameByDirectory, _operationCancellation.Token);
            var classification = await AppServices.OptiScalerReplacement.ClassifyTargetsAsync(Source, plan.Targets.Select(x => x.Installation).ToArray(), _operationCancellation.Token);
            var finalPlan = new OptiScalerReplacementPlan(Source, classification.ReadyTargets, [.. plan.SkippedTargets, .. classification.MultiProxyTargets, .. multiProxyTargets]);
            return new(finalPlan, [.. multiProxyTargets, .. classification.MultiProxyTargets], classification.FamilyMismatchTargets);
        }
        catch (OperationCanceledException) { StatusText = "Replacement canceled."; AbortPreparedReplacement(); return null; }
        catch (Exception ex) { AppServices.Logger.Error("OptiScaler replacement preparation failed.", ex); SourceError = ex.Message; StatusText = "Replacement could not start."; AbortPreparedReplacement(); return null; }
    }

    // The user declined a warning (or the page navigated away) after a
    // successful PrepareReplacementAsync: release what it left open without
    // replacing anything.
    public void AbortPreparedReplacement(string? statusText = null)
    {
        _operationCancellation?.Dispose(); _operationCancellation = null;
        IsBusy = false;
        if (statusText is not null) StatusText = statusText;
        OnPropertiesChanged();
    }

    public async Task<IReadOnlyList<OptiScalerReplacementResult>> ExecuteReplacementAsync(OptiScalerReplacementPlan plan)
    {
        try
        {
            if (plan.Targets.Count == 0) { Results = plan.SkippedTargets; StatusText = DescribeOutcome(Results); return Results; }
            ProgressTotal = plan.Targets.Count; ProgressCurrent = 0;
            var progress = new Progress<OptiScalerReplacementProgress>(p => { ProgressCurrent = p.Completed; ProgressTotal = p.Total; StatusText = $"Replacing OptiScaler binaries... {p.Completed} of {p.Total}"; });
            Results = await AppServices.OptiScalerReplacement.ReplaceAsync(plan, progress, _operationCancellation?.Token ?? CancellationToken.None);
            var refreshDirectories = Results.Where(x => x.Status is OptiScalerReplacementStatus.Replaced or OptiScalerReplacementStatus.Failed).Select(x => x.InstallDirectory).Distinct(StringComparer.OrdinalIgnoreCase);
            await AppServices.Installations.RescanDirectoriesAsync(refreshDirectories, CancellationToken.None);
            StatusText = DescribeOutcome(Results);
            return Results;
        }
        catch (OperationCanceledException) { StatusText = "Replacement canceled."; return Results; }
        catch (Exception ex) { AppServices.Logger.Error("OptiScaler replacement failed.", ex); SourceError = ex.Message; StatusText = "Replacement could not start."; return Results; }
        finally { _operationCancellation?.Dispose(); _operationCancellation = null; IsBusy = false; OnPropertiesChanged(); }
    }

    public void Cancel() => _operationCancellation?.Cancel();
    public void CancelScan() => _scanCancellation?.Cancel();
    public void CancelCurrentOperation() { if (IsScanning) CancelScan(); else Cancel(); }

    // "Replacement completed." regardless of outcome hid full-failure and
    // full-cancellation results behind a success-sounding status message.
    // Shared with the result dialog title so both agree on the outcome.
    internal enum ReplacementOutcome { None, Completed, Canceled, Skipped, Failed, PartiallyCompleted }

    internal static ReplacementOutcome ClassifyOutcome(IReadOnlyList<OptiScalerReplacementResult> results)
    {
        if (results.Count == 0) return ReplacementOutcome.None;
        var replaced = results.Count(x => x.Status == OptiScalerReplacementStatus.Replaced);
        if (replaced == results.Count) return ReplacementOutcome.Completed;
        if (replaced == 0 && results.All(x => x.Status == OptiScalerReplacementStatus.Canceled)) return ReplacementOutcome.Canceled;
        // Targets that vanished, are busy, or are no longer identified as
        // OptiScaler are Skipped rather than Failed; when every result is one
        // of those, nothing actually went wrong, so it should not be reported
        // the same way as an unexpected replacement failure.
        if (replaced == 0 && results.All(x => x.Status == OptiScalerReplacementStatus.Skipped)) return ReplacementOutcome.Skipped;
        if (replaced == 0) return ReplacementOutcome.Failed;
        return ReplacementOutcome.PartiallyCompleted;
    }

    internal static string DescribeOutcome(IReadOnlyList<OptiScalerReplacementResult> results)
    {
        if (results.Count == 0) return "No installations were replaced.";
        var replaced = results.Count(x => x.Status == OptiScalerReplacementStatus.Replaced);
        return ClassifyOutcome(results) switch
        {
            ReplacementOutcome.Completed => $"Replacement completed. {replaced} of {results.Count} installation(s) were replaced.",
            ReplacementOutcome.Canceled => "Replacement was canceled. No installations were replaced.",
            ReplacementOutcome.Skipped => results.All(x => x.Reason == OptiScalerReplacementReason.MultipleOptiScalerBinaries)
                ? "No installations were replaced. All selected installations require manual DLL cleanup."
                : $"No installations were replaced. All {results.Count} selected installation(s) were skipped (busy or no longer valid).",
            ReplacementOutcome.Failed => $"Replacement failed. 0 of {results.Count} installation(s) were replaced.",
            _ => $"Replacement partially completed. {replaced} of {results.Count} installation(s) were replaced.",
        };
    }

    private async Task<OptiScalerReplacementPlan> BuildPlanAsync(IReadOnlyList<string> requested, IReadOnlyDictionary<string, string> displayNameByDirectory, CancellationToken token)
    {
        var refreshed = await AppServices.Installations.RescanDirectoriesAsync(requested, token);
        var valid = refreshed.Where(x => IsAllSelected || (IsV09Selected && x.SchemaFamily == OptiSchemaFamily.V09) || (IsV10Selected && x.SchemaFamily == OptiSchemaFamily.V10) || !IsBulkMode).ToArray();
        var refreshedDirectories = refreshed.Select(x => x.InstallDirectory).ToHashSet(StringComparer.OrdinalIgnoreCase);

        var skipped = new List<OptiScalerReplacementResult>();
        foreach (var directory in requested)
        {
            if (refreshedDirectories.Contains(directory)) continue;
            token.ThrowIfCancellationRequested();
            // A directory that vanished from the rescan wasn't necessarily
            // removed: the scanner also excludes an installation outright when
            // its proxy DLLs report conflicting versions, which can happen if a
            // second OptiScaler DLL landed here in the brief window between the
            // pre-rescan multi-DLL check and this rescan. Re-check rather than
            // reporting the generic "no longer valid" for what is actually a
            // multi-DLL folder.
            var detected = AppServices.OptiScalerReplacement.DetectProxyBinaries(directory);
            if (detected.Count > 1)
            {
                var names = string.Join(", ", detected);
                skipped.Add(new(directory, displayNameByDirectory.GetValueOrDefault(directory), names, null, null, OptiScalerReplacementStatus.Skipped, OptiScalerReplacementReason.MultipleOptiScalerBinaries, $"Multiple OptiScaler DLLs were detected: {names}. Remove the unnecessary DLLs manually and scan again.", DetectedBinaryNames: detected));
            }
            else
            {
                skipped.Add(new(directory, displayNameByDirectory.GetValueOrDefault(directory), "Unknown", null, null, OptiScalerReplacementStatus.Skipped, OptiScalerReplacementReason.TargetMissing, "The installation is no longer valid."));
            }
        }
        return new(Source!, valid.Select(x => new OptiScalerReplacementTarget(x)).ToArray(), skipped.ToArray());
    }

    partial void OnIsAllSelectedChanged(bool value)
    {
        if (_changingBulk) return;
        _changingBulk = true; if (value) { IsV09Selected = false; IsV10Selected = false; } _changingBulk = false;
        ApplyBulkSelection();
    }
    partial void OnIsV09SelectedChanged(bool value) { if (!_changingBulk && value && IsAllSelected) { _changingBulk = true; IsAllSelected = false; _changingBulk = false; } ApplyBulkSelection(); }
    partial void OnIsV10SelectedChanged(bool value) { if (!_changingBulk && value && IsAllSelected) { _changingBulk = true; IsAllSelected = false; _changingBulk = false; } ApplyBulkSelection(); }
    partial void OnSearchTextChanged(string value) { OnPropertyChanged(nameof(FilteredItems)); RefreshRows(); }

    private void ApplyBulkSelection()
    {
        foreach (var item in Items)
        {
            item.IsSelectionEnabled = !IsBulkMode && !IsBusy && !AppServices.Installations.IsScanning;
            item.IsSelected = IsAllSelected || (IsV09Selected && item.Installation.SchemaFamily == OptiSchemaFamily.V09) || (IsV10Selected && item.Installation.SchemaFamily == OptiSchemaFamily.V10);
        }
        RefreshRows();
        OnPropertiesChanged();
    }
    private void RefreshCatalog()
    {
        IsScanning = AppServices.Installations.IsScanning;
        var existing = Items.ToDictionary(x => x.DirectoryIdentity, StringComparer.OrdinalIgnoreCase);
        foreach (var installation in AppServices.Installations.Installations)
        {
            if (existing.Remove(installation.InstallDirectory, out var item)) item.Update(installation); else AddItem(installation);
        }
        foreach (var obsolete in existing.Values) Items.Remove(obsolete);
        if (IsScanning) StatusText = "Scanning installed OptiScaler instances...";
        ApplyBulkSelection(); OnPropertiesChanged();
    }
    private bool MatchesSearch(OptiScalerUpdateItemViewModel item)
    {
        if (IsBulkMode && !IsAllSelected &&
            (!IsV09Selected || item.Installation.SchemaFamily != OptiSchemaFamily.V09) &&
            (!IsV10Selected || item.Installation.SchemaFamily != OptiSchemaFamily.V10)) return false;
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
    private void RefreshRows()
    {
        UpdateRows = FilteredItems.Chunk(2)
            .Select(items => new OptiScalerUpdateRow(items[0], items.Length > 1 ? items[1] : null))
            .ToArray();
        OnPropertyChanged(nameof(UpdateRows));
    }
    private void OnPropertiesChanged()
    {
        OnPropertyChanged(nameof(IsBulkMode)); OnPropertyChanged(nameof(IsIndividualSelectionEnabled)); OnPropertyChanged(nameof(SelectedCount)); OnPropertyChanged(nameof(CanReplace)); OnPropertyChanged(nameof(CanScan)); OnPropertyChanged(nameof(IsWorking)); OnPropertyChanged(nameof(CanCancel)); OnPropertyChanged(nameof(FilteredItems));
    }
}

public sealed record OptiScalerUpdateRow(OptiScalerUpdateItemViewModel First, OptiScalerUpdateItemViewModel? Second)
{
    public bool HasSecond => Second is not null;
}

public sealed record OptiScalerUpdatePreparation(
    OptiScalerReplacementPlan Plan,
    IReadOnlyList<OptiScalerReplacementResult> MultiProxyTargets,
    IReadOnlyList<OptiScalerReplacementTarget> FamilyMismatchTargets);
