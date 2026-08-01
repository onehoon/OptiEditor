using OptiEditor.Core.Discovery;
using OptiEditor.Core.Models;
using OptiEditor.Core.OptiScalerUpdate;
using OptiEditor.Core.Utilities;

namespace OptiEditor.Core.Tests;

public sealed class OptiScalerUpdateTests
{
    [Theory]
    [InlineData("ProductName")]
    [InlineData("InternalName")]
    [InlineData("OriginalFilename")]
    [InlineData("FileDescription")]
    public void Source_validation_accepts_OptiScaler_identity_in_any_PE_field(string field)
    {
        var path = Path.Combine(Path.GetTempPath(), "new-file.bin");
        var metadata = VersionData() with { ProductName = field == "ProductName" ? "my OptiScaler build" : null, InternalName = field == "InternalName" ? "OPTISCALER" : null, OriginalFilename = field == "OriginalFilename" ? "custom-OptiScaler.bin" : null, FileDescription = field == "FileDescription" ? "OptiScaler proxy" : null };
        File.WriteAllBytes(path, [1]);
        try { Assert.True(new OptiScalerSourceValidator(new FixedVersionInfo(metadata)).Validate(path).IsValid); }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Source_filename_is_not_used_for_validation()
    {
        var path = Path.Combine(Path.GetTempPath(), "OptiScaler.dll"); File.WriteAllBytes(path, [1]);
        try { Assert.False(new OptiScalerSourceValidator(new FixedVersionInfo(VersionData() with { ProductName = "Other Product", InternalName = null, OriginalFilename = null, FileDescription = null })).Validate(path).IsValid); }
        finally { File.Delete(path); }
    }

    [Fact]
    public async Task Replacement_preserves_target_filename_and_creates_no_backup()
    {
        var root = Path.Combine(Path.GetTempPath(), "OptiEditorTests", Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        var source = Path.Combine(root, "source.bin"); var target = Path.Combine(root, "dxgi.dll"); await File.WriteAllBytesAsync(source, [1, 2, 3]); await File.WriteAllBytesAsync(target, [4, 5]);
        try
        {
            var metadata = VersionData(); var provider = new FixedVersionInfo(metadata); var validator = new OptiScalerSourceValidator(provider);
            var sourceInfo = Assert.IsType<SourceOptiScalerBinary>(validator.Validate(source).Source);
            var installation = new OptiInstallation { IniPath = Path.Combine(root, "OptiScaler.ini"), InstallDirectory = root, OptiBinaryPath = target, OptiBinaryFileName = "dxgi.dll", FileVersion = new Version(0, 9, 0, 1), SchemaFamily = OptiSchemaFamily.V09, GameDisplayName = "Game", ScannedAt = DateTimeOffset.UtcNow };
            var service = new OptiScalerReplacementService(provider, validator, new NullLogger(), root);
            var result = Assert.Single(await service.ReplaceAsync(new(sourceInfo, [new(installation)], [])));
            Assert.Equal(OptiScalerReplacementStatus.Replaced, result.Status); Assert.Equal("dxgi.dll", result.TargetFileName); Assert.Equal(await File.ReadAllBytesAsync(source), await File.ReadAllBytesAsync(target));
            Assert.Empty(Directory.EnumerateFiles(root, "*.bak", SearchOption.AllDirectories)); Assert.Empty(Directory.EnumerateFiles(root, "*.optieditor.tmp*", SearchOption.AllDirectories));
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public async Task Source_change_after_selection_aborts_before_any_target_is_modified()
    {
        var root = Path.Combine(Path.GetTempPath(), "OptiEditorTests", Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        var source = Path.Combine(root, "source.bin"); var target = Path.Combine(root, "version.dll"); await File.WriteAllBytesAsync(source, [1]); await File.WriteAllBytesAsync(target, [9]);
        try
        {
            var provider = new FixedVersionInfo(VersionData()); var validator = new OptiScalerSourceValidator(provider); var selectedSource = Assert.IsType<SourceOptiScalerBinary>(validator.Validate(source).Source);
            await File.WriteAllBytesAsync(source, [1, 2]);
            var installation = new OptiInstallation { IniPath = Path.Combine(root, "OptiScaler.ini"), InstallDirectory = root, OptiBinaryPath = target, OptiBinaryFileName = "version.dll", FileVersion = new Version(0, 10), SchemaFamily = OptiSchemaFamily.V10, GameDisplayName = "Game", ScannedAt = DateTimeOffset.UtcNow };
            var service = new OptiScalerReplacementService(provider, validator, new NullLogger(), root);
            await Assert.ThrowsAsync<InvalidOperationException>(() => service.ReplaceAsync(new(selectedSource, [new(installation)], [])));
            Assert.Equal(new byte[] { 9 }, await File.ReadAllBytesAsync(target));
        }
        finally { Directory.Delete(root, true); }
    }

    private static FileVersionData VersionData() => new(0, 10, 1, 0, "0.10.1", "OptiScaler", "OptiScaler", "any.bin", "OptiScaler");
    private sealed class FixedVersionInfo(FileVersionData data) : IFileVersionInfoProvider { public FileVersionData Read(string path) => data; }
    private sealed class NullLogger : IDiagnosticLogger { public void Info(string message) { } public void Error(string message, Exception? exception = null) { } }
}
