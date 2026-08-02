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
    public async Task Final_verification_failure_restores_the_previous_target_file()
    {
        var root = Path.Combine(Path.GetTempPath(), "OptiEditorTests", Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        var source = Path.Combine(root, "source.bin"); var target = Path.Combine(root, "dxgi.dll");
        var originalBytes = new byte[] { 4, 5, 6 };
        await File.WriteAllBytesAsync(source, [1, 2, 3]); await File.WriteAllBytesAsync(target, originalBytes);
        try
        {
            var provider = new FlakyVersionInfo(VersionData()) { TargetPath = target };
            var validator = new OptiScalerSourceValidator(provider);
            var sourceInfo = Assert.IsType<SourceOptiScalerBinary>(validator.Validate(source).Source);
            var installation = new OptiInstallation { IniPath = Path.Combine(root, "OptiScaler.ini"), InstallDirectory = root, OptiBinaryPath = target, OptiBinaryFileName = "dxgi.dll", FileVersion = new Version(0, 9, 0, 1), SchemaFamily = OptiSchemaFamily.V09, GameDisplayName = "Game", ScannedAt = DateTimeOffset.UtcNow };
            var service = new OptiScalerReplacementService(provider, validator, new NullLogger(), root);
            var result = Assert.Single(await service.ReplaceAsync(new(sourceInfo, [new(installation)], [])));
            Assert.Equal(OptiScalerReplacementStatus.Failed, result.Status);
            Assert.Equal(OptiScalerReplacementReason.FinalVerificationFailed, result.Reason);
            Assert.Equal(originalBytes, await File.ReadAllBytesAsync(target));
            Assert.Empty(Directory.EnumerateFiles(root, "*.optieditor.rollback*", SearchOption.AllDirectories));
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public async Task Final_verification_exception_after_replace_still_restores_the_previous_target_file()
    {
        var root = Path.Combine(Path.GetTempPath(), "OptiEditorTests", Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        var source = Path.Combine(root, "source.bin"); var target = Path.Combine(root, "dxgi.dll");
        var originalBytes = new byte[] { 4, 5, 6 };
        await File.WriteAllBytesAsync(source, [1, 2, 3]); await File.WriteAllBytesAsync(target, originalBytes);
        try
        {
            var provider = new ThrowingOnSecondReadVersionInfo(VersionData()) { TargetPath = target };
            var validator = new OptiScalerSourceValidator(provider);
            var sourceInfo = Assert.IsType<SourceOptiScalerBinary>(validator.Validate(source).Source);
            var installation = new OptiInstallation { IniPath = Path.Combine(root, "OptiScaler.ini"), InstallDirectory = root, OptiBinaryPath = target, OptiBinaryFileName = "dxgi.dll", FileVersion = new Version(0, 9, 0, 1), SchemaFamily = OptiSchemaFamily.V09, GameDisplayName = "Game", ScannedAt = DateTimeOffset.UtcNow };
            var service = new OptiScalerReplacementService(provider, validator, new NullLogger(), root);
            var result = Assert.Single(await service.ReplaceAsync(new(sourceInfo, [new(installation)], [])));
            Assert.Equal(OptiScalerReplacementStatus.Failed, result.Status);
            Assert.Equal(OptiScalerReplacementReason.UnexpectedFailure, result.Reason);
            Assert.Contains("restored", result.UserMessage, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(originalBytes, await File.ReadAllBytesAsync(target));
            Assert.Empty(Directory.EnumerateFiles(root, "*.optieditor.rollback*", SearchOption.AllDirectories));
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

    [Fact]
    public async Task ClassifyTargets_flags_mismatch_only_across_version_families()
    {
        var root = Path.Combine(Path.GetTempPath(), "OptiEditorTests", Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        var source = Path.Combine(root, "source.bin"); File.WriteAllBytes(source, [1]);
        var sameFamilyDir = Directory.CreateDirectory(Path.Combine(root, "same")).FullName; File.WriteAllBytes(Path.Combine(sameFamilyDir, "dxgi.dll"), [2]);
        var differentFamilyDir = Directory.CreateDirectory(Path.Combine(root, "different")).FullName; File.WriteAllBytes(Path.Combine(differentFamilyDir, "dxgi.dll"), [3]);
        try
        {
            // The source and every proxy DLL report the same PE metadata (0.10.x,
            // identified as OptiScaler); only the installations' own SchemaFamily
            // (as if from a fresh scan) differs, which is what ClassifyTargets
            // must compare the source's family against — not a re-read of the
            // proxy DLL's own version.
            var provider = new FixedVersionInfo(VersionData());
            var validator = new OptiScalerSourceValidator(provider);
            var sourceInfo = Assert.IsType<SourceOptiScalerBinary>(validator.Validate(source).Source);
            var same = MakeInstallation(sameFamilyDir, "dxgi.dll", OptiSchemaFamily.V10, "Same Family Game");
            var different = MakeInstallation(differentFamilyDir, "dxgi.dll", OptiSchemaFamily.V09, "Different Family Game");
            var service = new OptiScalerReplacementService(provider, validator, new NullLogger(), root);
            var classification = await service.ClassifyTargetsAsync(sourceInfo, [same, different]);
            Assert.Equal(2, classification.ReadyTargets.Count);
            Assert.Empty(classification.MultiProxyTargets);
            var mismatched = Assert.Single(classification.FamilyMismatchTargets);
            Assert.Equal("Different Family Game", mismatched.Installation.GameDisplayName);
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public async Task ClassifyTargets_flags_no_mismatch_for_same_family_patch_updates()
    {
        var root = Path.Combine(Path.GetTempPath(), "OptiEditorTests", Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        var source = Path.Combine(root, "source.bin"); File.WriteAllBytes(source, [1]);
        var dir = Directory.CreateDirectory(Path.Combine(root, "install")).FullName; File.WriteAllBytes(Path.Combine(dir, "dxgi.dll"), [2]);
        try
        {
            var provider = new FixedVersionInfo(VersionData() with { FileMajorPart = 0, FileMinorPart = 9, FileBuildPart = 9 });
            var validator = new OptiScalerSourceValidator(provider);
            var sourceInfo = Assert.IsType<SourceOptiScalerBinary>(validator.Validate(source).Source);
            var installation = MakeInstallation(dir, "dxgi.dll", OptiSchemaFamily.V09, "Game");
            var service = new OptiScalerReplacementService(provider, validator, new NullLogger(), root);
            var classification = await service.ClassifyTargetsAsync(sourceInfo, [installation]);
            Assert.Single(classification.ReadyTargets);
            Assert.Empty(classification.FamilyMismatchTargets);
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public async Task ClassifyTargets_skips_installations_with_multiple_identified_OptiScaler_binaries()
    {
        var root = Path.Combine(Path.GetTempPath(), "OptiEditorTests", Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        var source = Path.Combine(root, "source.bin"); File.WriteAllBytes(source, [1]);
        var multiDir = Directory.CreateDirectory(Path.Combine(root, "multi")).FullName;
        File.WriteAllBytes(Path.Combine(multiDir, "dxgi.dll"), [2]);
        File.WriteAllBytes(Path.Combine(multiDir, "winmm.dll"), [3]);
        try
        {
            var provider = new FixedVersionInfo(VersionData());
            var validator = new OptiScalerSourceValidator(provider);
            var sourceInfo = Assert.IsType<SourceOptiScalerBinary>(validator.Validate(source).Source);
            var installation = MakeInstallation(multiDir, "dxgi.dll", OptiSchemaFamily.V10, "Multi DLL Game");
            var service = new OptiScalerReplacementService(provider, validator, new NullLogger(), root);
            var classification = await service.ClassifyTargetsAsync(sourceInfo, [installation]);
            Assert.Empty(classification.ReadyTargets);
            var skipped = Assert.Single(classification.MultiProxyTargets);
            Assert.Equal(OptiScalerReplacementStatus.Skipped, skipped.Status);
            Assert.Equal(OptiScalerReplacementReason.MultipleOptiScalerBinaries, skipped.Reason);
            Assert.Equal(["dxgi.dll", "winmm.dll"], skipped.DetectedBinaryNames);
            Assert.Contains("dxgi.dll", skipped.UserMessage);
            Assert.Contains("winmm.dll", skipped.UserMessage);
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public async Task ClassifyTargets_does_not_treat_a_non_OptiScaler_candidate_file_as_a_second_binary()
    {
        var root = Path.Combine(Path.GetTempPath(), "OptiEditorTests", Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        var source = Path.Combine(root, "source.bin"); File.WriteAllBytes(source, [1]);
        var dir = Directory.CreateDirectory(Path.Combine(root, "install")).FullName;
        var optiScalerDll = Path.Combine(dir, "dxgi.dll"); File.WriteAllBytes(optiScalerDll, [2]);
        var unrelatedDll = Path.Combine(dir, "winmm.dll"); File.WriteAllBytes(unrelatedDll, [3]);
        try
        {
            var versions = new Dictionary<string, FileVersionData>(StringComparer.OrdinalIgnoreCase)
            {
                [source] = VersionData(),
                [optiScalerDll] = VersionData(),
                [unrelatedDll] = VersionData() with { ProductName = "Some Other Mod", InternalName = null, OriginalFilename = null, FileDescription = null },
            };
            var provider = new PathVersionInfo(versions);
            var validator = new OptiScalerSourceValidator(provider);
            var sourceInfo = Assert.IsType<SourceOptiScalerBinary>(validator.Validate(source).Source);
            var installation = MakeInstallation(dir, "dxgi.dll", OptiSchemaFamily.V10, "Game");
            var service = new OptiScalerReplacementService(provider, validator, new NullLogger(), root);
            var classification = await service.ClassifyTargetsAsync(sourceInfo, [installation]);
            Assert.Single(classification.ReadyTargets);
            Assert.Empty(classification.MultiProxyTargets);
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public async Task ClassifyTargets_keeps_processing_ready_targets_alongside_multi_proxy_ones()
    {
        var root = Path.Combine(Path.GetTempPath(), "OptiEditorTests", Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        var source = Path.Combine(root, "source.bin"); File.WriteAllBytes(source, [1]);
        var validDir = Directory.CreateDirectory(Path.Combine(root, "valid")).FullName; File.WriteAllBytes(Path.Combine(validDir, "dxgi.dll"), [2]);
        var multiDir = Directory.CreateDirectory(Path.Combine(root, "multi")).FullName;
        File.WriteAllBytes(Path.Combine(multiDir, "dxgi.dll"), [3]);
        File.WriteAllBytes(Path.Combine(multiDir, "winmm.dll"), [4]);
        try
        {
            var provider = new FixedVersionInfo(VersionData());
            var validator = new OptiScalerSourceValidator(provider);
            var sourceInfo = Assert.IsType<SourceOptiScalerBinary>(validator.Validate(source).Source);
            var valid = MakeInstallation(validDir, "dxgi.dll", OptiSchemaFamily.V10, "Valid Game");
            var multi = MakeInstallation(multiDir, "dxgi.dll", OptiSchemaFamily.V10, "Multi Game");
            var service = new OptiScalerReplacementService(provider, validator, new NullLogger(), root);
            var classification = await service.ClassifyTargetsAsync(sourceInfo, [valid, multi]);
            var ready = Assert.Single(classification.ReadyTargets);
            Assert.Equal("Valid Game", ready.Installation.GameDisplayName);
            var skipped = Assert.Single(classification.MultiProxyTargets);
            Assert.Equal("Multi Game", skipped.GameDisplayName);
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public void Source_validation_rejects_missing_or_unreadable_file_version()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".bin"); File.WriteAllBytes(path, [1]);
        try
        {
            var missing = VersionData() with { FileVersionText = null };
            var malformed = VersionData() with { FileVersionText = "not-a-version" };
            Assert.False(new OptiScalerSourceValidator(new FixedVersionInfo(missing)).Validate(path).IsValid);
            Assert.False(new OptiScalerSourceValidator(new FixedVersionInfo(malformed)).Validate(path).IsValid);
        }
        finally { File.Delete(path); }
    }
    private static FileVersionData VersionData() => new(0, 10, 1, 0, "0.10.1", "OptiScaler", "OptiScaler", "any.bin", "OptiScaler", "0.10.1.0");
    private static OptiInstallation MakeInstallation(string directory, string binaryFileName, OptiSchemaFamily family, string gameDisplayName) => new()
    {
        IniPath = Path.Combine(directory, "OptiScaler.ini"),
        InstallDirectory = directory,
        OptiBinaryPath = Path.Combine(directory, binaryFileName),
        OptiBinaryFileName = binaryFileName,
        FileVersion = new Version(family == OptiSchemaFamily.V09 ? 0 : 0, family == OptiSchemaFamily.V09 ? 9 : 10),
        SchemaFamily = family,
        GameDisplayName = gameDisplayName,
        ScannedAt = DateTimeOffset.UtcNow,
    };
    private sealed class FixedVersionInfo(FileVersionData data) : IFileVersionInfoProvider { public FileVersionData Read(string path) => data; }
    private sealed class PathVersionInfo(IReadOnlyDictionary<string, FileVersionData> map) : IFileVersionInfoProvider { public FileVersionData Read(string path) => map[path]; }
    private sealed class FlakyVersionInfo(FileVersionData good) : IFileVersionInfoProvider
    {
        private int _targetReads;
        public string? TargetPath { get; init; }
        public FileVersionData Read(string path)
        {
            if (TargetPath is not null && string.Equals(path, TargetPath, StringComparison.OrdinalIgnoreCase) && ++_targetReads >= 2)
                return good with { ProductName = "unrelated binary", InternalName = null, OriginalFilename = null, FileDescription = null };
            return good;
        }
    }
    private sealed class ThrowingOnSecondReadVersionInfo(FileVersionData good) : IFileVersionInfoProvider
    {
        private int _targetReads;
        public string? TargetPath { get; init; }
        public FileVersionData Read(string path)
        {
            if (TargetPath is not null && string.Equals(path, TargetPath, StringComparison.OrdinalIgnoreCase) && ++_targetReads >= 2)
                throw new IOException("Simulated version read failure during final verification.");
            return good;
        }
    }
    private sealed class NullLogger : IDiagnosticLogger { public void Info(string message) { } public void Error(string message, Exception? exception = null) { } }
}
