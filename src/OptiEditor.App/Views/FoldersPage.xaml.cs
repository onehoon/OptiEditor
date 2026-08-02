using Microsoft.UI.Xaml.Controls;
using OptiEditor.App.ViewModels;
using WinRT.Interop;
using Windows.Storage.Pickers;

namespace OptiEditor.App.Views;
public sealed partial class FoldersPage : Page
{
    public FoldersViewModel ViewModel { get; } = new();
    public FoldersPage() { InitializeComponent(); Loaded += async (_, _) => await ViewModel.LoadAsync(); }
    private async void AddFolder_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        var picker = new FolderPicker(); picker.FileTypeFilter.Add("*");
        InitializeWithWindow.Initialize(picker, App.WindowHandle);
        var folder = await picker.PickSingleFolderAsync();
        if (folder is not null) await ViewModel.AddAsync(folder.Path);
    }
    private async void Remove_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    { if ((sender as Button)?.Tag is ScanRootItemViewModel item) await ViewModel.RemoveAsync(item); }
}
