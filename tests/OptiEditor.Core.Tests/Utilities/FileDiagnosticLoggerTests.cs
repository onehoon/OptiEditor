using OptiEditor.Core.Utilities;

namespace OptiEditor.Core.Tests.Utilities;

public sealed class FileDiagnosticLoggerTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"OptiEditorTests-{Guid.NewGuid():N}");

    [Fact]
    public void Constructor_DeletesLogsOlderThanYesterday()
    {
        var logsDirectory = Path.Combine(_root, "logs");
        Directory.CreateDirectory(logsDirectory);
        var today = DateOnly.FromDateTime(DateTime.Now);
        var todayPath = CreateLog(logsDirectory, today);
        var yesterdayPath = CreateLog(logsDirectory, today.AddDays(-1));
        var expiredPath = CreateLog(logsDirectory, today.AddDays(-2));

        _ = new FileDiagnosticLogger(_root);

        Assert.True(File.Exists(todayPath));
        Assert.True(File.Exists(yesterdayPath));
        Assert.False(File.Exists(expiredPath));
    }

    [Fact]
    public void Constructor_LeavesFilesOutsideTheManagedLogPatternUntouched()
    {
        var logsDirectory = Path.Combine(_root, "logs");
        Directory.CreateDirectory(logsDirectory);
        var unrelatedPath = Path.Combine(logsDirectory, "other.log");
        var malformedPath = Path.Combine(logsDirectory, "opticeditor-invalid.log");
        File.WriteAllText(unrelatedPath, "unrelated");
        File.WriteAllText(malformedPath, "malformed");

        _ = new FileDiagnosticLogger(_root);

        Assert.True(File.Exists(unrelatedPath));
        Assert.True(File.Exists(malformedPath));
    }

    [Fact]
    public void Info_WritesToTheLocalCalendarDateLog()
    {
        var logger = new FileDiagnosticLogger(_root);
        var today = DateOnly.FromDateTime(DateTime.Now);
        var expectedPath = Path.Combine(_root, "logs", $"opticeditor-{today:yyyyMMdd}.log");

        logger.Info("test message");

        Assert.Contains("[INFO] test message", File.ReadAllText(expectedPath));
    }

    private static string CreateLog(string logsDirectory, DateOnly date)
    {
        var path = Path.Combine(logsDirectory, $"opticeditor-{date:yyyyMMdd}.log");
        File.WriteAllText(path, "test");
        return path;
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }
}
