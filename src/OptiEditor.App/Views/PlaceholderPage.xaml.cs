using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace OptiEditor.App.Views;
public sealed partial class PlaceholderPage : Page
{
    public PlaceholderPage() => InitializeComponent();
    protected override void OnNavigatedTo(NavigationEventArgs e) { base.OnNavigatedTo(e); Heading.Text = e.Parameter as string ?? "Coming soon"; }
}
