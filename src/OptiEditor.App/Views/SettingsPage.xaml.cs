using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using OptiEditor.App.ViewModels;
using OptiEditor.Core.Models;
using OptiEditor.Core.Storage;

namespace OptiEditor.App.Views;

public sealed partial class SettingsPage : Page
{
    public EditorVisibilitySettingsViewModel ViewModel { get; } = new();

    public SettingsPage()
    {
        InitializeComponent();
        BuildCategories();
    }

    private void BuildCategories()
    {
        AddStartupTabCard();
        AddEditorVisibilityCard();
    }

    private void AddStartupTabCard()
    {
        var icon = new SymbolIcon(Symbol.Play) { VerticalAlignment = VerticalAlignment.Center };
        var text = new StackPanel { Spacing = 2, VerticalAlignment = VerticalAlignment.Center };
        text.Children.Add(new TextBlock { Text = "Startup tab", Style = (Style)Application.Current.Resources["SubtitleTextBlockStyle"] });
        text.Children.Add(new TextBlock { Text = "Choose the page shown when OptiEditor starts", Opacity = .7, TextWrapping = TextWrapping.Wrap });
        var combo = new ComboBox { Width = 190, VerticalAlignment = VerticalAlignment.Center };
        combo.Items.Add(new ComboBoxItem { Content = "Games", Tag = StartupTabs.Games });
        combo.Items.Add(new ComboBoxItem { Content = "OptiScaler Update", Tag = StartupTabs.OptiScalerUpdate });
        combo.SelectionChanged += StartupTab_SelectionChanged;

        var content = CreateCardContent(icon, text, combo);
        SettingsCategories.Children.Add(new Border { Child = content, Padding = new Thickness(18, 16, 18, 16), BorderBrush = (Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"], BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(8) });
        _ = LoadStartupTabAsync(combo);
    }

    private async Task LoadStartupTabAsync(ComboBox combo)
    {
        var tab = await Services.AppServices.StartupTab.LoadAsync();
        combo.SelectedItem = combo.Items.OfType<ComboBoxItem>().First(item => item.Tag as string == tab);
    }

    private async void StartupTab_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if ((sender as ComboBox)?.SelectedItem is ComboBoxItem item && item.Tag is string tab)
            await Services.AppServices.StartupTab.SaveAsync(tab);
    }

    private void AddEditorVisibilityCard()
    {
        var icon = new SymbolIcon(Symbol.Edit) { VerticalAlignment = VerticalAlignment.Center };
        var text = new StackPanel { Spacing = 2, VerticalAlignment = VerticalAlignment.Center };
        text.Children.Add(new TextBlock { Text = "Editor visibility", Style = (Style)Application.Current.Resources["SubtitleTextBlockStyle"] });
        text.Children.Add(new TextBlock { Text = "Choose which OptiScaler settings are shown while editing", Opacity = .7, TextWrapping = TextWrapping.Wrap });

        var chevron = new SymbolIcon(Symbol.Forward) { VerticalAlignment = VerticalAlignment.Center, Opacity = .7 };
        var content = CreateCardContent(icon, text, chevron);

        var button = new Button { Content = content, HorizontalContentAlignment = HorizontalAlignment.Stretch, Padding = new Thickness(18, 16, 18, 16), HorizontalAlignment = HorizontalAlignment.Stretch };
        button.Click += EditorVisibility_Click;
        SettingsCategories.Children.Add(button);
    }

    private static Grid CreateCardContent(FrameworkElement icon, FrameworkElement text, FrameworkElement trailing)
    {
        var content = new Grid { ColumnSpacing = 16 };
        content.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        content.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        content.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Grid.SetColumn(icon, 0); Grid.SetColumn(text, 1); Grid.SetColumn(trailing, 2);
        content.Children.Add(icon); content.Children.Add(text); content.Children.Add(trailing);
        return content;
    }

    private async void EditorVisibility_Click(object sender, RoutedEventArgs e)
    {
        SettingsHome.Visibility = Visibility.Collapsed;
        EditorVisibilityDetail.Visibility = Visibility.Visible;
        await ViewModel.LoadAsync(OptiSchemaFamily.V10);
        Render();
    }

    private void Back_Click(object sender, RoutedEventArgs e)
    {
        EditorVisibilityDetail.Visibility = Visibility.Collapsed;
        SettingsHome.Visibility = Visibility.Visible;
    }

    private async void Family_Click(object sender, RoutedEventArgs e)
    {
        await ViewModel.LoadAsync((sender as Button)?.Tag is "V09" ? OptiSchemaFamily.V09 : OptiSchemaFamily.V10);
        Render();
    }

    private async void Save_Click(object sender, RoutedEventArgs e) => await ViewModel.SaveAsync();
    private async void Reset_Click(object sender, RoutedEventArgs e) { await ViewModel.ResetAsync(); Render(); }

    private void Render()
    {
        SettingsPanel.Children.Clear(); string? group = null;
        foreach (var item in ViewModel.Settings)
        {
            if (!string.Equals(group, item.GroupName, StringComparison.Ordinal))
            {
                group = item.GroupName;
                SettingsPanel.Children.Add(new TextBlock { Text = group, Style = (Style)Application.Current.Resources["SubtitleTextBlockStyle"], Margin = new Thickness(0, 12, 0, 2) });
            }
            var toggle = new ToggleSwitch { Header = item.DisplayName, IsOn = item.IsVisible, Tag = item, OnContent = "Shown", OffContent = "Hidden" };
            toggle.Toggled += (_, _) => { if (toggle.Tag is EditorVisibilitySettingItemViewModel current) { current.IsVisible = toggle.IsOn; current.IsOverridden = current.IsVisible != current.DefaultVisible; } };
            var row = new StackPanel { Spacing = 2 }; row.Children.Add(toggle);
            if (!string.IsNullOrWhiteSpace(item.Description)) row.Children.Add(new TextBlock { Text = item.Description, TextWrapping = TextWrapping.Wrap });
            row.Children.Add(new TextBlock { Text = item.DefaultVisible ? "App default: shown" : "App default: hidden", Opacity = .65 });
            SettingsPanel.Children.Add(new Border { Child = row, Padding = new Thickness(10), BorderBrush = (Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"], BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(6) });
        }
    }
}
