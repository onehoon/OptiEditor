using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using OptiEditor.App.Controls;
using OptiEditor.App.Services;
using OptiEditor.App.ViewModels;
using OptiEditor.Core.Models;
using OptiEditor.Core.Presets;

namespace OptiEditor.App.Views;

public sealed partial class EditorPage : Page
{
    private bool _showSourceComments;
    private readonly List<PresetDefinition> _selectedPresets = [];
    private readonly Dictionary<EditorSettingItemViewModel, string> _presetRestoreValues = [];
    public EditorViewModel ViewModel { get; } = new();
    public EditorPage() => InitializeComponent();

    protected override async void OnNavigatedTo(Microsoft.UI.Xaml.Navigation.NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        if (e.Parameter is OptiInstallation installation)
        {
            _showSourceComments = await Services.AppServices.SourceComments.LoadAsync();
            await ViewModel.LoadAsync(installation);
            _selectedPresets.Clear();
            _presetRestoreValues.Clear();
            await RenderPresetButtonsAsync();
            RenderSettings();
        }
    }

    private async void Save_Click(object sender, RoutedEventArgs e)
    {
        if (!ViewModel.CanSave) return;
        var changes = BuildSaveReviewLines();
        if (await SaveReviewDialog.ConfirmAsync(XamlRoot, "Review changes", "The following values will be written to OptiScaler.ini.", changes))
        {
            if (await ViewModel.SaveAsync())
            {
                if (!ViewModel.IsDirty) ClearPresetSelection();
                var applied = new TextBox { Text = string.Join(Environment.NewLine, changes), IsReadOnly = true, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MinWidth = 520, MinHeight = 120, MaxHeight = 420 };
                await new ContentDialog
                {
                    XamlRoot = XamlRoot,
                    Title = "Settings applied",
                    Content = new ScrollViewer { Content = applied, MaxHeight = 420 },
                    CloseButtonText = "OK",
                    DefaultButton = ContentDialogButton.Close
                }.ShowAsync();
            }
        }
    }

    private IReadOnlyList<string> BuildSaveReviewLines()
    {
        var settings = ViewModel.Settings.ToDictionary(x => x.Binding.Definition.Id, StringComparer.OrdinalIgnoreCase);
        var presetValues = new Dictionary<string, (string Key, string Section, string Value)>(StringComparer.OrdinalIgnoreCase);
        foreach (var preset in _selectedPresets)
            foreach (var entry in preset.Entries)
                if (settings.TryGetValue(entry.SettingId, out var item))
                    presetValues[entry.SettingId] = (item.Binding.Definition.IniKey.Name, item.Binding.Definition.IniKey.Section, entry.RawValue);

        var lines = new List<string>();
        foreach (var pair in presetValues)
        {
            var item = settings[pair.Key];
            var status = item.IsModified ? "will change" : "already applied";
            lines.Add($"[{pair.Value.Section}] {pair.Value.Key} = {item.CurrentRawValue} ({status})");
        }

        var presetIds = presetValues.Keys.ToHashSet(StringComparer.OrdinalIgnoreCase);
        lines.AddRange(ViewModel.Settings
            .Where(item => item.IsModified && !presetIds.Contains(item.Binding.Definition.Id))
            .Select(item => $"[{item.Binding.Definition.IniKey.Section}] {item.Binding.Definition.IniKey.Name} = {item.CurrentRawValue} (will change)"));
        return lines;
    }

    private bool IsControlledBySelectedPreset(EditorSettingItemViewModel item) =>
        _selectedPresets.Any(x => x.Entries.Any(e => string.Equals(e.SettingId, item.Binding.Definition.Id, StringComparison.OrdinalIgnoreCase)));

    private void RevertAll_Click(object sender, RoutedEventArgs e) { ViewModel.RevertAll(); ClearPresetSelection(); RenderSettings(); }
    private void ResetAll_Click(object sender, RoutedEventArgs e) { ViewModel.ResetAllManagedToAuto(); ClearPresetSelection(); RenderSettings(); }
    private async void Reload_Click(object sender, RoutedEventArgs e) { if (ViewModel.Installation is { } installation) { await ViewModel.LoadAsync(installation); ClearPresetSelection(); await RenderPresetButtonsAsync(); RenderSettings(); } }
    private void Revert_Click(object sender, RoutedEventArgs e) { if ((sender as Button)?.Tag is EditorSettingItemViewModel item) { item.Revert(); ViewModel.Update(item); if (IsControlledBySelectedPreset(item)) ReapplySelectedPresets(); else RenderSettings(); } }
    private void SettingSearchBox_TextChanged(object sender, TextChangedEventArgs e) => RenderSettings();

    private async Task RenderPresetButtonsAsync()
    {
        PresetButtonsPanel.Children.Clear();
        if (ViewModel.Installation is null) { PresetButtonsPanel.Visibility = Visibility.Collapsed; return; }
        var presets = (await AppServices.LoadPresetsAsync())
            .Where(preset => preset.Family == ViewModel.Installation.SchemaFamily)
            .OrderBy(preset => preset.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        foreach (var preset in presets)
        {
            var button = new Button { Content = preset.Name, Tag = preset };
            button.Click += PresetButton_Click;
            PresetButtonsPanel.Children.Add(button);
        }
        UpdatePresetButtonStyles();
        PresetButtonsPanel.Visibility = presets.Length == 0 ? Visibility.Collapsed : Visibility.Visible;
    }

    private void PresetButton_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.Tag is not PresetDefinition preset) return;
        if (_selectedPresets.Any(x => x.Id == preset.Id))
        {
            _selectedPresets.RemoveAll(x => x.Id == preset.Id);
            ReapplySelectedPresets();
            return;
        }
        _selectedPresets.Add(preset);
        var skipped = ReapplySelectedPresets();
        // Entries can be skipped when their setting is hidden by the current
        // Editor visibility preferences; the preset button still looks fully
        // selected, so surface how many entries were not applied.
        if (skipped > 0) ViewModel.StatusText = $"Preset applied. {skipped} of {preset.Entries.Count} setting(s) were skipped because they are hidden in Editor visibility.";
    }

    private int ReapplySelectedPresets()
    {
        foreach (var (item, value) in _presetRestoreValues) { item.CurrentRawValue = value; ViewModel.Update(item); }
        var settingsById = ViewModel.Settings.ToDictionary(item => item.Binding.Definition.Id, StringComparer.OrdinalIgnoreCase);
        var skipped = 0;
        var controlledItems = new HashSet<EditorSettingItemViewModel>();
        foreach (var preset in _selectedPresets)
            foreach (var entry in preset.Entries)
            {
                if (!settingsById.TryGetValue(entry.SettingId, out var item)) { skipped++; continue; }
                controlledItems.Add(item);
                if (!_presetRestoreValues.ContainsKey(item)) _presetRestoreValues[item] = item.CurrentRawValue;
                item.CurrentRawValue = entry.RawValue;
                ViewModel.Update(item);
            }
        foreach (var staleItem in _presetRestoreValues.Keys.Where(item => !controlledItems.Contains(item)).ToArray()) _presetRestoreValues.Remove(staleItem);
        UpdatePresetButtonStyles();
        RenderSettings();
        return skipped;
    }

    private void ClearPresetSelection()
    {
        _selectedPresets.Clear();
        _presetRestoreValues.Clear();
        UpdatePresetButtonStyles();
    }

    private void UpdatePresetButtonStyles()
    {
        var accentStyle = (Style)Application.Current.Resources["AccentButtonStyle"];
        foreach (var button in PresetButtonsPanel.Children.OfType<Button>()) button.Style = button.Tag is PresetDefinition preset && _selectedPresets.Any(x => x.Id == preset.Id) ? accentStyle : null;
    }

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
                var grid = new Grid { ColumnSpacing = 12 };
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                var label = new StackPanel { Spacing = 3 };
                label.Children.Add(new TextBlock { Text = item.DisplayName, Style = (Style)Application.Current.Resources["BodyStrongTextBlockStyle"] });
                if (!string.IsNullOrWhiteSpace(item.Description)) label.Children.Add(new TextBlock { Text = item.Description, TextWrapping = TextWrapping.Wrap });
                if (_showSourceComments && !string.IsNullOrWhiteSpace(item.Binding.Definition.SourceComment)) label.Children.Add(new TextBlock { Text = item.Binding.Definition.SourceComment, TextWrapping = TextWrapping.Wrap, Opacity = .7 });
                var warning = new TextBlock();
                warning.SetBinding(TextBlock.TextProperty, new Binding { Source = item, Path = new PropertyPath(nameof(EditorSettingItemViewModel.UnknownValueWarningMessage)), Mode = BindingMode.OneWay });
                label.Children.Add(warning);
                grid.Children.Add(label);
                var input = SettingValueControlFactory.Create(item.Binding.Definition, item.CurrentRawValue, value => { item.CurrentRawValue = value; ViewModel.Update(item); if (_selectedPresets.Any(x => x.Entries.Any(e => string.Equals(e.SettingId, item.Binding.Definition.Id, StringComparison.OrdinalIgnoreCase)))) ReapplySelectedPresets(); }, item.InputHint);
                Grid.SetColumn(input, 1);
                grid.Children.Add(input);
                var actions = new StackPanel { Spacing = 4 };
                actions.Children.Add(new Button { Content = "Revert", Tag = item });
                var validation = new TextBlock { Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.IndianRed), TextWrapping = TextWrapping.Wrap, MaxWidth = 180 };
                validation.SetBinding(TextBlock.TextProperty, new Binding { Source = item, Path = new PropertyPath(nameof(EditorSettingItemViewModel.BlockingValidationMessage)), Mode = BindingMode.OneWay });
                actions.Children.Add(validation);
                Grid.SetColumn(actions, 2);
                grid.Children.Add(actions);
                ((Button)actions.Children[0]).Click += Revert_Click;
                rows.Children.Add(grid);
            }
            SettingsPanel.Children.Add(new CollapsibleSectionCard(group.First().GroupDisplayName ?? group.Key, rows, !string.IsNullOrEmpty(search)));
        }
    }
}
