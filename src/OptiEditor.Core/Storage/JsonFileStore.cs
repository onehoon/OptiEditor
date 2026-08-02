using System.Collections.Concurrent;
using System.Text.Json;
using OptiEditor.Core.Utilities;

namespace OptiEditor.Core.Storage;

// Shared atomic-save + corrupt-JSON-recovery behavior for the small per-user
// settings files under %LocalAppData%\OptiEditor. A corrupt file must never
// crash the app at startup; it is moved aside and the caller falls back to
// its default value.
internal static class JsonFileStore
{
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> Locks = new(StringComparer.OrdinalIgnoreCase);

    public static async Task<T> LoadAsync<T>(string path, T fallback, IDiagnosticLogger? logger, CancellationToken cancellationToken = default)
    {
        // Shares SaveAsync's per-path gate so a load can never observe a file
        // mid-write, and a corrupt read can never quarantine a file that a
        // concurrent save just replaced with valid content.
        var gate = Locks.GetOrAdd(path, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken);
        try
        {
            if (!File.Exists(path)) return fallback;
            try
            {
                await using var stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read);
                var value = await JsonSerializer.DeserializeAsync<T>(stream, cancellationToken: cancellationToken);
                return value ?? fallback;
            }
            catch (JsonException ex)
            {
                MoveAside(path, ex, logger);
                return fallback;
            }
        }
        finally { gate.Release(); }
    }

    public static async Task SaveAsync<T>(string path, T value, JsonSerializerOptions? options, IDiagnosticLogger? logger = null, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var gate = Locks.GetOrAdd(path, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken);
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            await using (var stream = File.Create(temporary)) await JsonSerializer.SerializeAsync(stream, value, options, cancellationToken);
            File.Move(temporary, path, true);
        }
        finally
        {
            // A cleanup failure (locked by an indexer/AV scan, permissions...)
            // must never skip releasing the gate below it, or every later save
            // to this same path hangs forever waiting on it.
            try { if (File.Exists(temporary)) File.Delete(temporary); }
            catch (Exception ex) { logger?.Error($"Temporary file cleanup failed for {Path.GetFileName(path)}.", ex); }
            finally { gate.Release(); }
        }
    }

    private static void MoveAside(string path, Exception ex, IDiagnosticLogger? logger)
    {
        try
        {
            var invalid = Path.Combine(Path.GetDirectoryName(path)!, $"{Path.GetFileNameWithoutExtension(path)}.invalid-{DateTime.UtcNow:yyyyMMddHHmmss}{Path.GetExtension(path)}");
            File.Move(path, invalid, true);
            logger?.Error($"Invalid {Path.GetFileName(path)} was moved aside.", ex);
        }
        catch (Exception recoveryException) when (recoveryException is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            logger?.Error($"Invalid {Path.GetFileName(path)} could not be moved aside.", recoveryException);
        }
    }
}
