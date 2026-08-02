using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using OptiEditor.App.ViewModels;
using OptiEditor.Core.Models;
using OptiEditor.Core.Storage;

namespace OptiEditor.App.Views;

public sealed partial class SettingsPage : Page
{
    private const string EditorVisibilityNavigationParameter = "EditorVisibility";
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
        text.Children.Add(new TextBlock { Text = "Section visibility", Style = (Style)Application.Current.Resources["SubtitleTextBlockStyle"] });
        text.Children.Add(new TextBlock { Text = "Choose which OptiScaler INI sections are shown while editing", Opacity = .7, TextWrapping = TextWrapping.Wrap });

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

    protected override async void OnNavigatedTo(Microsoft.UI.Xaml.Navigation.NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        if (e.Parameter is EditorVisibilityNavigationParameter)
        {
            SettingsHome.Visibility = Visibility.Collapsed;
            EditorVisibilityDetail.Visibility = Visibility.Visible;
            await ViewModel.LoadAsync(OptiSchemaFamily.V09);
            UpdateFamilyButtons();
            Render();
        }
        else
        {
            EditorVisibilityDetail.Visibility = Visibility.Collapsed;
            SettingsHome.Visibility = Visibility.Visible;
        }
    }

    private void EditorVisibility_Click(object sender, RoutedEventArgs e)
    {
        Frame.Navigate(typeof(SettingsPage), EditorVisibilityNavigationParameter);
    }

    private void Back_Click(object sender, RoutedEventArgs e)
    {
        if (Frame.CanGoBack) Frame.GoBack();
    }

    private async void Family_Click(object sender, RoutedEventArgs e)
    {
        await ViewModel.LoadAsync((sender as Button)?.Tag is "V09" ? OptiSchemaFamily.V09 : OptiSchemaFamily.V10);
        UpdateFamilyButtons();
        Render();
    }

    private void UpdateFamilyButtons()
    {
        var accentStyle = (Style)Application.Current.Resources["AccentButtonStyle"];
        V09Button.Style = ViewModel.SelectedFamily == OptiSchemaFamily.V09 ? accentStyle : null;
        V10Button.Style = ViewModel.SelectedFamily == OptiSchemaFamily.V10 ? accentStyle : null;
    }

    private async void Save_Click(object sender, RoutedEventArgs e) => await ViewModel.SaveAsync();
    private async void Reset_Click(object sender, RoutedEventArgs e) { await ViewModel.ResetAsync(); Render(); }

    private void Render()
    {
        SettingsPanel.Children.Clear();
        SettingsPanel.RowDefinitions.Clear();
        for (var index = 0; index < ViewModel.Sections.Count; index++)
        {
            if (index % 2 == 0) SettingsPanel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var item = ViewModel.Sections[index];
            var title = new TextBlock { Text = item.Section, Style = (Style)Application.Current.Resources["SubtitleTextBlockStyle"], VerticalAlignment = VerticalAlignment.Center };
            var toggle = new ToggleSwitch { IsOn = item.IsVisible, Tag = item, OnContent = "Shown", OffContent = "Hidden", HorizontalAlignment = HorizontalAlignment.Right };
            toggle.Toggled += (_, _) => { if (toggle.Tag is EditorVisibilitySectionItemViewModel current) { current.IsVisible = toggle.IsOn; current.IsOverridden = current.IsVisible != current.DefaultVisible; } };
            var content = CreateCardContent(new SymbolIcon(Symbol.Bullets) { VerticalAlignment = VerticalAlignment.Center }, title, toggle);
            var card = new Border { Child = content, Padding = new Thickness(14, 12, 14, 12), BorderBrush = (Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"], BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(8) };
            Grid.SetRow(card, index / 2); Grid.SetColumn(card, index % 2); SettingsPanel.Children.Add(card);
        }
    }
}
