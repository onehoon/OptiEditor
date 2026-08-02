using OptiEditor.Core.Utilities;

namespace OptiEditor.Core.Storage;

public interface ISourceCommentVisibilityStore
{
    Task<bool> LoadAsync(CancellationToken cancellationToken = default);
    Task SaveAsync(bool isVisible, CancellationToken cancellationToken = default);
}

public sealed class SourceCommentVisibilityStore(string? appData = null, IDiagnosticLogger? logger = null) : ISourceCommentVisibilityStore
{
    private readonly string _path = Path.Combine(appData ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OptiEditor"), "source-comment-visibility.json");

    public Task<bool> LoadAsync(CancellationToken cancellationToken = default) => JsonFileStore.LoadAsync(_path, true, logger, cancellationToken);

    public Task SaveAsync(bool isVisible, CancellationToken cancellationToken = default) => JsonFileStore.SaveAsync(_path, isVisible, null, logger, cancellationToken);
}
