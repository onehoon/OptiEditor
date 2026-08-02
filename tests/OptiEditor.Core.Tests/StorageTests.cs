using OptiEditor.Core.Models;
using OptiEditor.Core.Storage;

namespace OptiEditor.Core.Tests;

public sealed class StorageTests
{
    private static string CreateFolder()
    {
        var folder = Path.Combine(Path.GetTempPath(), "OptiEditorTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        return folder;
    }

    [Fact]
    public async Task Scan_root_store_round_trips_and_leaves_no_temp_files()
    {
        var folder = CreateFolder();
        try
        {
            var store = new ScanRootStore(folder);
            await store.SaveAsync([new ScanRoot { Path = "C:\\Games", IsEnabled = true }, new ScanRoot { Path = "C:\\More", IsEnabled = false }]);
            var loaded = await store.LoadAsync();
            Assert.Equal(2, loaded.Count);
            Assert.Contains(loaded, x => x.Path == "C:\\Games" && x.IsEnabled);
            Assert.Contains(loaded, x => x.Path == "C:\\More" && !x.IsEnabled);
            Assert.Empty(Directory.EnumerateFiles(folder, "*.tmp"));
        }
        finally { Directory.Delete(folder, true); }
    }

    [Fact]
    public async Task Scan_root_store_survives_concurrent_saves_without_corrupting_the_file()
    {
        var folder = CreateFolder();
        try
        {
            var store = new ScanRootStore(folder);
            var tasks = Enumerable.Range(0, 20).Select(i => store.SaveAsync([new ScanRoot { Path = $"C:\\Game{i}", IsEnabled = true }]));
            await Task.WhenAll(tasks);
            var loaded = await store.LoadAsync();
            Assert.Single(loaded);
        }
        finally { Directory.Delete(folder, true); }
    }

    [Fact]
    public async Task Scan_root_store_recovers_from_corrupt_json_instead_of_throwing()
    {
        var folder = CreateFolder();
        try
        {
            var path = Path.Combine(folder, "scan-roots.json");
            await File.WriteAllTextAsync(path, "{ not valid json");
            var store = new ScanRootStore(folder);
            var loaded = await store.LoadAsync();
            Assert.Empty(loaded);
            Assert.False(File.Exists(path));
            Assert.Single(Directory.EnumerateFiles(folder, "scan-roots.invalid-*.json"));
        }
        finally { Directory.Delete(folder, true); }
    }

    [Fact]
    public async Task Startup_tab_store_load_and_save_do_not_race()
    {
        var folder = CreateFolder();
        try
        {
            var path = Path.Combine(folder, "startup-tab.json");
            await File.WriteAllTextAsync(path, "not json");
            var store = new StartupTabStore(folder);
            var loads = Enumerable.Range(0, 25).Select(_ => store.LoadAsync());
            var saves = Enumerable.Range(0, 25).Select(_ => store.SaveAsync(StartupTabs.OptiScalerUpdate));
            await Task.WhenAll(loads.Concat(saves));
            Assert.Equal(StartupTabs.OptiScalerUpdate, await store.LoadAsync());
        }
        finally { Directory.Delete(folder, true); }
    }

    [Fact]
    public async Task Startup_tab_store_recovers_from_corrupt_json_instead_of_throwing()
    {
        var folder = CreateFolder();
        try
        {
            var path = Path.Combine(folder, "startup-tab.json");
            await File.WriteAllTextAsync(path, "not json");
            var store = new StartupTabStore(folder);
            Assert.Equal(StartupTabs.Games, await store.LoadAsync());
            Assert.False(File.Exists(path));
        }
        finally { Directory.Delete(folder, true); }
    }

    [Fact]
    public async Task Editor_visibility_store_recovers_from_corrupt_json_instead_of_throwing()
    {
        var folder = CreateFolder();
        try
        {
            var path = Path.Combine(folder, "editor-visibility.json");
            await File.WriteAllTextAsync(path, "[ this is not json");
            var store = new EditorVisibilityStore(folder);
            Assert.Empty(await store.LoadAsync());
        }
        finally { Directory.Delete(folder, true); }
    }

    [Fact]
    public async Task Source_comment_visibility_store_recovers_from_corrupt_json_instead_of_throwing()
    {
        var folder = CreateFolder();
        try
        {
            var path = Path.Combine(folder, "source-comment-visibility.json");
            await File.WriteAllTextAsync(path, "not-a-bool");
            var store = new SourceCommentVisibilityStore(folder);
            Assert.True(await store.LoadAsync());
        }
        finally { Directory.Delete(folder, true); }
    }
}
