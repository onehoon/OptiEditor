using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace OptiEditor.App.Controls;

public static class SaveReviewDialog
{
    public static async Task<bool> ConfirmAsync(XamlRoot xamlRoot, string title, string description, IEnumerable<string> changes)
    {
        var preview = new TextBox
        {
            Text = string.Join(Environment.NewLine, changes),
            IsReadOnly = true,
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            MinWidth = 520,
            MinHeight = 120,
            MaxHeight = 420
        };
        var content = new StackPanel { Spacing = 12 };
        content.Children.Add(new TextBlock { Text = description, TextWrapping = TextWrapping.Wrap });
        content.Children.Add(new ScrollViewer { Content = preview, MaxHeight = 420 });
        var dialog = new ContentDialog
        {
            XamlRoot = xamlRoot,
            Title = title,
            Content = content,
            PrimaryButtonText = "Save",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary
        };
        return await dialog.ShowAsync() == ContentDialogResult.Primary;
    }
}
