using System.Reflection;
using System.Text;

namespace OptiEditor.App.Services;

/// <summary>Writes diagnostics before the normal application logger is available.</summary>
public static class StartupDiagnostics
{
    private static readonly object Gate = new();
    private static readonly string LogDirectory = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OptiEditor", "logs");
    private static readonly string LogPath = System.IO.Path.Combine(LogDirectory, $"startup-{DateTime.UtcNow:yyyyMMdd}.log");
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
        var stream = new FileStream(LogPath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete);
        Writer = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false), bufferSize: 4096);
        return Writer;
    }

    private static void ResetWriter()
    {
        try { Writer?.Dispose(); }
        catch { /* Ignore cleanup failures in the fallback path. */ }
        Writer = null;
    }
}
