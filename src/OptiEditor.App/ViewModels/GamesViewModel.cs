using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using OptiEditor.App.Services;
using OptiEditor.Core.Models;

namespace OptiEditor.App.ViewModels;

public partial class GamesViewModel : ObservableObject
{
    public ObservableCollection<OptiInstallation> Installations { get; } = [];
    private readonly List<OptiInstallation> _allInstallations = [];
    [ObservableProperty] public partial bool IsScanning { get; set; }
    [ObservableProperty] public partial string SearchText { get; set; } = "";
    [ObservableProperty] public partial string StatusText { get; set; } = "Ready to scan.";
    [ObservableProperty] public partial string EmptyMessage { get; set; } = "No OptiScaler installations were found.";
    [ObservableProperty] public partial DateTimeOffset? LastScanTime { get; set; }
    [ObservableProperty] public partial string LastScanText { get; set; } = "Last scan: never";
    [ObservableProperty] public partial string InstallationCountText { get; set; } = "0 installations";
    private CancellationTokenSource? _cancellation;
    public async Task ScanAsync()
    {
        if (IsScanning) return;
        IsScanning = true; StatusText = "Scanning..."; _cancellation = new();
        try
        {
            var roots = await AppServices.ScanRoots.LoadAsync(_cancellation.Token);
            var result = await AppServices.Scanner.ScanAsync(roots, _cancellation.Token);
            _allInstallations.Clear(); _allInstallations.AddRange(result.Installations); RefreshFilter();
            EmptyMessage = Installations.Count == 0 ? "No OptiScaler installations were found." : "";
            LastScanTime = DateTimeOffset.Now;
            LastScanText = $"Last scan: {LastScanTime.Value.LocalDateTime:G}";
            StatusText = $"Scan complete: {result.Summary.ValidInstallations} installation(s) found.";
        }
        catch (OperationCanceledException) { StatusText = "Scan cancelled."; }
        catch (Exception ex) { AppServices.Logger.Error("Unexpected scan error.", ex); StatusText = "Scan completed with an unexpected error. See logs for details."; }
        finally { _cancellation?.Dispose(); _cancellation = null; IsScanning = false; }
    }
    public void Cancel() => _cancellation?.Cancel();
    partial void OnSearchTextChanged(string value) => RefreshFilter();
    private void RefreshFilter()
    {
        var search = SearchText.Trim();
        var results = _allInstallations.Where(x => string.IsNullOrEmpty(search)
            || x.GameDisplayName.Contains(search, StringComparison.OrdinalIgnoreCase)
            || x.InstallDirectory.Contains(search, StringComparison.OrdinalIgnoreCase)
            || x.GameExeName?.Contains(search, StringComparison.OrdinalIgnoreCase) == true).ToList();
        Installations.Clear(); foreach (var installation in results) Installations.Add(installation);
        InstallationCountText = $"{results.Count} installation{(results.Count == 1 ? "" : "s")}";
        EmptyMessage = results.Count == 0 ? "No OptiScaler installations were found." : "";
    }
}
