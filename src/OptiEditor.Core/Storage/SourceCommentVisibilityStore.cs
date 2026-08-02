using System.Collections.Concurrent;
using System.Text.Json;

namespace OptiEditor.Core.Storage;

public interface ISourceCommentVisibilityStore
{
    Task<bool> LoadAsync(CancellationToken cancellationToken = default);
    Task SaveAsync(bool isVisible, CancellationToken cancellationToken = default);
}

public sealed class SourceCommentVisibilityStore(string? appData = null) : ISourceCommentVisibilityStore
{
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> Locks = new(StringComparer.OrdinalIgnoreCase);
    private readonly string _path = Path.Combine(appData ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OptiEditor"), "source-comment-visibility.json");

    public async Task<bool> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_path)) return true;
        await using var stream = File.Open(_path, FileMode.Open, FileAccess.Read, FileShare.Read);
        return await JsonSerializer.DeserializeAsync<bool>(stream, cancellationToken: cancellationToken);
    }

    public async Task SaveAsync(bool isVisible, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        var gate = Locks.GetOrAdd(_path, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken);
        var temporary = _path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            await using (var stream = File.Create(temporary)) await JsonSerializer.SerializeAsync(stream, isVisible, cancellationToken: cancellationToken);
            File.Move(temporary, _path, true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); gate.Release(); }
    }
}
