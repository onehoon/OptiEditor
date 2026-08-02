using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Dispatching;
using OptiEditor.App.Services;
using OptiEditor.Core.Models;

namespace OptiEditor.App.ViewModels;

public partial class GamesViewModel : ObservableObject, IDisposable
{
    public ObservableCollection<OptiInstallation> Installations { get; } = [];
    public IReadOnlyList<GameRow> GameRows { get; private set; } = [];
    private readonly List<OptiInstallation> _allInstallations = [];
    [ObservableProperty] public partial bool IsScanning { get; set; }
    [ObservableProperty] public partial string SearchText { get; set; } = "";
    [ObservableProperty] public partial string StatusText { get; set; } = "Ready to scan.";
    [ObservableProperty] public partial string EmptyMessage { get; set; } = "No OptiScaler installations were found.";
    [ObservableProperty] public partial DateTimeOffset? LastScanTime { get; set; }
    [ObservableProperty] public partial string LastScanText { get; set; } = "Last scan: never";
    [ObservableProperty] public partial string InstallationCountText { get; set; } = "0 installations";
    private CancellationTokenSource? _cancellation;
    private readonly DispatcherQueue? _dispatcher = DispatcherQueue.GetForCurrentThread();
    public GamesViewModel()
    {
        AppServices.Installations.InstallationsChanged += OnInstallationsChanged;
        UpdateFromCatalog();
    }
    public void Dispose() => AppServices.Installations.InstallationsChanged -= OnInstallationsChanged;
    private void OnInstallationsChanged(object? sender, EventArgs args)
    {
        if (_dispatcher is null || _dispatcher.HasThreadAccess) UpdateFromCatalog();
        else _dispatcher.TryEnqueue(UpdateFromCatalog);
    }
    public async Task ScanAsync()
    {
        if (AppServices.Installations.IsScanning) return;
        IsScanning = true; StatusText = "Scanning installed OptiScaler instances..."; _cancellation = new();
        try
        {
            var result = await AppServices.Installations.ScanAllAsync(_cancellation.Token);
            UpdateFromCatalog();
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
        GameRows = results.Chunk(2).Select(items => new GameRow(items[0], items.Length > 1 ? items[1] : null)).ToArray();
        OnPropertyChanged(nameof(GameRows));
        InstallationCountText = $"{results.Count} installation{(results.Count == 1 ? "" : "s")}";
        EmptyMessage = results.Count == 0 ? "No OptiScaler installations were found." : "";
    }
    private void UpdateFromCatalog()
    {
        IsScanning = AppServices.Installations.IsScanning;
        _allInstallations.Clear(); _allInstallations.AddRange(AppServices.Installations.Installations); RefreshFilter();
        EmptyMessage = Installations.Count == 0 ? "No OptiScaler installations were found." : "";
        if (IsScanning) StatusText = "Scanning installed OptiScaler instances...";
    }
}

public sealed record GameRow(OptiInstallation First, OptiInstallation? Second)
{
    public bool HasSecond => Second is not null;
}
