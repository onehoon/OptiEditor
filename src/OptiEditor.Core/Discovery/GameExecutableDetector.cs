namespace OptiEditor.Core.Discovery;

public sealed record GameExecutableCandidate(string Path, long Length, string? ProductName);
public sealed record GameExecutable(string? Path, string? FileName, string DisplayName);

public static class GameExecutableDetector
{
    public static GameExecutable Select(IEnumerable<GameExecutableCandidate> candidates)
    {
        var selected = candidates
            .OrderByDescending(x => !string.IsNullOrWhiteSpace(x.ProductName))
            .ThenByDescending(x => x.Length)
            .ThenBy(x => System.IO.Path.GetFileName(x.Path), StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();
        if (selected is null) return new(null, null, "Unknown Game");
        var fileName = System.IO.Path.GetFileName(selected.Path);
        return new(selected.Path, fileName, string.IsNullOrWhiteSpace(selected.ProductName) ? System.IO.Path.GetFileNameWithoutExtension(fileName) : selected.ProductName.Trim());
    }
}
