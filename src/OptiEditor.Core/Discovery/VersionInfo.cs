using System.Diagnostics;

namespace OptiEditor.Core.Discovery;

public sealed record FileVersionData(
    int FileMajorPart, int FileMinorPart, int FileBuildPart, int FilePrivatePart,
    string? ProductVersion, string? ProductName, string? InternalName,
    string? OriginalFilename, string? FileDescription)
{
    public Version NumericVersion => new(FileMajorPart, FileMinorPart, Math.Max(0, FileBuildPart), Math.Max(0, FilePrivatePart));
}

public interface IFileVersionInfoProvider { FileVersionData Read(string path); }

public sealed class SystemFileVersionInfoProvider : IFileVersionInfoProvider
{
    public FileVersionData Read(string path)
    {
        var v = FileVersionInfo.GetVersionInfo(path);
        return new(v.FileMajorPart, v.FileMinorPart, v.FileBuildPart, v.FilePrivatePart,
            v.ProductVersion, v.ProductName, v.InternalName, v.OriginalFilename, v.FileDescription);
    }
}
