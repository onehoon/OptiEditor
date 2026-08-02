namespace OptiEditor.Core.Discovery;

public sealed record GameExecutableCandidate(string Path, long Length, string? ProductName);
public sealed record GameExecutable(string? Path, string? FileName, string DisplayName);

public static class GameExecutableDetector
{
    private static readonly HashSet<string> ExcludedFileNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "EpicOnlineServicesInstaller.exe",
        "DLSSTweaksConfigTool.exe",
        "VC_redist.x64.exe",
        "UnityCrashHandler64.exe",
        "CCrashReport.exe",
        "InstallerMessage.exe",
        "crs-handler.exe",
        "crs-uploader.exe",
        "REDEngineErrorReporter.exe",
        "BugSplatHD64.exe",
        "BsSndRpt64.exe",
        "crs-video.exe",
        "launcher.exe"
    };

    public static bool IsExcluded(string? path) =>
        !string.IsNullOrWhiteSpace(path) && ExcludedFileNames.Contains(System.IO.Path.GetFileName(path));

    public static GameExecutable Select(IEnumerable<GameExecutableCandidate> candidates)
    {
        var selected = candidates
            .Where(x => !IsExcluded(x.Path))
            .OrderByDescending(x => GameMasterNameMap.Find(x.Path) is not null)
            .ThenByDescending(x => !string.IsNullOrWhiteSpace(x.ProductName))
            .ThenByDescending(x => x.Length)
            .ThenBy(x => System.IO.Path.GetFileName(x.Path), StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();
        if (selected is null) return new(null, null, "Unknown Game");
        var fileName = System.IO.Path.GetFileName(selected.Path);
        return new(selected.Path, fileName, GameMasterNameMap.Find(fileName) ?? (string.IsNullOrWhiteSpace(selected.ProductName) ? System.IO.Path.GetFileNameWithoutExtension(fileName) : selected.ProductName.Trim()));
    }
}
