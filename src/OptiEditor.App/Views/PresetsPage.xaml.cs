using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using OptiEditor.App.Controls;
using OptiEditor.App.Services;
using OptiEditor.App.ViewModels;
using OptiEditor.Core.Models;
using OptiEditor.Core.Presets;
using OptiEditor.Core.Schema;
using OptiEditor.Core.Storage;

namespace OptiEditor.App.Views;

public sealed partial class PresetsPage : Page
{
    private PresetDefinition? _editingPreset;
    private OptiSchemaFamily _editingFamily;
    private bool _showSourceComments;
    private List<PresetEntryEditor> _entryEditors = [];

    public PresetsViewModel ViewModel { get; } = new();
    public PresetsPage() { InitializeComponent(); Loaded += async (_, _) => await ViewModel.LoadAsync(); }

    private async void Duplicate_Click(object sender, RoutedEventArgs e) { if ((sender as Button)?.Tag is PresetDefinition preset) await ViewModel.DuplicateAsync(preset); }
    private async void Delete_Click(object sender, RoutedEventArgs e) { if ((sender as Button)?.Tag is PresetDefinition preset) { if (preset.Source != PresetSource.User) { ViewModel.StatusText = "Built-in presets cannot be deleted."; return; } await ViewModel.DeleteAsync(preset); } }
    private async void Create_Click(object sender, RoutedEventArgs e) => await OpenPresetEditorAsync(null, (sender as Button)?.Tag is "V09" ? OptiSchemaFamily.V09 : OptiSchemaFamily.V10);
    private async void Edit_Click(object sender, RoutedEventArgs e) { if ((sender as Button)?.Tag is PresetDefinition preset) { if (preset.Source != PresetSource.User) { ViewModel.StatusText = "Built-in presets are read-only. Duplicate one to customize it."; return; } await OpenPresetEditorAsync(preset); } }

    private async void Apply_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.Tag is not PresetDefinition preset) return;
        var candidates = AppServices.Installations.Installations.Where(x => x.SchemaFamily == preset.Family).ToArray();
        if (candidates.Length == 0) { ViewModel.StatusText = "Scan and select a matching OptiScaler installation first."; return; }
        var list = new ListView { ItemsSource = candidates, DisplayMemberPath = nameof(OptiInstallation.GameDisplayName), SelectionMode = ListViewSelectionMode.Single, SelectedIndex = 0, MinWidth = 420 };
        var dialog = new ContentDialog { XamlRoot = XamlRoot, Title = "Apply preset to game", Content = list, PrimaryButtonText = "Review changes", CloseButtonText = "Cancel", DefaultButton = ContentDialogButton.Primary };
        if (await dialog.ShowAsync() == ContentDialogResult.Primary && list.SelectedItem is OptiInstallation installation) Frame.Navigate(typeof(EditorPage), new EditorNavigationRequest(installation, preset));
    }

    private async Task OpenPresetEditorAsync(PresetDefinition? existing, OptiSchemaFamily? requestedFamily = null)
    {
        _editingPreset = existing;
        _editingFamily = existing?.Family ?? requestedFamily ?? OptiSchemaFamily.V10;
        _showSourceComments = await AppServices.SourceComments.LoadAsync();
        var schema = AppServices.Schemas.Resolve(_editingFamily);
        var visibility = await AppServices.EditorVisibility.LoadAsync();
        var existingValues = existing?.Entries.ToDictionary(x => x.SettingId, x => x.RawValue, StringComparer.OrdinalIgnoreCase) ?? [];
        _entryEditors = schema.Settings
            .Where(x => EditorVisibilityPolicy.Resolve(x, _editingFamily, visibility) == EditorVisibility.Visible)
            .Select(x => new PresetEntryEditor(x, existingValues.TryGetValue(x.Id, out var value), value ?? x.DefaultRawValue))
            .ToList();

        EditorTitle.Text = existing is null ? "Create preset" : "Edit preset";
        EditorFamily.Text = $"Target family: {_editingFamily}";
        PresetNameBox.Text = existing?.Name ?? string.Empty;
        PresetDescriptionBox.Text = existing?.Description ?? string.Empty;
        PresetSettingSearchBox.Text = string.Empty;
        PresetList.Visibility = Visibility.Collapsed;
        PresetEditor.Visibility = Visibility.Visible;
        RenderPresetSections();
    }

    private void PresetSettingSearchBox_TextChanged(object sender, TextChangedEventArgs e) => RenderPresetSections();

    private void RenderPresetSections()
    {
        PresetSectionsPanel.Children.Clear();
        var search = PresetSettingSearchBox.Text.Trim();
        var groups = _entryEditors
            .Where(x => string.IsNullOrWhiteSpace(search) || x.Definition.IniKey.Name.Contains(search, StringComparison.OrdinalIgnoreCase))
            .GroupBy(x => x.Definition.GroupId)
            .OrderBy(x => x.Min(y => y.Definition.Order));

        foreach (var group in groups)
        {
            var rows = new StackPanel { Spacing = 8 };
            foreach (var entry in group)
            {
                var row = new Grid { ColumnSpacing = 12 };
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2, GridUnitType.Star) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                var label = new StackPanel { Spacing = 3 };
                label.Children.Add(new TextBlock { Text = entry.Definition.DisplayName });
                if (_showSourceComments && !string.IsNullOrWhiteSpace(entry.Definition.SourceComment)) label.Children.Add(new TextBlock { Text = entry.Definition.SourceComment, TextWrapping = TextWrapping.Wrap, Opacity = .7 });
                var check = new CheckBox { Content = label, IsChecked = entry.Included, VerticalAlignment = VerticalAlignment.Center };
                check.Checked += (_, _) => entry.Included = true;
                check.Unchecked += (_, _) => entry.Included = false;
                var value = SettingValueControlFactory.Create(entry.Definition, entry.Value, changed => entry.Value = changed, entry.Definition.Description);
                row.Children.Add(check); Grid.SetColumn(value, 1); row.Children.Add(value); rows.Children.Add(row);
            }

            PresetSectionsPanel.Children.Add(new CollapsibleSectionCard(group.Key, rows, !string.IsNullOrWhiteSpace(search)));
        }
    }

    private async void SavePreset_Click(object sender, RoutedEventArgs e)
    {
        var now = DateTimeOffset.UtcNow;
        var preset = new PresetDefinition(_editingPreset?.Id ?? Guid.NewGuid(), PresetNameBox.Text.Trim(), string.IsNullOrWhiteSpace(PresetDescriptionBox.Text) ? null : PresetDescriptionBox.Text.Trim(), _editingFamily, PresetSource.User, _entryEditors.Where(x => x.Included).Select(x => new PresetEntry(x.Definition.Id, x.Value)).ToArray(), _editingPreset?.CreatedAt ?? now, now);
        var changes = CreatePresetSaveReview(preset);
        if (!await SaveReviewDialog.ConfirmAsync(XamlRoot, "Review preset", "The following values will be saved in this preset.", changes)) return;
        var error = await ViewModel.SaveAsync(preset);
        if (error is not null) { ViewModel.StatusText = error; return; }
        ViewModel.StatusText = "Preset saved.";
        ClosePresetEditor();
    }

    private IReadOnlyList<string> CreatePresetSaveReview(PresetDefinition preset)
    {
        var schema = AppServices.Schemas.Resolve(_editingFamily);
        var previous = _editingPreset?.Entries.ToDictionary(x => x.SettingId, x => x.RawValue, StringComparer.OrdinalIgnoreCase) ?? [];
        var current = preset.Entries.ToDictionary(x => x.SettingId, x => x.RawValue, StringComparer.OrdinalIgnoreCase);
        var settingIds = previous.Keys.Union(current.Keys, StringComparer.OrdinalIgnoreCase)
            .Where(id => !previous.TryGetValue(id, out var before) || !current.TryGetValue(id, out var after) || !string.Equals(before, after, StringComparison.Ordinal))
            .OrderBy(id => schema.FindById(id)?.Order ?? int.MaxValue)
            .ThenBy(id => id, StringComparer.OrdinalIgnoreCase);

        return settingIds.Select(id =>
        {
            var definition = schema.FindById(id);
            var section = definition?.IniKey.Section ?? "Unknown";
            var key = definition?.IniKey.Name ?? id;
            return current.TryGetValue(id, out var value) ? $"[{section}] {key} = {value}" : $"[{section}] {key} = (removed)";
        }).ToArray();
    }

    private void CancelPresetEdit_Click(object sender, RoutedEventArgs e) => ClosePresetEditor();
    private void ClosePresetEditor() { PresetEditor.Visibility = Visibility.Collapsed; PresetList.Visibility = Visibility.Visible; _entryEditors = []; _editingPreset = null; }

    private sealed class PresetEntryEditor(SettingDefinition definition, bool included, string value)
    {
        public SettingDefinition Definition { get; } = definition;
        public bool Included { get; set; } = included;
        public string Value { get; set; } = value;
    }
}
