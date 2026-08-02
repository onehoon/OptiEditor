using Microsoft.UI.Xaml.Controls;
using OptiEditor.App.ViewModels;
using WinRT.Interop;
using Windows.Storage.Pickers;

namespace OptiEditor.App.Views;
public sealed partial class OptiScalerUpdatePage : Page
{
    private bool _isActive;
    public OptiScalerUpdateViewModel ViewModel { get; } = new();
    public OptiScalerUpdatePage() { InitializeComponent(); }
    protected override void OnNavigatedTo(Microsoft.UI.Xaml.Navigation.NavigationEventArgs e) { _isActive = true; base.OnNavigatedTo(e); }
    protected override void OnNavigatedFrom(Microsoft.UI.Xaml.Navigation.NavigationEventArgs e) { _isActive = false; ViewModel.Dispose(); base.OnNavigatedFrom(e); }
    private async void SelectSource_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        var picker = new FileOpenPicker(); picker.FileTypeFilter.Add("*"); InitializeWithWindow.Initialize(picker, App.WindowHandle);
        var file = await picker.PickSingleFileAsync(); if (file is not null) ViewModel.SelectSource(file.Path);
    }
    private async void ReplaceSelected_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        var confirmation = new ContentDialog { XamlRoot = XamlRoot, Title = "Replace OptiScaler binaries?", Content = $"The existing OptiScaler binary will be overwritten for {ViewModel.SelectedCount} installations.\n\nNo backup will be created.\n\nSource version: {ViewModel.Source?.FileVersion}", PrimaryButtonText = "Replace", CloseButtonText = "Cancel", DefaultButton = ContentDialogButton.Close };
        if (await confirmation.ShowAsync() != ContentDialogResult.Primary) return;
        var results = await ViewModel.ReplaceAsync();
        if (!_isActive) return;
        var replaced = results.Count(x => x.Status == OptiEditor.Core.OptiScalerUpdate.OptiScalerReplacementStatus.Replaced);
        var title = results.Count == 0 ? "Nothing to replace" : replaced == results.Count ? "Replacement completed" : replaced == 0 ? "Replacement failed" : "Replacement partially completed";
        var summary = $"Selected: {results.Count}\nReplaced: {replaced}\nSkipped: {results.Count(x => x.Status == OptiEditor.Core.OptiScalerUpdate.OptiScalerReplacementStatus.Skipped)}\nFailed: {results.Count(x => x.Status == OptiEditor.Core.OptiScalerUpdate.OptiScalerReplacementStatus.Failed)}\nCanceled: {results.Count(x => x.Status == OptiEditor.Core.OptiScalerUpdate.OptiScalerReplacementStatus.Canceled)}\n\n" + string.Join("\n", results.Select(x => $"{x.GameDisplayName ?? x.InstallDirectory} — {x.TargetFileName}: {x.Status}{(x.UserMessage is null ? "" : $" — {x.UserMessage}")}"));
        await new ContentDialog { XamlRoot = XamlRoot, Title = title, Content = summary, CloseButtonText = "Close" }.ShowAsync();
    }
    private void Cancel_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e) => ViewModel.Cancel();
}
