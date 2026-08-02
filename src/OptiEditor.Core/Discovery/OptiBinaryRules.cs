using OptiEditor.Core.Models;

namespace OptiEditor.Core.Discovery;

public static class OptiBinaryRules
{
    private static readonly string[] Priority = ["dxgi.dll", "winmm.dll", "version.dll", "dbghelp.dll", "d3d12.dll", "wininet.dll", "winhttp.dll", "OptiScaler.asi"];
    public static IReadOnlyList<string> CandidateNames => Priority;

    public static bool IsApprovedProxyName(string? fileName) =>
        !string.IsNullOrWhiteSpace(fileName) && Priority.Contains(fileName, StringComparer.OrdinalIgnoreCase);

    public static bool IsOptiScaler(FileVersionData? value)
    {
        if (value is null) return false;
        return new[] { value.ProductName, value.InternalName, value.OriginalFilename, value.FileDescription }
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x!.Trim())
            .Any(x => x.Contains("OptiScaler", StringComparison.OrdinalIgnoreCase));
    }

    public static OptiSchemaFamily DetectFamily(FileVersionData versionInfo) => DetectFamily(versionInfo.NumericVersion);

    public static OptiSchemaFamily DetectFamily(Version version) =>
        version.Major != 0 ? OptiSchemaFamily.Unsupported : version.Minor switch
        {
            9 => OptiSchemaFamily.V09,
            10 => OptiSchemaFamily.V10,
            _ => OptiSchemaFamily.Unsupported
        };

    // Top-level only, matching the scope of installation discovery: does not
    // recurse into subdirectories. Uses the same PE-identity check as the
    // rest of OptiEditor rather than trusting candidate filenames alone, so a
    // same-named non-OptiScaler DLL (a different mod, a game DLL) is not
    // mistaken for a second OptiScaler proxy.
    public static IReadOnlyList<string> FindProxyBinaries(string directory, IFileVersionInfoProvider versionInfo)
    {
        var found = new List<string>();
        foreach (var name in Priority)
        {
            var path = Path.Combine(directory, name);
            if (!File.Exists(path)) continue;
            try { if (IsOptiScaler(versionInfo.Read(path))) found.Add(name); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException or System.Security.SecurityException) { }
        }
        return found;
    }
}
