namespace OptiEditor.Core.Utilities;

public interface IDiagnosticLogger { void Info(string message); void Error(string message, Exception? exception = null); }

public sealed class FileDiagnosticLogger : IDiagnosticLogger
{
    private readonly string _path;
    private readonly object _gate = new();
    public FileDiagnosticLogger(string? appDataDirectory = null)
    {
        var root = appDataDirectory ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OptiEditor");
        Directory.CreateDirectory(Path.Combine(root, "logs"));
        _path = Path.Combine(root, "logs", $"opticeditor-{DateTime.UtcNow:yyyyMMdd}.log");
    }
    public void Info(string message) => Write("INFO", message, null);
    public void Error(string message, Exception? exception = null) => Write("ERROR", message, exception);
    private void Write(string level, string message, Exception? exception)
    {
        lock (_gate) File.AppendAllText(_path, $"{DateTimeOffset.UtcNow:O} [{level}] {message}{(exception is null ? "" : $" :: {exception.Message}")}{Environment.NewLine}");
    }
}
