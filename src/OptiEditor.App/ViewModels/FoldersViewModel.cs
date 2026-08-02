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

    // Serializes every save attempt (toggle, add, remove) so overlapping saves
    // can never interleave: each queued attempt snapshots and persists the
    // *current* Roots state once it actually runs, and a failure always rolls
    // the UI back to the exact last state that is known to be on disk rather
    // than a per-toggle guess, which could otherwise diverge from disk when
    // several toggles fail in quick succession.
    private readonly SemaphoreSlim _saveGate = new(1, 1);
    private IReadOnlyList<ScanRoot> _persisted = [];
    private bool _suppressAutoSave;

    public async Task LoadAsync()
    {
        var roots = await AppServices.ScanRoots.LoadAsync();
        _persisted = roots;
        Roots.Clear();
        foreach (var root in roots) AddRoot(root.Path, root.IsEnabled);
    }
    public async Task AddAsync(string path)
    {
        var normalized = System.IO.Path.GetFullPath(path);
        if (Roots.Any(x => string.Equals(x.Path, normalized, StringComparison.OrdinalIgnoreCase))) return;
        AddRoot(normalized, true);
        await TrySaveAsync();
    }
    public async Task RemoveAsync(ScanRootItemViewModel item)
    {
        if (!Roots.Contains(item)) return;
        Roots.Remove(item);
        await TrySaveAsync();
    }
    public Task SaveAsync() => AppServices.ScanRoots.SaveAsync(Roots.Select(x => x.ToModel()));

    private async Task<bool> TrySaveAsync()
    {
        await _saveGate.WaitAsync();
        try
        {
            var snapshot = Roots.Select(x => x.ToModel()).ToArray();
            try { await AppServices.ScanRoots.SaveAsync(snapshot); _persisted = snapshot; return true; }
            catch (Exception ex)
            {
                AppServices.Logger.Error("Scan folder changes could not be saved.", ex);
                StatusText = "Folder changes could not be saved. See logs for details.";
                RestorePersistedState();
                return false;
            }
        }
        finally { _saveGate.Release(); }
    }

    // Reconciles Roots back to the last state known to be on disk: restores
    // IsEnabled on surviving items, re-adds an item a failed Remove took out
    // of the UI, and drops an item a failed Add put in without ever saving it.
    private void RestorePersistedState()
    {
        _suppressAutoSave = true;
        try
        {
            var persistedByPath = _persisted.ToDictionary(x => x.Path, StringComparer.OrdinalIgnoreCase);
            for (var i = Roots.Count - 1; i >= 0; i--)
                if (!persistedByPath.ContainsKey(Roots[i].Path)) Roots.RemoveAt(i);
            foreach (var root in _persisted)
            {
                var existing = Roots.FirstOrDefault(x => string.Equals(x.Path, root.Path, StringComparison.OrdinalIgnoreCase));
                if (existing is not null) existing.IsEnabled = root.IsEnabled;
                else AddRoot(root.Path, root.IsEnabled);
            }
        }
        finally { _suppressAutoSave = false; }
    }

    private ScanRootItemViewModel AddRoot(string path, bool isEnabled)
    {
        var item = new ScanRootItemViewModel(path, isEnabled);
        item.PropertyChanged += async (_, args) =>
        {
            if (_suppressAutoSave || args.PropertyName != nameof(ScanRootItemViewModel.IsEnabled)) return;
            await TrySaveAsync();
        };
        Roots.Add(item);
        return item;
    }
}
