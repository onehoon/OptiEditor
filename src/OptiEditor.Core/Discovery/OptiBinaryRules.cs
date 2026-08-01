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

    public static OptiSchemaFamily DetectFamily(FileVersionData versionInfo) =>
        versionInfo.FileMajorPart != 0 ? OptiSchemaFamily.Unsupported : versionInfo.FileMinorPart switch
        {
            9 => OptiSchemaFamily.V09,
            10 => OptiSchemaFamily.V10,
            _ => OptiSchemaFamily.Unsupported
        };
}
