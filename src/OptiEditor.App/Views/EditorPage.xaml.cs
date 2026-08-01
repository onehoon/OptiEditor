using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml;
using OptiEditor.App.ViewModels;
using OptiEditor.Core.Models;

namespace OptiEditor.App.Views;
public sealed partial class EditorPage : Page
{
    public EditorViewModel ViewModel { get; } = new();
    public EditorPage() => InitializeComponent();
    protected override async void OnNavigatedTo(Microsoft.UI.Xaml.Navigation.NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        if (e.Parameter is EditorNavigationRequest request)
        {
            await ViewModel.LoadAsync(request.Installation);
            if (request.Preset is not null) await ReviewPresetAsync(request.Preset);
        }
        else if (e.Parameter is OptiInstallation installation) await ViewModel.LoadAsync(installation);
    }
    private async void Save_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e) => await ViewModel.SaveAsync();
    private void RevertAll_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e) => ViewModel.RevertAll();
    private void ResetAll_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e) => ViewModel.ResetAllManagedToAuto();
    private async void Reload_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e) { if (ViewModel.Installation is { } installation) await ViewModel.LoadAsync(installation); }
    private void Value_TextChanged(object sender, TextChangedEventArgs e) { if ((sender as FrameworkElement)?.DataContext is EditorSettingItemViewModel item) ViewModel.Update(item); }
    private void Value_SelectionChanged(object sender, SelectionChangedEventArgs e) { if ((sender as FrameworkElement)?.DataContext is EditorSettingItemViewModel item) ViewModel.Update(item); }
    private void Revert_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e) { if ((sender as Button)?.Tag is EditorSettingItemViewModel item) { item.Revert(); ViewModel.Update(item); } }
    private async Task ReviewPresetAsync(OptiEditor.Core.Presets.PresetDefinition preset)
    {
        var preview = ViewModel.CreatePresetPreview(preset);
        if (preview is null || preview.Error is not null) { ViewModel.StatusText = preview?.Error ?? "Preset preview could not be created."; return; }
        var changes = preview.Items.Where(x => x.Status == OptiEditor.Core.Presets.PresetApplicationStatus.WillChange).ToArray();
        if (changes.Length == 0) { ViewModel.StatusText = "This preset has no changes for the selected game."; return; }
        var panel = new StackPanel { Spacing = 6 }; panel.Children.Add(new TextBlock { Text = "Choose the preset values to apply. Nothing is written until you select Save." });
        var selected = new Dictionary<string, CheckBox>(StringComparer.OrdinalIgnoreCase);
        foreach (var change in changes)
        {
            var check = new CheckBox { Content = $"{change.Entry.SettingId}: {change.CurrentValue} → {change.Entry.RawValue}", IsChecked = true };
            selected[change.Entry.SettingId] = check; panel.Children.Add(check);
        }
        var dialog = new ContentDialog { XamlRoot = XamlRoot, Title = $"Review {preset.Name}", Content = new ScrollViewer { Content = panel, MaxHeight = 520 }, PrimaryButtonText = "Apply to editor", CloseButtonText = "Cancel", DefaultButton = ContentDialogButton.Primary };
        if (await dialog.ShowAsync() == ContentDialogResult.Primary)
        {
            var ids = selected.Where(x => x.Value.IsChecked == true).Select(x => x.Key).ToArray();
            if (!ViewModel.ApplyPreset(preset, ids)) ViewModel.StatusText = "No preset values were applied.";
        }
    }
}
