namespace OptiEditor.Core.Utilities;

public interface IAtomicFileReplacer { Task ReplaceAsync(string temporaryPath, string destinationPath, CancellationToken cancellationToken); }
public sealed class AtomicFileReplacer : IAtomicFileReplacer
{
    public Task ReplaceAsync(string temporaryPath, string destinationPath, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        File.Move(temporaryPath, destinationPath, true);
        return Task.CompletedTask;
    }
}
