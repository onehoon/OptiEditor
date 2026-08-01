using System.Security.Cryptography;
using OptiEditor.Core.Discovery;
using OptiEditor.Core.Models;
using OptiEditor.Core.Utilities;

namespace OptiEditor.Core.OptiScalerUpdate;

public enum OptiScalerReplacementStatus { Replaced, Skipped, Failed, Canceled }
public enum OptiScalerReplacementReason { None, TargetMissing, TargetNotOptiScaler, FileInUse, AccessDenied, TemporaryValidationFailed, FinalVerificationFailed, SourceValidationFailed, Canceled, UnexpectedFailure }

public sealed record OptiScalerReplacementTarget(OptiInstallation Installation);
public sealed record OptiScalerReplacementPlan(SourceOptiScalerBinary Source, IReadOnlyList<OptiScalerReplacementTarget> Targets, IReadOnlyList<OptiScalerReplacementResult> SkippedTargets);
public sealed record OptiScalerReplacementResult(string InstallDirectory, string? GameDisplayName, string TargetFileName, Version? PreviousVersion, Version? InstalledVersion, OptiScalerReplacementStatus Status, OptiScalerReplacementReason Reason, string? UserMessage = null, Exception? InternalException = null);
public sealed record OptiScalerReplacementProgress(int Completed, int Total, string? GameDisplayName, string? TargetFileName);

public sealed class OptiScalerReplacementService(IFileVersionInfoProvider versionInfo, OptiScalerSourceValidator sourceValidator, IDiagnosticLogger logger, string stagingRoot)
{
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
        try
        {
            if (!File.Exists(target.OptiBinaryPath)) return Result(target, OptiScalerReplacementStatus.Skipped, OptiScalerReplacementReason.TargetMissing, "The installed OptiScaler binary is no longer available.");
            var currentMetadata = versionInfo.Read(target.OptiBinaryPath);
            if (!OptiBinaryRules.IsOptiScaler(currentMetadata)) return Result(target, OptiScalerReplacementStatus.Skipped, OptiScalerReplacementReason.TargetNotOptiScaler, "The target is no longer identified as an OptiScaler binary.");
            using (File.Open(target.OptiBinaryPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None)) { }
            temporaryPath = UniqueTemporaryPath(target.OptiBinaryPath);
            await CopyAndFlushAsync(stagedPath, temporaryPath, cancellationToken);
            var tempFile = new FileInfo(temporaryPath);
            var temporaryHash = await HashAsync(temporaryPath, cancellationToken);
            if (tempFile.Length != new FileInfo(stagedPath).Length || !CryptographicOperations.FixedTimeEquals(sourceHash, temporaryHash) || !sourceValidator.Validate(temporaryPath).IsValid)
                return Result(target, OptiScalerReplacementStatus.Failed, OptiScalerReplacementReason.TemporaryValidationFailed, "The temporary OptiScaler file could not be verified.");

            ReplaceWithoutBackup(temporaryPath, target.OptiBinaryPath);
            temporaryPath = null;
            var finalHash = File.Exists(target.OptiBinaryPath) ? await HashAsync(target.OptiBinaryPath, cancellationToken) : [];
            if (!File.Exists(target.OptiBinaryPath) || !CryptographicOperations.FixedTimeEquals(sourceHash, finalHash) || !OptiBinaryRules.IsOptiScaler(versionInfo.Read(target.OptiBinaryPath)))
                return Result(target, OptiScalerReplacementStatus.Failed, OptiScalerReplacementReason.FinalVerificationFailed, "The final OptiScaler file could not be verified.");
            var installed = versionInfo.Read(target.OptiBinaryPath).NumericVersion;
            logger.Info($"OptiScaler binary replaced: {target.OptiBinaryPath}");
            return new(target.InstallDirectory, target.GameDisplayName, target.OptiBinaryFileName, target.FileVersion, installed, OptiScalerReplacementStatus.Replaced, OptiScalerReplacementReason.None);
        }
        catch (UnauthorizedAccessException ex) { return Result(target, OptiScalerReplacementStatus.Failed, OptiScalerReplacementReason.AccessDenied, "Access to the target file was denied.", ex); }
        catch (IOException ex) when (IsSharingViolation(ex)) { return Result(target, OptiScalerReplacementStatus.Skipped, OptiScalerReplacementReason.FileInUse, "The installed OptiScaler binary is currently in use.", ex); }
        catch (Exception ex) { logger.Error($"OptiScaler replacement failed: {target.OptiBinaryPath}", ex); return Result(target, OptiScalerReplacementStatus.Failed, OptiScalerReplacementReason.UnexpectedFailure, "The target could not be replaced.", ex); }
        finally { if (temporaryPath is not null) try { File.Delete(temporaryPath); } catch { } }
    }

    private static void ReplaceWithoutBackup(string temporaryPath, string targetPath)
    {
        try { File.Replace(temporaryPath, targetPath, destinationBackupFileName: null, ignoreMetadataErrors: true); }
        catch (PlatformNotSupportedException) { File.Move(temporaryPath, targetPath, true); }
    }
    private static bool IsSharingViolation(IOException exception) => exception.HResult is unchecked((int)0x80070020) or unchecked((int)0x80070021);
    private static OptiScalerReplacementResult Result(OptiInstallation target, OptiScalerReplacementStatus status, OptiScalerReplacementReason reason, string message, Exception? exception = null) => new(target.InstallDirectory, target.GameDisplayName, target.OptiBinaryFileName, target.FileVersion, null, status, reason, message, exception);
    private static string UniqueTemporaryPath(string targetPath) { var candidate = targetPath + ".optieditor.tmp"; return File.Exists(candidate) ? candidate + "." + Guid.NewGuid().ToString("N") : candidate; }
    private static async Task CopyAndFlushAsync(string source, string target, CancellationToken token) { await using var input = File.Open(source, FileMode.Open, FileAccess.Read, FileShare.Read); await using var output = File.Open(target, FileMode.CreateNew, FileAccess.Write, FileShare.None); await input.CopyToAsync(output, token); await output.FlushAsync(token); output.Flush(flushToDisk: true); }
    private static async Task<byte[]> HashAsync(string path, CancellationToken token) { await using var stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read); return await SHA256.HashDataAsync(stream, token); }
}
