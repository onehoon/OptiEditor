namespace OptiEditor.Core.Utilities;

public interface IAtomicFileReplacer { Task ReplaceAsync(string temporaryPath, string destinationPath, CancellationToken cancellationToken); }
public sealed class AtomicFileReplacer : IAtomicFileReplacer
{
    public Task ReplaceAsync(string temporaryPath, string destinationPath, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try { File.Replace(temporaryPath, destinationPath, destinationBackupFileName: null, ignoreMetadataErrors: true); }
        catch (PlatformNotSupportedException) { File.Move(temporaryPath, destinationPath, true); }
        return Task.CompletedTask;
    }
}
