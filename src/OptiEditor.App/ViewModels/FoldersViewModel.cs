using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using OptiEditor.App.Services;
using OptiEditor.Core.Models;

namespace OptiEditor.App.ViewModels;

public partial class ScanRootItemViewModel(string path, bool isEnabled) : ObservableObject
{
    public string Path { get; } = path;
    [ObservableProperty] public partial bool IsEnabled { get; set; } = isEnabled;
    public ScanRoot ToModel() => new() { Path = Path, IsEnabled = IsEnabled };
}

public partial class FoldersViewModel : ObservableObject
{
    public ObservableCollection<ScanRootItemViewModel> Roots { get; } = [];
    [ObservableProperty] public partial string StatusText { get; set; } = "Manage folders that OptiEditor scans.";
    public async Task LoadAsync()
    {
        Roots.Clear(); foreach (var root in await AppServices.ScanRoots.LoadAsync()) Roots.Add(new(root.Path, root.IsEnabled));
    }
    public async Task AddAsync(string path)
    {
        var normalized = System.IO.Path.GetFullPath(path);
        if (Roots.Any(x => string.Equals(x.Path, normalized, StringComparison.OrdinalIgnoreCase))) return;
        Roots.Add(new(normalized, true)); await SaveAsync();
    }
    public async Task RemoveAsync(ScanRootItemViewModel item) { Roots.Remove(item); await SaveAsync(); }
    public Task SaveAsync() => AppServices.ScanRoots.SaveAsync(Roots.Select(x => x.ToModel()));
}
