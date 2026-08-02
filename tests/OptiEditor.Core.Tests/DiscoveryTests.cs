using OptiEditor.Core.Discovery;
using OptiEditor.Core.Models;
using OptiEditor.Core.Utilities;

namespace OptiEditor.Core.Tests;

public sealed class DiscoveryTests
{
    [Theory]
    [InlineData("dxgi.dll")][InlineData("OptiScaler.dll")][InlineData("WINMM.DLL")][InlineData("OptiScaler.asi")]
    public void Approved_proxy_names_are_accepted(string name) => Assert.True(OptiBinaryRules.IsApprovedProxyName(name));
    [Fact] public void Unknown_proxy_name_is_rejected() => Assert.False(OptiBinaryRules.IsApprovedProxyName("random.dll"));

    [Theory]
    [InlineData("EpicOnlineServicesInstaller.exe")]
    [InlineData("DLSSTweaksConfigTool.exe")]
    [InlineData("DLSSTweaksConfig.exe")]
    [InlineData("VC_redist.x64.exe")]
    [InlineData("UnityCrashHandler64.exe")]
    [InlineData("CCrashReport.exe")]
    [InlineData("InstallerMessage.exe")]
    [InlineData("crs-handler.exe")]
    [InlineData("crs-uploader.exe")]
    [InlineData("REDEngineErrorReporter.exe")]
    [InlineData("BugSplatHD64.exe")]
    [InlineData("BsSndRpt64.exe")]
    [InlineData("crs-video.exe")]
    [InlineData("launcher.exe")]
    [InlineData("unitycrashhandler64.EXE")]
    public void Known_non_game_executables_are_excluded(string name) => Assert.True(GameExecutableDetector.IsExcluded(name));

    [Fact]
    public void Excluded_executable_is_not_selected_as_game()
    {
        var result = GameExecutableDetector.Select([new("C:\\UnityCrashHandler64.exe", 5000, "Unity Crash Handler"), new("C:\\game.exe", 10, "Game")]);
        Assert.Equal("game.exe", result.FileName);
    }

    [Fact]
    public void Game_master_name_takes_precedence_over_product_name()
    {
        var result = GameExecutableDetector.Select([new("C:\\DD2.exe", 10, "Unrelated Product")]);
        Assert.Equal("Dragon's Dogma 2", result.DisplayName);
    }

    [Fact]
    public void Game_master_map_uses_only_executable_entries()
    {
        Assert.Null(GameMasterNameMap.Find("WHGameArm.dll"));
        Assert.Equal("Kingdom Come: Deliverance II", GameMasterNameMap.Find("KingdomCome.exe"));
    }

    [Theory]
    [InlineData("ProductName")][InlineData("InternalName")][InlineData("OriginalFilename")][InlineData("FileDescription")]
    public void OptiScaler_metadata_is_accepted(string property)
    {
        var v = VersionData() with { ProductName = property == "ProductName" ? " OptiScaler " : null, InternalName = property == "InternalName" ? "OptiScaler" : null, OriginalFilename = property == "OriginalFilename" ? "OptiScaler.dll" : null, FileDescription = property == "FileDescription" ? "OptiScaler" : null };
        Assert.True(OptiBinaryRules.IsOptiScaler(v));
    }
    [Fact] public void System_version_dll_and_missing_metadata_are_rejected() { Assert.False(OptiBinaryRules.IsOptiScaler(VersionData() with { ProductName = null, OriginalFilename = "version.dll" })); Assert.False(OptiBinaryRules.IsOptiScaler(VersionData() with { ProductName = null })); }

    [Theory]
    [InlineData(0, 9, OptiSchemaFamily.V09)][InlineData(0, 10, OptiSchemaFamily.V10)][InlineData(0, 8, OptiSchemaFamily.Unsupported)][InlineData(0, 11, OptiSchemaFamily.Unsupported)][InlineData(1, 0, OptiSchemaFamily.Unsupported)]
    public void Version_family_is_detected_from_numeric_parts(int major, int minor, OptiSchemaFamily expected) => Assert.Equal(expected, OptiBinaryRules.DetectFamily(VersionData(major, minor)));

    [Theory]
    [InlineData(0, 9, OptiSchemaFamily.V09)][InlineData(0, 10, OptiSchemaFamily.V10)][InlineData(0, 8, OptiSchemaFamily.Unsupported)][InlineData(1, 0, OptiSchemaFamily.Unsupported)]
    public void Version_family_is_detected_from_a_plain_Version(int major, int minor, OptiSchemaFamily expected) => Assert.Equal(expected, OptiBinaryRules.DetectFamily(new Version(major, minor)));

    [Fact]
    public void FindProxyBinaries_only_returns_files_identified_as_OptiScaler()
    {
        var temp = Path.Combine(Path.GetTempPath(), "OptiEditorTests", Guid.NewGuid().ToString("N")); Directory.CreateDirectory(temp);
        try
        {
            var opti = Path.Combine(temp, "dxgi.dll"); File.WriteAllBytes(opti, [1]);
            var alsoOpti = Path.Combine(temp, "winmm.dll"); File.WriteAllBytes(alsoOpti, [2]);
            var notOpti = Path.Combine(temp, "version.dll"); File.WriteAllBytes(notOpti, [3]);
            var versions = new Dictionary<string, FileVersionData>(StringComparer.OrdinalIgnoreCase)
            {
                [opti] = VersionData(),
                [alsoOpti] = VersionData(),
                [notOpti] = VersionData() with { ProductName = "Unrelated Product", InternalName = null, OriginalFilename = null, FileDescription = null },
            };
            Assert.Equal(["dxgi.dll", "winmm.dll"], OptiBinaryRules.FindProxyBinaries(temp, new FakeVersions(versions)));
        }
        finally { Directory.Delete(temp, true); }
    }

    [Fact]
    public void FindProxyBinaries_only_looks_at_the_top_level_directory()
    {
        var temp = Path.Combine(Path.GetTempPath(), "OptiEditorTests", Guid.NewGuid().ToString("N")); Directory.CreateDirectory(temp);
        try
        {
            var nested = Directory.CreateDirectory(Path.Combine(temp, "nested")).FullName;
            var nestedOpti = Path.Combine(nested, "dxgi.dll"); File.WriteAllBytes(nestedOpti, [1]);
            var versions = new Dictionary<string, FileVersionData>(StringComparer.OrdinalIgnoreCase) { [nestedOpti] = VersionData() };
            Assert.Empty(OptiBinaryRules.FindProxyBinaries(temp, new FakeVersions(versions)));
        }
        finally { Directory.Delete(temp, true); }
    }

    [Fact]
    public void Game_exe_prefers_game_master_then_product_name_then_size_then_filename()
    {
        var result = GameExecutableDetector.Select([new("C:\\b.exe", 500, null), new("C:\\a.exe", 10, "My Game")]); Assert.Equal("a.exe", result.FileName);
        result = GameExecutableDetector.Select([new("C:\\b.exe", 500, "B Game"), new("C:\\a.exe", 500, "A Game")]); Assert.Equal("a.exe", result.FileName);
        result = GameExecutableDetector.Select([new("C:\\b.exe", 500, null), new("C:\\a.exe", 500, null)]); Assert.Equal("a.exe", result.FileName);
        Assert.Equal("Unknown Game", GameExecutableDetector.Select([]).DisplayName);
    }

    [Fact]
    public async Task Scanner_filters_invalid_unsupported_and_conflicting_installations()
    {
        var temp = Path.Combine(Path.GetTempPath(), "OptiEditorTests", Guid.NewGuid().ToString("N")); Directory.CreateDirectory(temp);
        try
        {
            var good = Directory.CreateDirectory(Path.Combine(temp, "good")).FullName; File.WriteAllText(Path.Combine(good, "OptiScaler.ini"), ""); File.WriteAllText(Path.Combine(good, "dxgi.dll"), ""); File.WriteAllText(Path.Combine(good, "game.exe"), "");
            var unsupported = Directory.CreateDirectory(Path.Combine(temp, "unsupported")).FullName; File.WriteAllText(Path.Combine(unsupported, "OptiScaler.ini"), ""); File.WriteAllText(Path.Combine(unsupported, "dxgi.dll"), "");
            var conflict = Directory.CreateDirectory(Path.Combine(temp, "conflict")).FullName; File.WriteAllText(Path.Combine(conflict, "OptiScaler.ini"), ""); File.WriteAllText(Path.Combine(conflict, "dxgi.dll"), ""); File.WriteAllText(Path.Combine(conflict, "winmm.dll"), "");
            var same = Directory.CreateDirectory(Path.Combine(temp, "same")).FullName; File.WriteAllText(Path.Combine(same, "OptiScaler.ini"), ""); File.WriteAllText(Path.Combine(same, "dxgi.dll"), ""); File.WriteAllText(Path.Combine(same, "winmm.dll"), "");
            var noProxy = Directory.CreateDirectory(Path.Combine(temp, "no-proxy")).FullName; File.WriteAllText(Path.Combine(noProxy, "OptiScaler.ini"), "");
            var map = new Dictionary<string, FileVersionData>(StringComparer.OrdinalIgnoreCase) { [Path.Combine(good, "dxgi.dll")] = VersionData(), [Path.Combine(good, "game.exe")] = VersionData() with { ProductName = "A Game" }, [Path.Combine(unsupported, "dxgi.dll")] = VersionData(0, 11), [Path.Combine(conflict, "dxgi.dll")] = VersionData(), [Path.Combine(conflict, "winmm.dll")] = VersionData(0, 9), [Path.Combine(same, "dxgi.dll")] = VersionData(), [Path.Combine(same, "winmm.dll")] = VersionData() };
            var scanner = new InstallationDiscoveryScanner(new FakeVersions(map), new NullLogger()); var result = await scanner.ScanAsync([new() { Path = temp }]);
            Assert.Equal(2, result.Installations.Count); Assert.Contains(result.Installations, x => x.GameDisplayName == "A Game"); Assert.Equal(1, result.Summary.SkippedInvalidBinary); Assert.Equal(1, result.Summary.SkippedUnsupportedVersion); Assert.Equal(1, result.Summary.ConflictingVersions);
            File.Delete(Path.Combine(good, "dxgi.dll")); File.Delete(Path.Combine(same, "dxgi.dll")); File.Delete(Path.Combine(same, "winmm.dll")); result = await scanner.ScanAsync([new() { Path = temp }]); Assert.Empty(result.Installations);
        }
        finally { Directory.Delete(temp, true); }
    }

    [Fact]
    public async Task Scanner_does_not_descend_below_a_valid_supported_installation()
    {
        var temp = Path.Combine(Path.GetTempPath(), "OptiEditorTests", Guid.NewGuid().ToString("N")); Directory.CreateDirectory(temp);
        try
        {
            File.WriteAllText(Path.Combine(temp, "OptiScaler.ini"), ""); File.WriteAllText(Path.Combine(temp, "dxgi.dll"), "");
            var nested = Directory.CreateDirectory(Path.Combine(temp, "nested")).FullName; File.WriteAllText(Path.Combine(nested, "OptiScaler.ini"), ""); File.WriteAllText(Path.Combine(nested, "dxgi.dll"), "");
            var versions = new Dictionary<string, FileVersionData>(StringComparer.OrdinalIgnoreCase) { [Path.Combine(temp, "dxgi.dll")] = VersionData(), [Path.Combine(nested, "dxgi.dll")] = VersionData() };
            var scanner = new InstallationDiscoveryScanner(new FakeVersions(versions), new NullLogger());
            var result = await scanner.ScanAsync([new() { Path = temp }]);
            var installation = Assert.Single(result.Installations); Assert.Equal(Path.GetFullPath(temp), installation.InstallDirectory);
        }
        finally { Directory.Delete(temp, true); }
    }
    [Fact]
    public async Task Scanner_does_not_loop_forever_on_a_directory_junction_cycle()
    {
        var temp = Path.Combine(Path.GetTempPath(), "OptiEditorTests", Guid.NewGuid().ToString("N")); Directory.CreateDirectory(temp);
        try
        {
            File.WriteAllText(Path.Combine(temp, "OptiScaler.ini"), "");
            var nested = Directory.CreateDirectory(Path.Combine(temp, "nested")).FullName;
            var junction = Path.Combine(nested, "loop");
            var startInfo = new System.Diagnostics.ProcessStartInfo("cmd.exe", $"/c mklink /J \"{junction}\" \"{temp}\"") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
            using (var process = System.Diagnostics.Process.Start(startInfo)!) { process.WaitForExit(10000); if (process.ExitCode != 0 || !Directory.Exists(junction)) return; }

            try
            {
                var scanner = new InstallationDiscoveryScanner(new FakeVersions(new Dictionary<string, FileVersionData>(StringComparer.OrdinalIgnoreCase)), new NullLogger());
                var task = scanner.ScanAsync([new() { Path = temp }]);
                var completed = await Task.WhenAny(task, Task.Delay(TimeSpan.FromSeconds(10)));
                Assert.Same(task, completed);
            }
            finally { Directory.Delete(junction, false); }
        }
        finally { Directory.Delete(temp, true); }
    }

    private static FileVersionData VersionData(int major = 0, int minor = 10) => new(major, minor, 0, 1, "0.10.0-pre1", "OptiScaler", null, null, null);
    private sealed class FakeVersions(IReadOnlyDictionary<string, FileVersionData> map) : IFileVersionInfoProvider { public FileVersionData Read(string path) => map[path]; }
    private sealed class NullLogger : IDiagnosticLogger { public void Info(string message) { } public void Error(string message, Exception? exception = null) { } }
}
