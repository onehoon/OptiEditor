using System.Globalization;
using System.Reflection;
using System.Text;

namespace OptiEditor.App.Services;

/// <summary>Writes diagnostics before the normal application logger is available.</summary>
public static class StartupDiagnostics
{
    private const string LogFilePrefix = "startup-";
    private const string LogFileExtension = ".log";
    private const string LogDateFormat = "yyyyMMdd";

    private static readonly object Gate = new();
    private static readonly string LogDirectory = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OptiEditor", "logs");
    private static readonly DateOnly LogDate = DateOnly.FromDateTime(DateTime.UtcNow);
    private static readonly string LogPath = System.IO.Path.Combine(LogDirectory, $"{LogFilePrefix}{LogDate:yyyyMMdd}{LogFileExtension}");
    private static readonly string Version = Assembly.GetEntryAssembly()?.GetName().Version?.ToString() ?? "unknown";
    private static readonly int ProcessId = Environment.ProcessId;
    private static StreamWriter? Writer;

    public static void Info(string message) => Write("INFO", message, null);
    public static void Error(string message, Exception? exception) => Write("ERROR", message, exception);

    private static void Write(string level, string message, Exception? exception)
    {
        lock (Gate)
        {
            try
            {
                var writer = GetWriter();
                writer.WriteLine($"{DateTimeOffset.UtcNow:O} [{level}] pid={ProcessId} version={Version} {message}");
                if (exception is not null) writer.WriteLine(exception);
                writer.Flush();
            }
            catch
            {
                ResetWriter();
                // Diagnostic logging must never prevent startup.
            }
        }
    }

    private static StreamWriter GetWriter()
    {
        if (Writer is not null) return Writer;

        Directory.CreateDirectory(LogDirectory);
        DeleteExpiredLogs();
        var stream = new FileStream(LogPath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete);
        Writer = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false), bufferSize: 4096);
        return Writer;
    }

    private static void DeleteExpiredLogs()
    {
        var oldestDateToKeep = LogDate.AddDays(-1);
        try
        {
            foreach (var path in Directory.EnumerateFiles(LogDirectory, $"{LogFilePrefix}*{LogFileExtension}"))
            {
                var fileName = System.IO.Path.GetFileName(path);
                var dateText = fileName[LogFilePrefix.Length..^LogFileExtension.Length];
                if (!DateOnly.TryParseExact(dateText, LogDateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var logDate)) continue;
                if (logDate < oldestDateToKeep) File.Delete(path);
            }
        }
        catch
        {
            // Log cleanup must never prevent application startup.
        }
    }

    private static void ResetWriter()
    {
        try { Writer?.Dispose(); }
        catch { /* Ignore cleanup failures in the fallback path. */ }
        Writer = null;
    }
}
