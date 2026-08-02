using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using OptiEditor.App.Controls;
using OptiEditor.App.Services;
using OptiEditor.App.ViewModels;
using OptiEditor.Core.Models;
using OptiEditor.Core.Ini;
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
    private PresetDefinition? _applyPreset;
    private bool _isApplying;
    private readonly List<ApplyGameItem> _allApplyGames = [];

    public PresetsViewModel ViewModel { get; } = new();
    public ObservableCollection<ApplyGameItem> ApplyGames { get; } = [];
    public PresetsPage() { InitializeComponent(); Loaded += async (_, _) => await ViewModel.LoadAsync(); }

    private async void Delete_Click(object sender, RoutedEventArgs e) { if ((sender as Button)?.Tag is PresetDefinition preset) { if (preset.Source != PresetSource.User) { ViewModel.StatusText = "Built-in presets cannot be deleted."; return; } await ViewModel.DeleteAsync(preset); } }
    private async void Create_Click(object sender, RoutedEventArgs e) => await OpenPresetEditorAsync(null, (sender as Button)?.Tag is "V09" ? OptiSchemaFamily.V09 : OptiSchemaFamily.V10);
    private async void Edit_Click(object sender, RoutedEventArgs e) { if ((sender as Button)?.Tag is PresetDefinition preset) { await OpenPresetEditorAsync(preset with { Source = PresetSource.User }); } }
    private void ApplyToGames_Click(object sender, RoutedEventArgs e) { if ((sender as Button)?.Tag is PresetDefinition preset) OpenApplyPage(preset); }

    private void OpenApplyPage(PresetDefinition preset)
    {
        _applyPreset = preset;
        _allApplyGames.Clear();
        _allApplyGames.AddRange(AppServices.Installations.Installations
            .Where(x => x.SchemaFamily == preset.Family)
            .OrderBy(x => x.GameDisplayName, StringComparer.OrdinalIgnoreCase)
            .Select(x => new ApplyGameItem(x)));
        ApplyTitle.Text = $"Apply: {preset.Name}";
        ApplyVersion.Text = preset.Family == OptiSchemaFamily.V09 ? "Target Version: v0.9" : "Target Version: v0.10";
        ApplySearchBox.Text = string.Empty;
        PresetList.Visibility = Visibility.Collapsed;
        PresetEditor.Visibility = Visibility.Collapsed;
        ApplyPresetPage.Visibility = Visibility.Visible;
        RenderApplyGames();
    }

    private void RenderApplyGames()
    {
        ApplyGames.Clear();
        if (_applyPreset is null) return;
        var search = ApplySearchBox.Text.Trim();
        foreach (var item in _allApplyGames.Where(x => string.IsNullOrWhiteSpace(search)
            || x.Installation.GameDisplayName.Contains(search, StringComparison.OrdinalIgnoreCase)
            || (x.Installation.GameExeName?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false)
            || x.Installation.InstallDirectory.Contains(search, StringComparison.OrdinalIgnoreCase)))
        {
            ApplyGames.Add(item);
        }
        UpdateApplyAllState();
    }

    private void UpdateApplyAllState() => ApplyAllCheckBox.IsChecked = ApplyGames.Count > 0 && ApplyGames.All(x => x.IsSelected);
    private void ApplySearch_TextChanged(object sender, TextChangedEventArgs e) => RenderApplyGames();
    private void ApplyGameCheckBox_Click(object sender, RoutedEventArgs e) => UpdateApplyAllState();
    private void ApplyAll_Click(object sender, RoutedEventArgs e)
    {
        var value = ApplyAllCheckBox.IsChecked == true;
        foreach (var item in ApplyGames) item.IsSelected = value;
        UpdateApplyAllState();
    }
    private void BackFromApply_Click(object sender, RoutedEventArgs e)
    {
        ApplyPresetPage.Visibility = Visibility.Collapsed;
        PresetList.Visibility = Visibility.Visible;
        _applyPreset = null;
        _allApplyGames.Clear();
        ApplyGames.Clear();
    }

    private async void ApplySelected_Click(object sender, RoutedEventArgs e)
    {
        var preset = _applyPreset;
        if (preset is null || _isApplying) return;
        var selected = _allApplyGames.Where(x => x.IsSelected).Select(x => x.Installation).ToArray();
        if (selected.Length == 0) { ViewModel.StatusText = "Select at least one game."; return; }
        var schema = AppServices.Schemas.Resolve(preset.Family);
        var entries = preset.Entries.ToArray();
        var lines = entries.Select(x => $"[{x.SettingId}] = {x.RawValue}").ToArray();
        if (!await SaveReviewDialog.ConfirmAsync(XamlRoot, "Review preset application", $"Apply '{preset.Name}' to {selected.Length} game(s).", lines)) return;
        _isApplying = true; ApplySelectedButton.IsEnabled = false; BackFromApplyButton.IsEnabled = false; ApplySearchBox.IsEnabled = false; ApplyAllCheckBox.IsEnabled = false; ApplyGamesRepeater.IsEnabled = false;
        var applied = 0; var failed = 0;
        foreach (var game in selected)
        {
            try
            {
                var session = await new IniEditorSessionService(AppServices.IniFiles).OpenSessionAsync(game); var candidate = session.Document.Clone();
                var patchResults = entries.Select(entry => schema.FindById(entry.SettingId) is { } definition ? candidate.ApplyPatch(new IniPatch(definition.IniKey, entry.RawValue)) : null).ToArray();
                if (patchResults.Any(x => x is null || x.Error is not null)) { failed++; continue; }
                var result = await AppServices.IniFiles.SaveAsync(candidate, session.Snapshot); if (result.Success) applied++; else failed++;
            }
            catch (Exception ex) { AppServices.Logger.Error($"Preset application failed: {game.InstallDirectory}", ex); failed++; }
        }
        _isApplying = false; ApplySelectedButton.IsEnabled = true; BackFromApplyButton.IsEnabled = true; ApplySearchBox.IsEnabled = true; ApplyAllCheckBox.IsEnabled = true; ApplyGamesRepeater.IsEnabled = true;
        ViewModel.StatusText = $"Preset applied to {applied} of {selected.Length} game(s). Failed: {failed}.";
        var dialog = new ContentDialog { XamlRoot = XamlRoot, Title = "Preset application complete", Content = $"Applied: {applied}\nFailed: {failed}\nTotal selected: {selected.Length}", CloseButtonText = "OK", DefaultButton = ContentDialogButton.Close };
        await dialog.ShowAsync();
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
        EditorFamily.Text = _editingFamily == OptiSchemaFamily.V09 ? "Target Version: v0.9" : "Target Version: v0.10";
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
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
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
        // _entryEditors only covers settings visible under the current editor
        // visibility preferences. Entries for settings hidden (or otherwise not
        // shown for editing) must be carried over untouched, not dropped.
        var editableIds = _entryEditors.Select(x => x.Definition.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var preservedEntries = _editingPreset?.Entries.Where(x => !editableIds.Contains(x.SettingId)) ?? [];
        var entries = preservedEntries.Concat(_entryEditors.Where(x => x.Included).Select(x => new PresetEntry(x.Definition.Id, x.Value))).ToArray();
        var preset = new PresetDefinition(_editingPreset?.Id ?? Guid.NewGuid(), PresetNameBox.Text.Trim(), string.IsNullOrWhiteSpace(PresetDescriptionBox.Text) ? null : PresetDescriptionBox.Text.Trim(), _editingFamily, PresetSource.User, entries, _editingPreset?.CreatedAt ?? now, now);
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
        // The review must describe the complete preset that will be saved,
        // not only entries whose value differs from the previous version.
        // Otherwise unchanged entries (for example FGInput/FGOutput) are
        // persisted successfully but appear to be missing from the review.
        var settingIds = current.Keys
            .OrderBy(id => schema.FindById(id)?.Order ?? int.MaxValue)
            .ThenBy(id => id, StringComparer.OrdinalIgnoreCase)
            .Concat(previous.Keys.Except(current.Keys, StringComparer.OrdinalIgnoreCase)
                .OrderBy(id => schema.FindById(id)?.Order ?? int.MaxValue)
                .ThenBy(id => id, StringComparer.OrdinalIgnoreCase));

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

public partial class ApplyGameItem(OptiInstallation installation) : ObservableObject
{
    public OptiInstallation Installation { get; } = installation;

    [ObservableProperty]
    public partial bool IsSelected { get; set; }
}
