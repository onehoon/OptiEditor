using Microsoft.UI.Xaml.Controls;
using OptiEditor.App.Services;
using OptiEditor.App.ViewModels;
using OptiEditor.Core.Models;
using OptiEditor.Core.Presets;
using OptiEditor.Core.Schema;

namespace OptiEditor.App.Views;

public sealed partial class PresetsPage : Page
{
    public PresetsViewModel ViewModel { get; } = new();
    public PresetsPage() { InitializeComponent(); Loaded += async (_, _) => await ViewModel.LoadAsync(); }
    private async void Duplicate_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e) { if ((sender as Button)?.Tag is PresetDefinition preset) await ViewModel.DuplicateAsync(preset); }
    private async void Delete_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e) { if ((sender as Button)?.Tag is PresetDefinition preset) { if (preset.Source != PresetSource.User) { ViewModel.StatusText = "Built-in presets cannot be deleted."; return; } await ViewModel.DeleteAsync(preset); } }
    private async void Create_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e) => await EditPresetAsync(null, (sender as Button)?.Tag is "V09" ? OptiSchemaFamily.V09 : OptiSchemaFamily.V10);
    private async void Edit_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e) { if ((sender as Button)?.Tag is PresetDefinition preset) { if (preset.Source != PresetSource.User) { ViewModel.StatusText = "Built-in presets are read-only. Duplicate one to customize it."; return; } await EditPresetAsync(preset); } }
    private async void Apply_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if ((sender as Button)?.Tag is not PresetDefinition preset) return;
        var candidates = AppServices.Installations.Installations.Where(x => x.SchemaFamily == preset.Family).ToArray();
        if (candidates.Length == 0) { ViewModel.StatusText = "Scan and select a matching OptiScaler installation first."; return; }
        var list = new ListView { ItemsSource = candidates, DisplayMemberPath = nameof(OptiInstallation.GameDisplayName), SelectionMode = ListViewSelectionMode.Single, SelectedIndex = 0, MinWidth = 420 };
        var dialog = new ContentDialog { XamlRoot = XamlRoot, Title = "Apply preset to game", Content = list, PrimaryButtonText = "Review changes", CloseButtonText = "Cancel", DefaultButton = ContentDialogButton.Primary };
        if (await dialog.ShowAsync() == ContentDialogResult.Primary && list.SelectedItem is OptiInstallation installation) Frame.Navigate(typeof(EditorPage), new EditorNavigationRequest(installation, preset));
    }
    private async Task EditPresetAsync(PresetDefinition? existing, OptiSchemaFamily? requestedFamily = null)
    {
        var family = existing?.Family ?? requestedFamily ?? OptiSchemaFamily.V10;
        var schema = AppServices.Schemas.Resolve(family); var existingValues = existing?.Entries.ToDictionary(x => x.SettingId, x => x.RawValue, StringComparer.OrdinalIgnoreCase) ?? [];
        var name = new TextBox { Header = "Name", Text = existing?.Name ?? "" }; var description = new TextBox { Header = "Description", Text = existing?.Description ?? "", AcceptsReturn = true, TextWrapping = Microsoft.UI.Xaml.TextWrapping.Wrap };
        var editors = new List<PresetEntryEditor>(); var settings = new StackPanel { Spacing = 6 };
        foreach (var definition in schema.Settings)
        {
            var included = existingValues.TryGetValue(definition.Id, out var value); var check = new CheckBox { Content = definition.DisplayName, IsChecked = included };
            var raw = new TextBox { Text = included ? value! : "auto", PlaceholderText = definition.Description, MinWidth = 160 }; var row = new StackPanel { Orientation = Microsoft.UI.Xaml.Controls.Orientation.Horizontal, Spacing = 12 }; row.Children.Add(check); row.Children.Add(raw); settings.Children.Add(row); editors.Add(new(definition, check, raw));
        }
        var content = new StackPanel { Spacing = 10 }; content.Children.Add(name); content.Children.Add(description); content.Children.Add(new TextBlock { Text = $"Target family: {family}" }); content.Children.Add(new TextBlock { Text = "Select settings to include and provide their values." }); content.Children.Add(new ScrollViewer { Content = settings, MaxHeight = 460 });
        var dialog = new ContentDialog { XamlRoot = XamlRoot, Title = existing is null ? "Create preset" : "Edit preset", Content = content, PrimaryButtonText = "Save", CloseButtonText = "Cancel", DefaultButton = ContentDialogButton.Primary };
        if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;
        var now = DateTimeOffset.UtcNow; var preset = new PresetDefinition(existing?.Id ?? Guid.NewGuid(), name.Text.Trim(), string.IsNullOrWhiteSpace(description.Text) ? null : description.Text.Trim(), family, PresetSource.User, editors.Where(x => x.Included).Select(x => new PresetEntry(x.Definition.Id, x.Value)).ToArray(), existing?.CreatedAt ?? now, now);
        var error = await ViewModel.SaveAsync(preset); ViewModel.StatusText = error ?? "Preset saved.";
    }
    private sealed class PresetEntryEditor(SettingDefinition definition, CheckBox included, TextBox value) { public SettingDefinition Definition { get; } = definition; public bool Included => included.IsChecked == true; public string Value => value.Text; }
}
