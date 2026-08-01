using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using OptiEditor.App.ViewModels;
using OptiEditor.Core.Models;

namespace OptiEditor.App.Views;

public sealed partial class SettingsPage : Page
{
    public EditorVisibilitySettingsViewModel ViewModel { get; } = new();
    public SettingsPage() { InitializeComponent(); Loaded += async (_, _) => { await ViewModel.LoadAsync(OptiSchemaFamily.V10); Render(); }; }
    private async void Family_Click(object sender, RoutedEventArgs e) { await ViewModel.LoadAsync((sender as Button)?.Tag is "V09" ? OptiSchemaFamily.V09 : OptiSchemaFamily.V10); Render(); }
    private async void Save_Click(object sender, RoutedEventArgs e) => await ViewModel.SaveAsync();
    private async void Reset_Click(object sender, RoutedEventArgs e) { await ViewModel.ResetAsync(); Render(); }
    private void Render()
    {
        SettingsPanel.Children.Clear(); string? group = null;
        foreach (var item in ViewModel.Settings)
        {
            if (!string.Equals(group, item.GroupName, StringComparison.Ordinal)) { group = item.GroupName; SettingsPanel.Children.Add(new TextBlock { Text = group, Style = (Style)Application.Current.Resources["SubtitleTextBlockStyle"], Margin = new Thickness(0, 12, 0, 2) }); }
            var toggle = new ToggleSwitch { Header = item.DisplayName, IsOn = item.IsVisible, Tag = item, OnContent = "Shown", OffContent = "Hidden" };
            toggle.Toggled += (_, _) => { if (toggle.Tag is EditorVisibilitySettingItemViewModel current) { current.IsVisible = toggle.IsOn; current.IsOverridden = current.IsVisible != current.DefaultVisible; } };
            var row = new StackPanel { Spacing = 2 }; row.Children.Add(toggle); if (!string.IsNullOrWhiteSpace(item.Description)) row.Children.Add(new TextBlock { Text = item.Description, TextWrapping = TextWrapping.Wrap }); row.Children.Add(new TextBlock { Text = item.DefaultVisible ? "App default: shown" : "App default: hidden", Opacity = .65 }); SettingsPanel.Children.Add(new Border { Child = row, Padding = new Thickness(10), BorderBrush = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"], BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(6) });
        }
    }
}
