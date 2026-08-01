using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml;
using OptiEditor.App.ViewModels;
using OptiEditor.App.Controls;
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
            await ViewModel.LoadAsync(request.Installation); RenderSettings();
            if (request.Preset is not null) await ReviewPresetAsync(request.Preset);
        }
        else if (e.Parameter is OptiInstallation installation) { await ViewModel.LoadAsync(installation); RenderSettings(); }
    }
    private async void Save_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e) => await ViewModel.SaveAsync();
    private void RevertAll_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e) => ViewModel.RevertAll();
    private void ResetAll_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e) => ViewModel.ResetAllManagedToAuto();
    private async void Reload_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e) { if (ViewModel.Installation is { } installation) { await ViewModel.LoadAsync(installation); RenderSettings(); } }
    private void Revert_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e) { if ((sender as Button)?.Tag is EditorSettingItemViewModel item) { item.Revert(); ViewModel.Update(item); RenderSettings(); } }
    private void RenderSettings()
    {
        SettingsPanel.Children.Clear();
        foreach (var item in ViewModel.Settings)
        {
            if (item.GroupDisplayName is not null) SettingsPanel.Children.Add(new TextBlock { Text = item.GroupDisplayName, Style = (Style)Application.Current.Resources["SubtitleTextBlockStyle"], Margin = new Thickness(0, 16, 0, 4) });
            var grid = new Grid { ColumnSpacing = 12 }; grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2, GridUnitType.Star) }); grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var label = new StackPanel { Spacing = 3 }; label.Children.Add(new TextBlock { Text = item.DisplayName, Style = (Style)Application.Current.Resources["BodyStrongTextBlockStyle"] }); if (!string.IsNullOrWhiteSpace(item.Description)) label.Children.Add(new TextBlock { Text = item.Description, TextWrapping = TextWrapping.Wrap }); if (item.IsUnknownValue) label.Children.Add(new TextBlock { Text = "Unknown value will be preserved unless changed." }); grid.Children.Add(label);
            SettingControlBase input = item.Binding.Definition.ValueKind switch { OptiEditor.Core.Schema.SettingValueKind.Boolean => new AutoBooleanSettingControl(item, ViewModel.Update), OptiEditor.Core.Schema.SettingValueKind.Enum => new EnumSettingControl(item, ViewModel.Update), OptiEditor.Core.Schema.SettingValueKind.Shortcut => new ShortcutSettingControl(item, ViewModel.Update), _ => new AutoNumberSettingControl(item, ViewModel.Update) };
            Grid.SetColumn(input, 1); grid.Children.Add(input); var actions = new StackPanel { Spacing = 4 }; actions.Children.Add(new Button { Content = "Revert", Tag = item }); if (!string.IsNullOrWhiteSpace(item.ValidationError)) actions.Children.Add(new TextBlock { Text = item.ValidationError, Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.IndianRed), TextWrapping = TextWrapping.Wrap, MaxWidth = 180 }); Grid.SetColumn(actions, 2); grid.Children.Add(actions);
            ((Button)actions.Children[0]).Click += Revert_Click; SettingsPanel.Children.Add(new Border { Padding = new Thickness(12), BorderBrush = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"], BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(6), Child = grid });
        }
    }
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
