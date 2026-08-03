using System.Globalization;

namespace OptiEditor.Core.Utilities;

public interface IDiagnosticLogger { void Info(string message); void Error(string message, Exception? exception = null); }

public sealed class FileDiagnosticLogger : IDiagnosticLogger
{
    private const string LogFilePrefix = "opticeditor-";
    private const string LogFileExtension = ".log";
    private const string LogDateFormat = "yyyyMMdd";

    private readonly string _path;
    private readonly object _gate = new();

    public FileDiagnosticLogger(string? appDataDirectory = null)
    {
        var root = appDataDirectory ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OptiEditor");
        var logsDirectory = Path.Combine(root, "logs");
        Directory.CreateDirectory(logsDirectory);

        var localToday = DateOnly.FromDateTime(DateTime.Now);
        DeleteExpiredLogs(logsDirectory, localToday);
        _path = Path.Combine(logsDirectory, $"{LogFilePrefix}{localToday:yyyyMMdd}{LogFileExtension}");
    }

    public void Info(string message) => Write("INFO", message, null);
    public void Error(string message, Exception? exception = null) => Write("ERROR", message, exception);

    private static void DeleteExpiredLogs(string logsDirectory, DateOnly localToday)
    {
        var oldestDateToKeep = localToday.AddDays(-1);

        try
        {
            foreach (var path in Directory.EnumerateFiles(logsDirectory, $"{LogFilePrefix}*{LogFileExtension}"))
            {
                var fileName = Path.GetFileName(path);
                var dateText = fileName[LogFilePrefix.Length..^LogFileExtension.Length];
                if (!DateOnly.TryParseExact(dateText, LogDateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var logDate)) continue;
                if (logDate < oldestDateToKeep) File.Delete(path);
            }
        }
        catch
        {
            // Log cleanup must not prevent application startup.
        }
    }

    private void Write(string level, string message, Exception? exception)
    {
        try
        {
            lock (_gate) File.AppendAllText(_path, $"{DateTimeOffset.Now:O} [{level}] {message}{(exception is null ? "" : Environment.NewLine + exception)}{Environment.NewLine}");
        }
        catch
        {
            // Logging must not terminate the application.
        }
    }
}
