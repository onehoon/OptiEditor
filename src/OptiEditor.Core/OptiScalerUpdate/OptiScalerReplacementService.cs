using System.Security.Cryptography;
using OptiEditor.Core.Discovery;
using OptiEditor.Core.Models;
using OptiEditor.Core.Utilities;

namespace OptiEditor.Core.OptiScalerUpdate;

public enum OptiScalerReplacementStatus { Replaced, Skipped, Failed, Canceled }
public enum OptiScalerReplacementReason { None, TargetMissing, TargetNotOptiScaler, TargetVersionFamilyChanged, FileInUse, AccessDenied, TemporaryValidationFailed, FinalVerificationFailed, SourceValidationFailed, Canceled, UnexpectedFailure, MultipleOptiScalerBinaries }

public sealed record OptiScalerReplacementTarget(OptiInstallation Installation);
public sealed record OptiScalerReplacementPlan(SourceOptiScalerBinary Source, IReadOnlyList<OptiScalerReplacementTarget> Targets, IReadOnlyList<OptiScalerReplacementResult> SkippedTargets);
public sealed record OptiScalerReplacementResult(string InstallDirectory, string? GameDisplayName, string TargetFileName, Version? PreviousVersion, Version? InstalledVersion, OptiScalerReplacementStatus Status, OptiScalerReplacementReason Reason, string? UserMessage = null, Exception? InternalException = null, IReadOnlyList<string>? DetectedBinaryNames = null);
public sealed record OptiScalerReplacementProgress(int Completed, int Total, string? GameDisplayName, string? TargetFileName);

// Result of pre-flight classification, run once per replacement attempt right
// before building the final plan: separates installations with more than one
// identified OptiScaler proxy DLL (never auto-resolved; always Skipped) from
// the remaining ready targets, and flags which of those ready targets are on
// a different OptiScaler version family (0.9 vs 0.10) than the source, so the
// caller can require explicit confirmation before including them.
public sealed record OptiScalerPlanClassification(
    IReadOnlyList<OptiScalerReplacementTarget> ReadyTargets,
    IReadOnlyList<OptiScalerReplacementResult> MultiProxyTargets,
    IReadOnlyList<OptiScalerReplacementTarget> FamilyMismatchTargets);

public sealed class OptiScalerReplacementService(IFileVersionInfoProvider versionInfo, OptiScalerSourceValidator sourceValidator, IDiagnosticLogger logger, string stagingRoot)
{
    // Lets a caller check a folder for multiple identified OptiScaler proxy
    // DLLs before it has an OptiInstallation to classify — e.g. before the
    // installation scanner has even run, since it excludes a folder with
    // version-conflicting proxies entirely rather than reporting it.
    public IReadOnlyList<string> DetectProxyBinaries(string directory) => OptiBinaryRules.FindProxyBinaries(directory, versionInfo);

    // Must run against freshly rescanned candidates, immediately before the
    // replacement plan is built, so both the proxy-DLL count and the version
    // family comparison reflect the current on-disk state rather than
    // whatever was true when the source file was first selected.
    public Task<OptiScalerPlanClassification> ClassifyTargetsAsync(SourceOptiScalerBinary source, IReadOnlyList<OptiInstallation> candidates, CancellationToken cancellationToken = default) =>
        Task.Run(() =>
        {
            var sourceFamily = OptiBinaryRules.DetectFamily(source.FileVersion);
            var ready = new List<OptiScalerReplacementTarget>();
            var multiProxy = new List<OptiScalerReplacementResult>();
            var mismatched = new List<OptiScalerReplacementTarget>();
            foreach (var installation in candidates)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var detected = OptiBinaryRules.FindProxyBinaries(installation.InstallDirectory, versionInfo);
                if (detected.Count > 1)
                {
                    var names = string.Join(", ", detected);
                    multiProxy.Add(new(installation.InstallDirectory, installation.GameDisplayName, names, installation.FileVersion, null, OptiScalerReplacementStatus.Skipped, OptiScalerReplacementReason.MultipleOptiScalerBinaries, $"Multiple OptiScaler DLLs were detected: {names}. Remove the unnecessary DLLs manually and scan again.", DetectedBinaryNames: detected));
                    continue;
                }
                var target = new OptiScalerReplacementTarget(installation);
                ready.Add(target);
                if (installation.SchemaFamily != sourceFamily) mismatched.Add(target);
            }
            return new OptiScalerPlanClassification(ready, multiProxy, mismatched);
        }, cancellationToken);

    public async Task<IReadOnlyList<OptiScalerReplacementResult>> ReplaceAsync(OptiScalerReplacementPlan plan, IProgress<OptiScalerReplacementProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        (string Directory, string Path, byte[] Hash) staged = default;
        var results = new List<OptiScalerReplacementResult>(plan.SkippedTargets);
        try
        {
            staged = await StageAsync(plan.Source, cancellationToken);
            for (var index = 0; index < plan.Targets.Count; index++)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    foreach (var remaining in plan.Targets.Skip(index)) results.Add(Result(remaining.Installation, OptiScalerReplacementStatus.Canceled, OptiScalerReplacementReason.Canceled, "Canceled before replacement."));
                    break;
                }
                var target = plan.Targets[index].Installation;
                progress?.Report(new(index, plan.Targets.Count, target.GameDisplayName, target.OptiBinaryFileName));
                results.Add(await ReplaceOneAsync(staged.Path, staged.Hash, target, cancellationToken));
                progress?.Report(new(index + 1, plan.Targets.Count, target.GameDisplayName, target.OptiBinaryFileName));
            }
            return results;
        }
        finally
        {
            if (staged.Directory is not null) try { Directory.Delete(staged.Directory, true); } catch (Exception ex) { logger.Error("OptiScaler update staging cleanup failed.", ex); }
        }
    }

    private async Task<(string Directory, string Path, byte[] Hash)> StageAsync(SourceOptiScalerBinary selectedSource, CancellationToken cancellationToken)
    {
        var source = sourceValidator.Validate(selectedSource.Path);
        if (!source.IsValid) throw new InvalidOperationException(source.Error ?? "Source validation failed.");
        var hash = await HashAsync(source.Source!.Path, cancellationToken);
        if (!CryptographicOperations.FixedTimeEquals(selectedSource.ContentHash, hash)) throw new InvalidOperationException("The selected source changed after it was selected.");
        string? directory = null;
        try
        {
            directory = Path.Combine(stagingRoot, "staging", "optiscaler-update", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            var stagedPath = Path.Combine(directory, "source.bin");
            await CopyAndFlushAsync(source.Source.Path, stagedPath, cancellationToken);
            var stagedHash = await HashAsync(stagedPath, cancellationToken);
            if (!CryptographicOperations.FixedTimeEquals(hash, stagedHash) || !sourceValidator.Validate(stagedPath).IsValid) throw new InvalidOperationException("The staged source could not be verified.");
            logger.Info($"OptiScaler update source staged. Version: {source.Source.FileVersion}; hash: {Convert.ToHexString(hash)}.");
            return (directory, stagedPath, hash);
        }
        catch
        {
            if (directory is not null) try { Directory.Delete(directory, true); } catch { }
            throw;
        }
    }

    private async Task<OptiScalerReplacementResult> ReplaceOneAsync(string stagedPath, byte[] sourceHash, OptiInstallation target, CancellationToken cancellationToken)
    {
        string? temporaryPath = null;
        string? rollbackPath = null;
        var replaced = false;
        var keepRollbackFile = false;

        // Once the swap below has happened, ANY exit path other than a
        // verified success (explicit mismatch or an exception thrown while
        // reading the result) must try to restore the pre-replacement file
        // before returning. A restore that fails must keep the rollback copy
        // on disk instead of losing it in the finally cleanup below.
        OptiScalerReplacementResult Recover(OptiScalerReplacementStatus status, OptiScalerReplacementReason reason, string message, Exception? exception = null)
        {
            if (!replaced || rollbackPath is null) return Result(target, status, reason, message, exception);
            if (TryRestoreRollback(rollbackPath, target.OptiBinaryPath)) message += " The previous file was restored.";
            else { keepRollbackFile = true; message += $" The previous file could not be restored; a copy was kept at: {rollbackPath}"; }
            return Result(target, status, reason, message, exception);
        }

        try
        {
            if (!File.Exists(target.OptiBinaryPath)) return Result(target, OptiScalerReplacementStatus.Skipped, OptiScalerReplacementReason.TargetMissing, "The installed OptiScaler binary is no longer available.");
            var currentMetadata = versionInfo.Read(target.OptiBinaryPath);
            if (!OptiBinaryRules.IsOptiScaler(currentMetadata)) return Result(target, OptiScalerReplacementStatus.Skipped, OptiScalerReplacementReason.TargetNotOptiScaler, "The target is no longer identified as an OptiScaler binary.");
            // Re-check the installed version family immediately before touching
            // the file: family classification (and any user confirmation of a
            // family change) happened once during preparation, but the
            // confirmation dialogs give the user arbitrary time for the
            // installed binary to change family externally in the meantime.
            // Same-family patch updates (e.g. 0.9.1 -> 0.9.9) are unaffected,
            // since only the family is compared, not the full version.
            var currentFamily = OptiBinaryRules.DetectFamily(currentMetadata);
            if (currentFamily == OptiSchemaFamily.Unsupported)
                return Result(target, OptiScalerReplacementStatus.Skipped, OptiScalerReplacementReason.TargetVersionFamilyChanged, "The installed OptiScaler version is no longer supported. Scan again and retry the replacement.");
            if (currentFamily != target.SchemaFamily)
                return Result(target, OptiScalerReplacementStatus.Skipped, OptiScalerReplacementReason.TargetVersionFamilyChanged, "The installed OptiScaler version family changed after preparation. Scan again and retry the replacement.");
            // Re-check immediately before touching the file: classification ran
            // once during preparation, but the user can spend arbitrary time on
            // the confirmation dialogs in between, during which a second
            // OptiScaler proxy could appear in this folder.
            var proxies = OptiBinaryRules.FindProxyBinaries(target.InstallDirectory, versionInfo);
            if (proxies.Count > 1)
            {
                var names = string.Join(", ", proxies);
                return Result(target, OptiScalerReplacementStatus.Skipped, OptiScalerReplacementReason.MultipleOptiScalerBinaries, $"Multiple OptiScaler DLLs were detected: {names}. Remove the unnecessary DLLs manually and scan again.", detectedBinaryNames: proxies);
            }
            using (File.Open(target.OptiBinaryPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None)) { }
            temporaryPath = UniqueTemporaryPath(target.OptiBinaryPath);
            await CopyAndFlushAsync(stagedPath, temporaryPath, cancellationToken);
            var tempFile = new FileInfo(temporaryPath);
            var temporaryHash = await HashAsync(temporaryPath, cancellationToken);
            if (tempFile.Length != new FileInfo(stagedPath).Length || !CryptographicOperations.FixedTimeEquals(sourceHash, temporaryHash) || !sourceValidator.Validate(temporaryPath).IsValid)
                return Result(target, OptiScalerReplacementStatus.Failed, OptiScalerReplacementReason.TemporaryValidationFailed, "The temporary OptiScaler file could not be verified.");

            // Keep a transient rollback copy of the pre-replacement file. It is
            // deleted before returning once it is no longer needed, so this does
            // not create the persistent backup the OptiScaler Update feature
            // intentionally omits; it only lets a failed final verification
            // restore the previous file instead of leaving the installation
            // with an unverified binary.
            cancellationToken.ThrowIfCancellationRequested();
            rollbackPath = UniqueRollbackPath(target.OptiBinaryPath);
            await CopyAndFlushAsync(target.OptiBinaryPath, rollbackPath, cancellationToken);

            ReplaceWithoutBackup(temporaryPath, target.OptiBinaryPath);
            temporaryPath = null;
            replaced = true;

            // The swap itself cannot be canceled once started; verify it with
            // CancellationToken.None so a cancellation requested in this window
            // cannot turn an actually-successful replacement into a reported
            // failure while leaving the new binary in place.
            var finalHash = File.Exists(target.OptiBinaryPath) ? await HashAsync(target.OptiBinaryPath, CancellationToken.None) : [];
            if (!File.Exists(target.OptiBinaryPath) || !CryptographicOperations.FixedTimeEquals(sourceHash, finalHash) || !OptiBinaryRules.IsOptiScaler(versionInfo.Read(target.OptiBinaryPath)))
                return Recover(OptiScalerReplacementStatus.Failed, OptiScalerReplacementReason.FinalVerificationFailed, "The final OptiScaler file could not be verified.");

            var installed = versionInfo.Read(target.OptiBinaryPath).NumericVersion;
            logger.Info($"OptiScaler binary replaced: {target.OptiBinaryPath}");
            return new(target.InstallDirectory, target.GameDisplayName, target.OptiBinaryFileName, target.FileVersion, installed, OptiScalerReplacementStatus.Replaced, OptiScalerReplacementReason.None);
        }
        catch (OperationCanceledException) { return Recover(OptiScalerReplacementStatus.Canceled, OptiScalerReplacementReason.Canceled, "Replacement was canceled."); }
        catch (UnauthorizedAccessException ex) { return Recover(OptiScalerReplacementStatus.Failed, OptiScalerReplacementReason.AccessDenied, "Access to the target file was denied.", ex); }
        catch (IOException ex) when (IsSharingViolation(ex)) { return Recover(OptiScalerReplacementStatus.Skipped, OptiScalerReplacementReason.FileInUse, "The installed OptiScaler binary is currently in use.", ex); }
        catch (Exception ex) { logger.Error($"OptiScaler replacement failed: {target.OptiBinaryPath}", ex); return Recover(OptiScalerReplacementStatus.Failed, OptiScalerReplacementReason.UnexpectedFailure, "The target could not be replaced.", ex); }
        finally
        {
            if (temporaryPath is not null) try { File.Delete(temporaryPath); } catch { }
            if (rollbackPath is not null && !keepRollbackFile) try { File.Delete(rollbackPath); } catch { }
        }
    }

    private static void ReplaceWithoutBackup(string temporaryPath, string targetPath)
    {
        try { File.Replace(temporaryPath, targetPath, destinationBackupFileName: null, ignoreMetadataErrors: true); }
        catch (PlatformNotSupportedException) { File.Move(temporaryPath, targetPath, true); }
    }
    private bool TryRestoreRollback(string rollbackPath, string targetPath)
    {
        try { File.Copy(rollbackPath, targetPath, overwrite: true); return true; }
        catch (Exception ex) { logger.Error($"OptiScaler rollback failed after verification failure: {targetPath}", ex); return false; }
    }
    private static bool IsSharingViolation(IOException exception) => exception.HResult is unchecked((int)0x80070020) or unchecked((int)0x80070021);
    private static OptiScalerReplacementResult Result(OptiInstallation target, OptiScalerReplacementStatus status, OptiScalerReplacementReason reason, string message, Exception? exception = null, IReadOnlyList<string>? detectedBinaryNames = null) => new(target.InstallDirectory, target.GameDisplayName, target.OptiBinaryFileName, target.FileVersion, null, status, reason, message, exception, detectedBinaryNames);
    private static string UniqueTemporaryPath(string targetPath) { var candidate = targetPath + ".optieditor.tmp"; return File.Exists(candidate) ? candidate + "." + Guid.NewGuid().ToString("N") : candidate; }
    private static string UniqueRollbackPath(string targetPath) { var candidate = targetPath + ".optieditor.rollback"; return File.Exists(candidate) ? candidate + "." + Guid.NewGuid().ToString("N") : candidate; }
    private static async Task CopyAndFlushAsync(string source, string target, CancellationToken token) { await using var input = File.Open(source, FileMode.Open, FileAccess.Read, FileShare.Read); await using var output = File.Open(target, FileMode.CreateNew, FileAccess.Write, FileShare.None); await input.CopyToAsync(output, token); await output.FlushAsync(token); output.Flush(flushToDisk: true); }
    private static async Task<byte[]> HashAsync(string path, CancellationToken token) { await using var stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read); return await SHA256.HashDataAsync(stream, token); }
}
