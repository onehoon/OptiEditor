using Microsoft.UI.Xaml.Controls;
using OptiEditor.App.ViewModels;
using OptiEditor.Core.Presets;
namespace OptiEditor.App.Views;
public sealed partial class PresetsPage : Page { public PresetsViewModel ViewModel { get; } = new(); public PresetsPage() { InitializeComponent(); Loaded += async (_, _) => await ViewModel.LoadAsync(); } private async void Duplicate_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e) { if ((sender as Button)?.Tag is PresetDefinition preset) await ViewModel.DuplicateAsync(preset); } private async void Delete_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e) { if ((sender as Button)?.Tag is PresetDefinition preset) await ViewModel.DeleteAsync(preset); } private async void Create_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e) => await ViewModel.CreateAsync(); }
