using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using OptiEditor.App.ViewModels;
using OptiEditor.App.Controls;
using OptiEditor.Core.Models;

namespace OptiEditor.App.Views;
public sealed partial class EditorPage : Page
{
    private bool _showSourceComments;
    public EditorViewModel ViewModel { get; } = new();
    public EditorPage() => InitializeComponent();
    protected override async void OnNavigatedTo(Microsoft.UI.Xaml.Navigation.NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        if (e.Parameter is EditorNavigationRequest request)
        {
            _showSourceComments = await Services.AppServices.SourceComments.LoadAsync(); await ViewModel.LoadAsync(request.Installation); RenderSettings();
            if (request.Preset is not null) await ReviewPresetAsync(request.Preset);
        }
        else if (e.Parameter is OptiInstallation installation) { _showSourceComments = await Services.AppServices.SourceComments.LoadAsync(); await ViewModel.LoadAsync(installation); RenderSettings(); }
    }
    private async void Save_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e) => await ViewModel.SaveAsync();
    private void RevertAll_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e) { ViewModel.RevertAll(); RenderSettings(); }
    private void ResetAll_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e) { ViewModel.ResetAllManagedToAuto(); RenderSettings(); }
    private async void Reload_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e) { if (ViewModel.Installation is { } installation) { await ViewModel.LoadAsync(installation); RenderSettings(); } }
    private void Revert_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e) { if ((sender as Button)?.Tag is EditorSettingItemViewModel item) { item.Revert(); ViewModel.Update(item); RenderSettings(); } }
    private void SettingSearchBox_TextChanged(object sender, TextChangedEventArgs e) => RenderSettings();
    private void RenderSettings()
    {
        SettingsPanel.Children.Clear();
        var search = SettingSearchBox?.Text?.Trim() ?? string.Empty;
        var matchingSettings = ViewModel.Settings.Where(item => string.IsNullOrEmpty(search) || item.Binding.Definition.IniKey.Name.Contains(search, StringComparison.OrdinalIgnoreCase));
        foreach (var group in matchingSettings.GroupBy(x => x.GroupName))
        {
            var rows = new StackPanel { Spacing = 8 };
            foreach (var item in group)
            {
                var grid = new Grid { ColumnSpacing = 12 }; grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2, GridUnitType.Star) }); grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                var label = new StackPanel { Spacing = 3 }; label.Children.Add(new TextBlock { Text = item.DisplayName, Style = (Style)Application.Current.Resources["BodyStrongTextBlockStyle"] }); if (!string.IsNullOrWhiteSpace(item.Description)) label.Children.Add(new TextBlock { Text = item.Description, TextWrapping = TextWrapping.Wrap }); if (_showSourceComments && !string.IsNullOrWhiteSpace(item.Binding.Definition.SourceComment)) label.Children.Add(new TextBlock { Text = item.Binding.Definition.SourceComment, TextWrapping = TextWrapping.Wrap, Opacity = .7 }); var warning = new TextBlock(); warning.SetBinding(TextBlock.TextProperty, new Binding { Source = item, Path = new PropertyPath(nameof(EditorSettingItemViewModel.UnknownValueWarningMessage)), Mode = BindingMode.OneWay }); label.Children.Add(warning); grid.Children.Add(label);
                var input = SettingValueControlFactory.Create(item.Binding.Definition, item.CurrentRawValue, value => { item.CurrentRawValue = value; ViewModel.Update(item); }, item.InputHint);
                Grid.SetColumn(input, 1); grid.Children.Add(input); var actions = new StackPanel { Spacing = 4 }; actions.Children.Add(new Button { Content = "Revert", Tag = item }); var validation = new TextBlock { Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.IndianRed), TextWrapping = TextWrapping.Wrap, MaxWidth = 180 }; validation.SetBinding(TextBlock.TextProperty, new Binding { Source = item, Path = new PropertyPath(nameof(EditorSettingItemViewModel.BlockingValidationMessage)), Mode = BindingMode.OneWay }); actions.Children.Add(validation); Grid.SetColumn(actions, 2); grid.Children.Add(actions);
                ((Button)actions.Children[0]).Click += Revert_Click; rows.Children.Add(grid);
            }
            SettingsPanel.Children.Add(new CollapsibleSectionCard(group.First().GroupDisplayName ?? group.Key, rows, !string.IsNullOrEmpty(search)));
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
            else RenderSettings();
        }
    }
}
