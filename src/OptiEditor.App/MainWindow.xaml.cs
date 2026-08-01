using Microsoft.UI.Xaml;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

using Microsoft.UI.Xaml.Controls;
using OptiEditor.App.Views;

namespace OptiEditor.App;

/// <summary>
/// The application window. This hosts a Frame that displays pages. Add your
/// UI and logic to MainPage.xaml / MainPage.xaml.cs instead of here so you
/// can use Page features such as navigation events and the Loaded lifecycle.
/// </summary>
public sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);

        AppWindow.SetIcon("Assets/AppIcon.ico");

        RootFrame.Navigate(typeof(GamesPage));
    }

    private void Navigation_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.SelectedItem is not NavigationViewItem item) return;
        RootFrame.Navigate(item.Tag switch
        {
            "Games" => typeof(GamesPage), "Folders" => typeof(FoldersPage), "OptiScalerUpdate" => typeof(OptiScalerUpdatePage),
            "Presets" => typeof(PresetsPage), _ => typeof(PlaceholderPage)
        }, item.Tag);
    }
}
