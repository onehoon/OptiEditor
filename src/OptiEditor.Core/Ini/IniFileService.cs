using System.Collections.Concurrent;
using System.Security.Cryptography;
using OptiEditor.Core.Utilities;

namespace OptiEditor.Core.Ini;

public interface IIniBackupService { Task<string> CreateBackupAsync(string sourcePath, CancellationToken cancellationToken); }
public sealed class IniBackupService : IIniBackupService
{
    public async Task<string> CreateBackupAsync(string sourcePath, CancellationToken cancellationToken)
    {
        var backup = sourcePath + ".optieditor.bak"; var pending = backup + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try { await using (var source = File.OpenRead(sourcePath)) await using (var target = File.Create(pending)) await source.CopyToAsync(target, cancellationToken); File.Move(pending, backup, true); return backup; }
        finally { if (File.Exists(pending)) File.Delete(pending); }
    }
}
public interface IIniFileService
{
    Task<IniLoadResult> LoadAsync(string path, CancellationToken cancellationToken = default);
    Task<IniSaveResult> SaveAsync(IniDocument document, IniFileSnapshot expectedSnapshot, CancellationToken cancellationToken = default);
}
public sealed class IniFileService(IIniBackupService? backups = null, IAtomicFileReplacer? replacer = null) : IIniFileService
{
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> Locks = new(StringComparer.OrdinalIgnoreCase);
    private readonly IIniBackupService _backups = backups ?? new IniBackupService();
    private readonly IAtomicFileReplacer _replacer = replacer ?? new AtomicFileReplacer();
    public async Task<IniLoadResult> LoadAsync(string path, CancellationToken cancellationToken = default)
    {
        var fullPath = Path.GetFullPath(path); if (!File.Exists(fullPath)) throw new FileNotFoundException("The INI file was not found.", fullPath);
        var bytes = await File.ReadAllBytesAsync(fullPath, cancellationToken); var detected = FileEncodingDetector.Detect(bytes);
        var offset = detected.HasBom ? detected.Encoding.GetPreamble().Length : 0;
        var document = IniParser.Parse(detected.Encoding.GetString(bytes, offset, bytes.Length - offset), detected.Encoding, detected.HasBom);
        return new(document, await CaptureSnapshotAsync(fullPath, bytes, cancellationToken), detected.Encoding, detected.HasBom, document.DominantLineEnding, document.Diagnostics);
    }
    public async Task<IniSaveResult> SaveAsync(IniDocument document, IniFileSnapshot expectedSnapshot, CancellationToken cancellationToken = default)
    {
        var path = Path.GetFullPath(expectedSnapshot.FullPath); var gate = Locks.GetOrAdd(path, _ => new SemaphoreSlim(1, 1)); await gate.WaitAsync(cancellationToken);
        string? temporary = null;
        try
        {
            var current = await CaptureSnapshotAsync(path, null, cancellationToken);
            if (!string.Equals(current.ContentHash, expectedSnapshot.ContentHash, StringComparison.Ordinal)) throw new IniFileChangedExternallyException();
            temporary = path + ".optieditor." + Guid.NewGuid().ToString("N") + ".tmp";
            var bytes = document.RenderBytes();
            await using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough)) { await stream.WriteAsync(bytes, cancellationToken); await stream.FlushAsync(cancellationToken); }
            var verification = await LoadAsync(temporary, cancellationToken);
            foreach (var key in document.ModifiedKeys)
                if (!string.Equals(verification.Document.GetRawValue(key), document.GetRawValue(key), StringComparison.Ordinal)) throw new IniSaveException("Temporary INI verification failed.");
            // The source can change while the temporary copy is validated. Check
            // the original snapshot again immediately before the irreversible swap.
            var finalCurrent = await CaptureSnapshotAsync(path, null, cancellationToken);
            if (!string.Equals(finalCurrent.ContentHash, expectedSnapshot.ContentHash, StringComparison.Ordinal)) throw new IniFileChangedExternallyException();
            var backup = await _backups.CreateBackupAsync(path, cancellationToken);
            var afterBackup = await CaptureSnapshotAsync(path, null, cancellationToken);
            if (!string.Equals(afterBackup.ContentHash, expectedSnapshot.ContentHash, StringComparison.Ordinal)) throw new IniFileChangedExternallyException();
            cancellationToken.ThrowIfCancellationRequested(); await _replacer.ReplaceAsync(temporary, path, cancellationToken); temporary = null;
            try { File.Delete(backup); } catch { /* best-effort cleanup; save already succeeded */ }
            var snapshot = await CaptureSnapshotAsync(path, null, cancellationToken);
            return new(true, backup, snapshot, document.ModifiedKeys.ToArray(), null);
        }
        catch (IniFileChangedExternallyException ex) { return new(false, null, null, [], ex.Message); }
        catch (OperationCanceledException) { return new(false, null, null, [], "Save was cancelled."); }
        catch (Exception) { return new(false, null, null, [], "The INI file could not be saved safely."); }
        finally
        {
            if (temporary is not null) try { if (File.Exists(temporary)) File.Delete(temporary); } catch { /* best-effort cleanup; must not skip releasing the gate below */ }
            gate.Release();
        }
    }
    internal static async Task<IniFileSnapshot> CaptureSnapshotAsync(string path, byte[]? content, CancellationToken cancellationToken)
    {
        var fullPath = Path.GetFullPath(path); content ??= await File.ReadAllBytesAsync(fullPath, cancellationToken); var info = new FileInfo(fullPath);
        return new(fullPath, info.Length, info.LastWriteTimeUtc, Convert.ToHexString(SHA256.HashData(content)));
    }
}
