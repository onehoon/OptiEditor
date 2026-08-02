using Microsoft.UI.Xaml.Controls;
using OptiEditor.App.ViewModels;
using OptiEditor.Core.Discovery;
using OptiEditor.Core.Models;
using OptiEditor.Core.OptiScalerUpdate;
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

        var preparation = await ViewModel.PrepareReplacementAsync();
        if (!_isActive || preparation is null) return;

        if (preparation.MultiProxyTargets.Count > 0) await ShowMultiProxyNoticeAsync(preparation.MultiProxyTargets);
        if (!_isActive) return;

        if (preparation.FamilyMismatchTargets.Count > 0)
        {
            var proceed = await ShowFamilyMismatchDialogAsync(preparation.Plan, preparation.FamilyMismatchTargets);
            if (!_isActive) return;
            if (!proceed) { ViewModel.AbortPreparedReplacement("Replacement canceled."); return; }
        }

        var results = await ViewModel.ExecuteReplacementAsync(preparation.Plan);
        if (!_isActive) return;
        var mismatchedDirectories = preparation.FamilyMismatchTargets.Select(x => x.Installation.InstallDirectory).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var crossedFamiliesSuccessfully = results.Any(x => x.Status == OptiScalerReplacementStatus.Replaced && mismatchedDirectories.Contains(x.InstallDirectory));
        await ShowResultDialogAsync(results, crossedFamiliesSuccessfully);
    }

    private async Task ShowMultiProxyNoticeAsync(IReadOnlyList<OptiScalerReplacementResult> multiProxyTargets)
    {
        if (multiProxyTargets.Count == 1)
        {
            var target = multiProxyTargets[0];
            var detected = string.Join("\n", (target.DetectedBinaryNames ?? []).Select(name => $"- {name}"));
            var dialog = new ContentDialog
            {
                XamlRoot = XamlRoot,
                Title = "Multiple OptiScaler DLLs detected",
                Content = $"Multiple OptiScaler proxy DLLs were found in this installation.\nGame: {target.GameDisplayName ?? target.InstallDirectory}\nFolder: {target.InstallDirectory}\nDetected OptiScaler DLLs:\n{detected}\nOptiEditor cannot safely determine which DLL should be updated.\nPlease remove the unnecessary OptiScaler DLLs manually so that only one remains, then scan again.",
                PrimaryButtonText = "Open Folder",
                CloseButtonText = "Close"
            };
            if (await dialog.ShowAsync() == ContentDialogResult.Primary) OpenFolder(target.InstallDirectory);
        }
        else
        {
            var body = string.Join("\n\n", multiProxyTargets.Select(target => $"{target.GameDisplayName ?? "Unknown game"}\n{target.InstallDirectory}\n" + string.Join("\n", (target.DetectedBinaryNames ?? []).Select(name => $"- {name}"))));
            var dialog = new ContentDialog
            {
                XamlRoot = XamlRoot,
                Title = "Multiple OptiScaler DLLs detected",
                Content = $"The following installations contain multiple OptiScaler DLLs and will be skipped:\n\n{body}\n\nPlease manually remove the unnecessary OptiScaler DLLs so that only one remains in each folder, then scan again.",
                CloseButtonText = "Close"
            };
            await dialog.ShowAsync();
        }
    }

    private async Task<bool> ShowFamilyMismatchDialogAsync(OptiScalerReplacementPlan plan, IReadOnlyList<OptiScalerReplacementTarget> mismatched)
    {
        var sourceFamily = OptiBinaryRules.DetectFamily(plan.Source.FileVersion);
        if (mismatched.Count == 1)
        {
            var target = mismatched[0].Installation;
            var dialog = new ContentDialog
            {
                XamlRoot = XamlRoot,
                Title = "Different OptiScaler version family detected",
                Content = $"This installation currently uses OptiScaler {FamilyLabel(target.SchemaFamily)}, but the selected source is OptiScaler {FamilyLabel(sourceFamily)}.\nThe existing OptiScaler.ini file will not be converted automatically. Some settings may be unsupported or behave differently after replacement.\nGame: {target.GameDisplayName}\nCurrent version: {target.FileVersion}\nSource version: {plan.Source.FileVersion}\nContinue with the replacement?",
                PrimaryButtonText = "Replace Anyway",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Close
            };
            return await dialog.ShowAsync() == ContentDialogResult.Primary;
        }
        else
        {
            var to10 = mismatched.Where(x => x.Installation.SchemaFamily == OptiSchemaFamily.V09).ToArray();
            var to09 = mismatched.Where(x => x.Installation.SchemaFamily == OptiSchemaFamily.V10).ToArray();
            var lines = new List<string> { $"Selected installations: {plan.Targets.Count}", $"Same version family: {plan.Targets.Count - mismatched.Count}" };
            if (to10.Length > 0) lines.Add($"OptiScaler 0.9 → 0.10: {to10.Length}");
            if (to09.Length > 0) lines.Add($"OptiScaler 0.10 → 0.9: {to09.Length}");
            if (to10.Length > 0) { lines.Add(""); lines.Add("0.9 → 0.10"); lines.AddRange(to10.Select(x => $"- {x.Installation.GameDisplayName}")); }
            if (to09.Length > 0) { lines.Add(""); lines.Add("0.10 → 0.9"); lines.AddRange(to09.Select(x => $"- {x.Installation.GameDisplayName}")); }
            lines.Add("");
            lines.Add("The existing OptiScaler.ini files will not be converted automatically.");
            lines.Add("Some settings may be unsupported or behave differently after replacement.");
            lines.Add("");
            lines.Add("Continue with the replacement?");
            var dialog = new ContentDialog
            {
                XamlRoot = XamlRoot,
                Title = "Different OptiScaler version family detected",
                Content = string.Join("\n", lines),
                PrimaryButtonText = "Replace Anyway",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Close
            };
            return await dialog.ShowAsync() == ContentDialogResult.Primary;
        }
    }

    private async Task ShowResultDialogAsync(IReadOnlyList<OptiScalerReplacementResult> results, bool crossedFamiliesSuccessfully)
    {
        var replaced = results.Count(x => x.Status == OptiScalerReplacementStatus.Replaced);
        var title = OptiScalerUpdateViewModel.ClassifyOutcome(results) switch
        {
            OptiScalerUpdateViewModel.ReplacementOutcome.None => "Nothing to replace",
            OptiScalerUpdateViewModel.ReplacementOutcome.Completed => "Replacement completed",
            OptiScalerUpdateViewModel.ReplacementOutcome.Canceled => "Replacement canceled",
            OptiScalerUpdateViewModel.ReplacementOutcome.Skipped => "Replacement skipped",
            OptiScalerUpdateViewModel.ReplacementOutcome.Failed => "Replacement failed",
            _ => "Replacement partially completed",
        };
        var summary = $"Selected: {results.Count}\nReplaced: {replaced}\nSkipped: {results.Count(x => x.Status == OptiScalerReplacementStatus.Skipped)}\nFailed: {results.Count(x => x.Status == OptiScalerReplacementStatus.Failed)}\nCanceled: {results.Count(x => x.Status == OptiScalerReplacementStatus.Canceled)}\n\n"
            + string.Join("\n", results.Select(x => $"{x.GameDisplayName ?? x.InstallDirectory} — {x.TargetFileName}: {x.Status}{(x.UserMessage is null ? "" : $" — {x.UserMessage}")}"))
            + (crossedFamiliesSuccessfully ? "\n\nReview the OptiScaler settings after changing version families." : "");
        await new ContentDialog { XamlRoot = XamlRoot, Title = title, Content = summary, CloseButtonText = "Close" }.ShowAsync();
    }

    private static string FamilyLabel(OptiSchemaFamily family) => family switch
    {
        OptiSchemaFamily.V09 => "0.9",
        OptiSchemaFamily.V10 => "0.10",
        _ => "Unsupported",
    };
    private static void OpenFolder(string path) => System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("explorer.exe", path) { UseShellExecute = true });

    private void Cancel_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e) => ViewModel.Cancel();
}
