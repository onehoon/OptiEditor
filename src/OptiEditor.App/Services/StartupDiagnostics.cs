using System.Reflection;

namespace OptiEditor.App.Services;

/// <summary>Writes diagnostics before the normal application logger is available.</summary>
public static class StartupDiagnostics
{
    private static readonly object Gate = new();
    private static readonly string Path = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OptiEditor", "logs", $"startup-{DateTime.UtcNow:yyyyMMdd}.log");

    public static void Info(string message) => Write("INFO", message, null);
    public static void Error(string message, Exception? exception) => Write("ERROR", message, exception);

    private static void Write(string level, string message, Exception? exception)
    {
        try
        {
            lock (Gate)
            {
                Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path)!);
                var version = Assembly.GetEntryAssembly()?.GetName().Version?.ToString() ?? "unknown";
                File.AppendAllText(Path, $"{DateTimeOffset.UtcNow:O} [{level}] pid={Environment.ProcessId} version={version} {message}{Environment.NewLine}{(exception is null ? "" : exception + Environment.NewLine)}");
            }
        }
        catch { /* Diagnostic logging must never prevent startup. */ }
    }
}
