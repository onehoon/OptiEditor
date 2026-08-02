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
        Roots.Clear(); foreach (var root in await AppServices.ScanRoots.LoadAsync()) AddRoot(root.Path, root.IsEnabled);
    }
    public async Task AddAsync(string path)
    {
        var normalized = System.IO.Path.GetFullPath(path);
        if (Roots.Any(x => string.Equals(x.Path, normalized, StringComparison.OrdinalIgnoreCase))) return;
        var item = AddRoot(normalized, true);
        if (!await TrySaveAsync()) Roots.Remove(item);
    }
    public async Task RemoveAsync(ScanRootItemViewModel item)
    {
        var index = Roots.IndexOf(item);
        if (index < 0) return;
        Roots.RemoveAt(index);
        if (!await TrySaveAsync()) Roots.Insert(index, item);
    }
    public Task SaveAsync() => AppServices.ScanRoots.SaveAsync(Roots.Select(x => x.ToModel()));

    private async Task<bool> TrySaveAsync()
    {
        try { await SaveAsync(); return true; }
        catch (Exception ex) { AppServices.Logger.Error("Scan folder changes could not be saved.", ex); StatusText = "Folder changes could not be saved. See logs for details."; return false; }
    }

    private ScanRootItemViewModel AddRoot(string path, bool isEnabled)
    {
        var suppress = false;
        var item = new ScanRootItemViewModel(path, isEnabled);
        item.PropertyChanged += async (sender, args) =>
        {
            if (suppress || args.PropertyName != nameof(ScanRootItemViewModel.IsEnabled)) return;
            var changed = (ScanRootItemViewModel)sender!;
            var previous = !changed.IsEnabled;
            if (await TrySaveAsync()) return;
            // A failed save must not leave the toggle showing a state that
            // was never persisted; revert it without re-triggering a save.
            suppress = true;
            try { changed.IsEnabled = previous; } finally { suppress = false; }
        };
        Roots.Add(item);
        return item;
    }
}
