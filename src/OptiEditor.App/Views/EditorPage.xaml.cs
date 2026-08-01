using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml;
using OptiEditor.App.ViewModels;
using OptiEditor.Core.Models;

namespace OptiEditor.App.Views;
public sealed partial class EditorPage : Page
{
    public EditorViewModel ViewModel { get; } = new();
    public EditorPage() => InitializeComponent();
    protected override async void OnNavigatedTo(Microsoft.UI.Xaml.Navigation.NavigationEventArgs e) { base.OnNavigatedTo(e); if (e.Parameter is OptiInstallation installation) await ViewModel.LoadAsync(installation); }
    private async void Save_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e) => await ViewModel.SaveAsync();
    private void RevertAll_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e) => ViewModel.RevertAll();
    private void ResetAll_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e) => ViewModel.ResetAllManagedToAuto();
    private async void Reload_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e) { if (ViewModel.Installation is { } installation) await ViewModel.LoadAsync(installation); }
    private void Value_TextChanged(object sender, TextChangedEventArgs e) { if ((sender as FrameworkElement)?.DataContext is EditorSettingItemViewModel item) ViewModel.Update(item); }
    private void Revert_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e) { if ((sender as Button)?.Tag is EditorSettingItemViewModel item) { item.Revert(); ViewModel.Update(item); } }
}
