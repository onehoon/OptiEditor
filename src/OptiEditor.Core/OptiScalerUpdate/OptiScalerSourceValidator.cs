using OptiEditor.Core.Discovery;
using OptiEditor.Core.Models;
using System.Security.Cryptography;

namespace OptiEditor.Core.OptiScalerUpdate;

public sealed record SourceOptiScalerBinary(string Path, Version FileVersion, string? ProductVersion, long FileSize, byte[] ContentHash);

public sealed record SourceValidationResult(SourceOptiScalerBinary? Source, string? Error)
{
    public bool IsValid => Source is not null;
}

public sealed class OptiScalerSourceValidator(IFileVersionInfoProvider versionInfo)
{
    public SourceValidationResult Validate(string path)
    {
        try
        {
            var fullPath = System.IO.Path.GetFullPath(path);
            var file = new FileInfo(fullPath);
            if (!file.Exists || file.Length == 0) return new(null, "The selected file does not contain readable version information.");
            using (File.Open(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read)) { }
            var metadata = versionInfo.Read(fullPath);
            if (!OptiBinaryRules.IsOptiScaler(metadata)) return new(null, "The selected file was not identified as an OptiScaler binary.");
            if (!metadata.HasReadableNumericVersion) return new(null, "The selected file does not contain readable numeric version information.");
            var version = metadata.NumericVersion;
            // A source whose family OptiEditor does not support (e.g. 0.8, 0.11)
            // must never reach the replacement flow: DetectFamily(Unsupported)
            // there would otherwise get silently mislabeled as "0.10" and
            // replace targets it isn't actually compatible with.
            if (OptiBinaryRules.DetectFamily(version) == OptiSchemaFamily.Unsupported) return new(null, $"OptiScaler {version} is not supported. Select a 0.9.x or 0.10.x binary.");
            using var hashStream = File.Open(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            return new(new(fullPath, version, metadata.ProductVersion, file.Length, SHA256.HashData(hashStream)), null);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or System.Security.SecurityException)
        {
            return new(null, "The selected file does not contain readable version information.");
        }
    }
}
