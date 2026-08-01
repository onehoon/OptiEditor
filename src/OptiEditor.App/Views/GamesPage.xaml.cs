using Microsoft.UI.Xaml.Controls;
using OptiEditor.App.ViewModels;
using Windows.ApplicationModel.DataTransfer;

namespace OptiEditor.App.Views;
public sealed partial class GamesPage : Page
{
    public GamesViewModel ViewModel { get; } = new();
    public GamesPage() { InitializeComponent(); }
    protected override void OnNavigatedFrom(Microsoft.UI.Xaml.Navigation.NavigationEventArgs e) { ViewModel.Dispose(); base.OnNavigatedFrom(e); }
    private async void Rescan_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e) => await ViewModel.ScanAsync();
    private void Cancel_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e) => ViewModel.Cancel();
    private void OpenFolder_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e) { if ((sender as Button)?.Tag is string path) System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("explorer.exe", path) { UseShellExecute = true }); }
    private void CopyPath_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e) { if ((sender as Button)?.Tag is string path) { var package = new DataPackage(); package.SetText(path); Clipboard.SetContent(package); } }
    private void EditSettings_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e) { if ((sender as Button)?.Tag is OptiEditor.Core.Models.OptiInstallation installation) Frame.Navigate(typeof(EditorPage), installation); }
}
