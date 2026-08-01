using System.Runtime.InteropServices;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

using Microsoft.UI.Xaml.Controls;
using OptiEditor.App.Views;
using WinRT.Interop;

namespace OptiEditor.App;

/// <summary>
/// The application window. This hosts a Frame that displays pages. Add your
/// UI and logic to MainPage.xaml / MainPage.xaml.cs instead of here so you
/// can use Page features such as navigation events and the Loaded lifecycle.
/// </summary>
public sealed partial class MainWindow : Window
{
    private const int DefaultWidthDips = 1200;
    private const int DefaultHeightDips = 720;

    public MainWindow()
    {
        InitializeComponent();

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);
        ApplyDefaultSize();
    }

    private void Navigation_Loaded(object sender, RoutedEventArgs e)
    {
        Navigation.SelectedItem = Navigation.MenuItems[0];
        Navigation.OpenPaneLength = MeasureAutoPaneWidth();
    }

    private void ApplyDefaultSize()
    {
        var hwnd = WindowNative.GetWindowHandle(this); var dpi = GetDpiForWindow(hwnd); var scale = dpi > 0 ? dpi / 96.0 : 1.0;
        var widthPx = (int)(DefaultWidthDips * scale); var heightPx = (int)(DefaultHeightDips * scale);
        var windowId = Win32Interop.GetWindowIdFromWindow(hwnd); var displayArea = DisplayArea.GetFromWindowId(windowId, DisplayAreaFallback.Nearest);
        if (displayArea is not null) { widthPx = Math.Min(widthPx, displayArea.WorkArea.Width); heightPx = Math.Min(heightPx, displayArea.WorkArea.Height); }
        AppWindow.Resize(new Windows.Graphics.SizeInt32(widthPx, heightPx));
        if (displayArea is not null) AppWindow.Move(new Windows.Graphics.PointInt32(displayArea.WorkArea.X + (displayArea.WorkArea.Width - widthPx) / 2, displayArea.WorkArea.Y + (displayArea.WorkArea.Height - heightPx) / 2));
    }

    private double MeasureAutoPaneWidth()
    {
        const double iconColumnAndPadding = 68; const double minPaneWidth = 140; var maxTextWidth = 0.0;
        foreach (var label in Navigation.MenuItems.Concat(Navigation.FooterMenuItems).OfType<NavigationViewItem>().Select(x => (x.Content as TextBlock)?.Text).Where(x => !string.IsNullOrWhiteSpace(x)))
        {
            var block = new TextBlock { Text = label }; block.Measure(new Windows.Foundation.Size(double.PositiveInfinity, double.PositiveInfinity)); maxTextWidth = Math.Max(maxTextWidth, block.DesiredSize.Width);
        }
        return Math.Max(minPaneWidth, maxTextWidth + iconColumnAndPadding);
    }

    [DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(IntPtr hwnd);

    private void Navigation_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.SelectedItemContainer is not NavigationViewItem item) return;
        RootFrame.Navigate(item.Tag switch
        {
            "Games" => typeof(GamesPage), "Folders" => typeof(FoldersPage), "OptiScalerUpdate" => typeof(OptiScalerUpdatePage),
            "Presets" => typeof(PresetsPage), "Settings" => typeof(SettingsPage), _ => typeof(PlaceholderPage)
        }, item.Tag);
    }
}
