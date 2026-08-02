using OptiEditor.Core.Models;
using OptiEditor.Core.Utilities;

namespace OptiEditor.Core.Discovery;

public sealed record DiscoveryResult(IReadOnlyList<OptiInstallation> Installations, ScanSummary Summary);

public sealed class InstallationDiscoveryScanner(IFileVersionInfoProvider versionInfo, IDiagnosticLogger logger)
{
    public Task<DiscoveryResult> ScanAsync(IEnumerable<ScanRoot> roots, CancellationToken cancellationToken = default) =>
        Task.Run(() => Scan(roots, cancellationToken), cancellationToken);

    public Task<OptiInstallation?> ScanDirectoryAsync(string installDirectory, CancellationToken cancellationToken = default) =>
        Task.Run(() => ScanDirectory(installDirectory, cancellationToken), cancellationToken);

    private DiscoveryResult Scan(IEnumerable<ScanRoot> roots, CancellationToken token)
    {
        logger.Info("Scan started.");
        var iniPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var errors = 0;
        foreach (var root in roots.Where(x => x.IsEnabled))
        {
            try { FindIniFiles(root.Path, iniPaths, token, ref errors); }
            catch (Exception ex) when (IsExpectedFileSystemError(ex)) { errors++; logger.Error($"Scan root failed: {root.Path}", ex); }
        }
        var installations = new List<OptiInstallation>();
        var invalid = 0; var unsupported = 0; var conflicts = 0;
        foreach (var ini in iniPaths)
        {
            token.ThrowIfCancellationRequested();
            var outcome = TryCreateInstallation(ini, ref errors);
            if (outcome.Conflict) { conflicts++; continue; }
            if (outcome.Unsupported) { unsupported++; continue; }
            if (outcome.Installation is null) { invalid++; continue; }
            installations.Add(outcome.Installation);
        }
        var summary = new ScanSummary { IniFilesFound = iniPaths.Count, ValidInstallations = installations.Count, SkippedInvalidBinary = invalid, SkippedUnsupportedVersion = unsupported, ConflictingVersions = conflicts, Errors = errors };
        logger.Info($"Scan completed. Valid installations: {summary.ValidInstallations}; errors: {summary.Errors}.");
        return new(installations, summary);
    }

    private void FindIniFiles(string root, HashSet<string> results, CancellationToken token, ref int errors)
    {
        var directories = new Stack<string>(); directories.Push(Path.GetFullPath(root));
        while (directories.Count > 0)
        {
            token.ThrowIfCancellationRequested(); var directory = directories.Pop();
            try
            {
                var iniPath = Directory.EnumerateFiles(directory, "*", SearchOption.TopDirectoryOnly)
                    .FirstOrDefault(file => string.Equals(Path.GetFileName(file), "OptiScaler.ini", StringComparison.OrdinalIgnoreCase));
                if (iniPath is not null)
                {
                    var normalizedIniPath = Path.GetFullPath(iniPath);
                    results.Add(normalizedIniPath);
                    if (HasSupportedOptiScalerBinary(directory, ref errors))
                    {
                        logger.Info($"Valid OptiScaler installation found; child directories will not be scanned: {directory}");
                        continue;
                    }
                }
                // Reparse points (junctions/symlinks) can point back at an ancestor
                // directory and loop the scan forever; do not descend into them.
                // A root the user added directly is still scanned even if it is
                // itself a reparse point.
                foreach (var child in Directory.EnumerateDirectories(directory, "*", SearchOption.TopDirectoryOnly))
                    if (!new DirectoryInfo(child).Attributes.HasFlag(FileAttributes.ReparsePoint)) directories.Push(child);
            }
            catch (Exception ex) when (IsExpectedFileSystemError(ex)) { errors++; logger.Error($"Directory skipped: {directory}", ex); }
        }
    }

    private bool HasSupportedOptiScalerBinary(string directory, ref int errors)
    {
        var validVersions = new List<FileVersionData>();
        foreach (var name in OptiBinaryRules.CandidateNames)
        {
            var path = Path.Combine(directory, name);
            if (!File.Exists(path)) continue;
            try
            {
                var info = versionInfo.Read(path);
                if (OptiBinaryRules.IsOptiScaler(info)) validVersions.Add(info);
            }
            catch (Exception ex) when (IsExpectedFileSystemError(ex))
            {
                errors++;
                logger.Error($"PE version read failed while determining scan boundary: {path}", ex);
            }
        }
        return validVersions.Count > 0
               && validVersions.Select(x => x.NumericVersion).Distinct().Take(2).Count() == 1
               && OptiBinaryRules.DetectFamily(validVersions[0]) != OptiSchemaFamily.Unsupported;
    }

    private (OptiInstallation? Installation, bool Unsupported, bool Conflict) TryCreateInstallation(string iniPath, ref int errors)
    {
        var directory = Path.GetDirectoryName(iniPath)!;
        var candidates = new List<(string Path, FileVersionData Version)>();
        foreach (var name in OptiBinaryRules.CandidateNames)
        {
            var path = Path.Combine(directory, name); if (!File.Exists(path)) continue;
            try { var info = versionInfo.Read(path); if (OptiBinaryRules.IsOptiScaler(info)) candidates.Add((Path.GetFullPath(path), info)); }
            catch (Exception ex) when (IsExpectedFileSystemError(ex)) { errors++; logger.Error($"PE version read failed: {path}", ex); }
        }
        if (candidates.Count == 0) { logger.Info($"INI skipped; no valid OptiScaler proxy: {iniPath}"); return (null, false, false); }
        if (candidates.Select(x => x.Version.NumericVersion).Distinct().Skip(1).Any()) { logger.Info($"INI skipped; conflicting proxy versions: {iniPath}"); return (null, false, true); }
        var selected = candidates[0]; var family = OptiBinaryRules.DetectFamily(selected.Version);
        if (family == OptiSchemaFamily.Unsupported) { logger.Info($"INI skipped; unsupported OptiScaler version {selected.Version.NumericVersion}: {iniPath}"); return (null, true, false); }
        var game = DetectGameExecutable(directory, ref errors);
        return (new() { IniPath = Path.GetFullPath(iniPath), InstallDirectory = Path.GetFullPath(directory), OptiBinaryPath = selected.Path, OptiBinaryFileName = Path.GetFileName(selected.Path), FileVersion = selected.Version.NumericVersion, ProductVersion = selected.Version.ProductVersion, SchemaFamily = family, GameExePath = game.Path, GameExeName = game.FileName, GameDisplayName = game.DisplayName, ScannedAt = DateTimeOffset.UtcNow }, false, false);
    }

    private OptiInstallation? ScanDirectory(string installDirectory, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        var fullDirectory = Path.GetFullPath(installDirectory);
        try
        {
            var iniPath = Directory.EnumerateFiles(fullDirectory, "*", SearchOption.TopDirectoryOnly)
                .FirstOrDefault(file => string.Equals(Path.GetFileName(file), "OptiScaler.ini", StringComparison.OrdinalIgnoreCase));
            if (iniPath is null) return null;
            var errors = 0;
            var outcome = TryCreateInstallation(iniPath, ref errors);
            return outcome.Installation;
        }
        catch (Exception ex) when (IsExpectedFileSystemError(ex))
        {
            logger.Error($"Directory rescan failed: {fullDirectory}", ex);
            return null;
        }
    }

    private GameExecutable DetectGameExecutable(string directory, ref int errors)
    {
        var candidates = new List<GameExecutableCandidate>();
        try { foreach (var path in Directory.EnumerateFiles(directory, "*.exe", SearchOption.TopDirectoryOnly))
        {
            try { var metadata = versionInfo.Read(path); candidates.Add(new(path, new FileInfo(path).Length, metadata.ProductName)); }
            catch (Exception ex) when (IsExpectedFileSystemError(ex)) { errors++; logger.Error($"Game EXE detection failed: {path}", ex); }
        } }
        catch (Exception ex) when (IsExpectedFileSystemError(ex)) { errors++; logger.Error($"Game EXE enumeration failed: {directory}", ex); }
        return GameExecutableDetector.Select(candidates);
    }

    private static bool IsExpectedFileSystemError(Exception ex) => ex is UnauthorizedAccessException or IOException or ArgumentException or NotSupportedException or System.Security.SecurityException;
}
