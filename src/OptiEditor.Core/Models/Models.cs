namespace OptiEditor.Core.Models;

public sealed record ScanRoot
{
    public required string Path { get; init; }
    public bool IsEnabled { get; init; } = true;
}

public enum OptiSchemaFamily { Unsupported, V09, V10 }

public sealed record OptiInstallation
{
    public required string IniPath { get; init; }
    public required string InstallDirectory { get; init; }
    public required string OptiBinaryPath { get; init; }
    public required string OptiBinaryFileName { get; init; }
    public required Version FileVersion { get; init; }
    public string? ProductVersion { get; init; }
    public required OptiSchemaFamily SchemaFamily { get; init; }
    public string? GameExePath { get; init; }
    public string? GameExeName { get; init; }
    public required string GameDisplayName { get; init; }
    public required DateTimeOffset ScannedAt { get; init; }
}

public sealed record ScanSummary
{
    public int IniFilesFound { get; init; }
    public int ValidInstallations { get; init; }
    public int SkippedInvalidBinary { get; init; }
    public int SkippedUnsupportedVersion { get; init; }
    public int ConflictingVersions { get; init; }
    public int Errors { get; init; }
}
