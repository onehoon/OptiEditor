using System.Text.Json;
using OptiEditor.Core.Models;
using OptiEditor.Core.Schema;
using OptiEditor.Core.Storage;

namespace OptiEditor.Core.Tests;

public sealed class SchemaTests
{
    [Fact]
    public void Generated_catalog_contains_only_root_ini_keys_processed_by_upstream_code()
    {
        var resolver = new OptiSchemaResolver();
        var v09 = resolver.Resolve(OptiSchemaFamily.V09);
        var v10 = resolver.Resolve(OptiSchemaFamily.V10);
        Assert.Equal(288, v09.Settings.Count);
        Assert.Equal(357, v10.Settings.Count);
        Assert.NotNull(v09.FindById("Plugins.LoadAsiPlugins"));
        Assert.NotNull(v10.FindById("Magnifier.Enabled"));
        Assert.Null(v09.FindById("DLSSG.InterpolationCount"));
        Assert.Null(v10.FindById("Hotfix.ManualInputPolling"));
    }

    [Fact]
    public void Dashboard_contract_matches_every_v10_setting_used_by_the_editor()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Schema", "optiscaler-v10-master.json")));
        var root = document.RootElement;
        var schema = new Opti10SchemaProvider();
        Assert.Equal("optiscaler.v10.master", root.GetProperty("schemaId").GetString());
        Assert.Equal("ccf3b8c6a75824d0c9c7cc7156bf666afddde3cc", root.GetProperty("upstream").GetProperty("commit").GetString());
        Assert.Equal(schema.Settings.Count, root.GetProperty("settingCount").GetInt32());

        var exported = root.GetProperty("settings").EnumerateArray().ToDictionary(x => x.GetProperty("id").GetString()!, StringComparer.Ordinal);
        Assert.Equal(schema.Settings.Count, exported.Count);
        foreach (var definition in schema.Settings)
        {
            var entry = exported[definition.Id];
            Assert.Equal(definition.IniKey.Section, entry.GetProperty("section").GetString());
            Assert.Equal(definition.IniKey.Name, entry.GetProperty("key").GetString());
            Assert.Equal(definition.ValueKind.ToString().ToLowerInvariant(), entry.GetProperty("valueType").GetString());
            Assert.Equal(definition.SupportsAuto, entry.GetProperty("supportsAuto").GetBoolean());
            Assert.Equal(definition.DefaultRawValue, entry.GetProperty("defaultValue").GetString());
            AssertJsonNumber(definition.Minimum, entry.GetProperty("minimum"));
            AssertJsonNumber(definition.Maximum, entry.GetProperty("maximum"));
            Assert.Equal(definition.OptionsAreSuggestions ? "suggestions" : definition.ValueKind == SettingValueKind.Enum ? "closed" : "none", entry.GetProperty("optionsMode").GetString());
            Assert.Equal(definition.SourceReader, entry.GetProperty("sourceReader").GetString());
            Assert.Equal(definition.ContractNote, entry.GetProperty("contractNote").ValueKind == JsonValueKind.Null ? null : entry.GetProperty("contractNote").GetString());
            Assert.Equal(definition.SourceComment, entry.GetProperty("sourceComment").GetString());

            var options = entry.GetProperty("options").EnumerateArray().ToArray();
            Assert.Equal(definition.Options.Count, options.Length);
            for (var index = 0; index < options.Length; index++)
            {
                Assert.Equal(definition.Options[index].Value, options[index].GetProperty("value").GetString());
                Assert.Equal(definition.Options[index].Label, options[index].GetProperty("label").GetString());
                var condition = options[index].GetProperty("condition");
                Assert.Equal(definition.Options[index].Condition, condition.ValueKind == JsonValueKind.Null ? null : condition.GetString());
            }
        }

        var readers = root.GetProperty("readerContracts").EnumerateArray().ToArray();
        var readerIds = readers.Select(reader => reader.GetProperty("id").GetString()!).ToHashSet(StringComparer.Ordinal);
        Assert.Contains("readUInt", readerIds);
        Assert.Contains("readWString", readerIds);
        Assert.Contains(readers, reader => reader.GetProperty("id").GetString() == "readUInt" &&
            reader.GetProperty("behavior").GetString()!.Contains("-1 becomes UInt32.MaxValue", StringComparison.Ordinal));
        foreach (var sourceReader in schema.Settings.Select(setting => setting.SourceReader).Where(reader => reader.Length > 0).Distinct(StringComparer.Ordinal))
        {
            var contractId = sourceReader.StartsWith("readEnum<", StringComparison.Ordinal) ? "readEnum<T>" : sourceReader;
            Assert.Contains(contractId, readerIds);
        }
    }

    private static void AssertJsonNumber(double? expected, JsonElement actual)
    {
        if (expected is null) Assert.Equal(JsonValueKind.Null, actual.ValueKind);
        else Assert.Equal(expected.Value, actual.GetDouble());
    }

    [Fact]
    public void Generated_types_follow_the_upstream_config_reader()
    {
        var schema = new Opti10SchemaProvider();
        Assert.Equal(SettingValueKind.Boolean, schema.FindById("Plugins.LoadAsiPlugins")!.ValueKind);
        Assert.Equal(SettingValueKind.Enum, schema.FindById("FrameGen.AllowedFrameAhead")!.ValueKind);
        Assert.Equal(SettingValueKind.Double, schema.FindById("FSR.VerticalFov")!.ValueKind);
        Assert.Equal(SettingValueKind.String, schema.FindById("Menu.TTFFontPath")!.ValueKind);
        Assert.Null(schema.FindById("FSR.Fsr4ForceModel"));
        var upscaler = schema.FindById("Upscalers.Dx11Upscaler")!;
        Assert.Equal(SettingValueKind.String, upscaler.ValueKind);
        Assert.True(upscaler.OptionsAreSuggestions);
        Assert.True(SettingValidator.Validate(upscaler, "MyCustomProvider").IsValid);
        Assert.Equal(1, schema.FindById("Anisotropy.AnisotropyOverride")!.Minimum);
        Assert.Equal(16, schema.FindById("Anisotropy.AnisotropyOverride")!.Maximum);
        var menuScale = schema.FindById("Menu.Scale")!;
        Assert.Equal(SettingValueKind.Double, menuScale.ValueKind);
        Assert.Equal(0.5, menuScale.Minimum);
        Assert.Equal(2, menuScale.Maximum);
        var xeFgInterpolation = schema.FindById("XeFG.InterpolationCount")!;
        Assert.Equal(SettingValueKind.Integer, xeFgInterpolation.ValueKind);
        Assert.Equal(1, xeFgInterpolation.Minimum);
        Assert.Null(xeFgInterpolation.Maximum);
        Assert.Equal(["1", "2", "3"], xeFgInterpolation.Options.Select(x => x.Value));

        var v09 = new Opti09SchemaProvider();
        Assert.Equal(["fsr22", "fsr31", "xess", "xess_12", "fsr21_12", "fsr22_12", "fsr31_12", "dlss"], v09.FindById("Upscalers.Dx11Upscaler")!.Options.Select(x => x.Value));
        Assert.Equal(["dlssg", "nukems", "fsrfg", "upscaler", "fsrfg30"], v09.FindById("FrameGen.FGInput")!.Options.Select(x => x.Value));
        Assert.Equal(["fsrfg", "xefg", "nukems"], v09.FindById("FrameGen.FGOutput")!.Options.Select(x => x.Value));
        Assert.Equal(["xess", "xess_12", "fsr21", "fsr21_12", "fsr22", "fsr22_12", "ffx", "ffx_12", "dlss", "dlss_12", "dlssd", "fsr31", "fsr31_12"], upscaler.Options.Select(x => x.Value));
        var rrUpscaler = schema.FindById("Upscalers.RRUpscaler")!;
        Assert.Equal(SettingValueKind.String, rrUpscaler.ValueKind);
        Assert.True(rrUpscaler.OptionsAreSuggestions);
        Assert.True(SettingValidator.Validate(rrUpscaler, "MyCustomRRProvider").IsValid);
        Assert.Equal(["NoFG", "Upscaler", "DLSSG", "NvngxFG", "FSRFG", "FSRFG30", "XeFG"], schema.FindById("FrameGen.FGInput")!.Options.Select(x => x.Value));
        Assert.DoesNotContain(schema.FindById("FrameGen.FGInput")!.Options, x => x.Value.Equals("ForceXeLL", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(["NoFG", "FSRFG", "DLSSG", "XeFG", "Reprojection", "FlipMeter"], schema.FindById("FrameGen.FGOutput")!.Options.Select(x => x.Value));
        var replacement = schema.FindById("FrameGen.FGNvngxReplacement")!;
        Assert.Equal(SettingValueKind.String, replacement.ValueKind);
        Assert.True(replacement.OptionsAreSuggestions);
        Assert.Equal(["none", "nukems", "arturs", "ffx", "combo"], replacement.Options.Select(x => x.Value));

        foreach (var id in new[] { "Upscalers.Dx11Upscaler", "Upscalers.Dx12Upscaler", "Upscalers.VulkanUpscaler", "FrameGen.FGInput", "FrameGen.FGOutput" }) Assert.Equal(SettingValueKind.Enum, v09.FindById(id)!.ValueKind);
        foreach (var id in new[] { "FrameGen.FGInput", "FrameGen.FGOutput", "Sharpness.Shader" }) Assert.Equal(SettingValueKind.Enum, schema.FindById(id)!.ValueKind);
        foreach (var id in new[] { "Upscalers.Dx11Upscaler", "Upscalers.Dx12Upscaler", "Upscalers.VulkanUpscaler" }) Assert.Equal(SettingValueKind.String, schema.FindById(id)!.ValueKind);
        foreach (var id in new[] { "Spoofing.DxgiBlacklist", "Log.LogFileName" }) { Assert.Equal(SettingValueKind.String, v09.FindById(id)!.ValueKind); Assert.Equal(SettingValueKind.String, schema.FindById(id)!.ValueKind); }
    }

    [Fact]
    public void Generated_ranges_and_enum_values_are_validated()
    {
        var schema = new Opti10SchemaProvider();
        var anisotropy = schema.FindById("Anisotropy.AnisotropyOverride")!;
        Assert.False(SettingValidator.Validate(anisotropy, "17").IsValid);
        Assert.True(SettingValidator.Validate(anisotropy, "16").IsValid);
        Assert.True(SettingValidator.Validate(anisotropy, "6").IsValid);
        var downscaler = schema.FindById("OutputScaling.Downscaler")!;
        Assert.False(SettingValidator.Validate(downscaler, "8").IsValid);
        Assert.True(SettingValidator.Validate(downscaler, "7").IsValid);
        var xeFgInterpolation = schema.FindById("XeFG.InterpolationCount")!;
        Assert.True(SettingValidator.Validate(xeFgInterpolation, "4").IsValid);
        Assert.False(SettingValidator.Validate(xeFgInterpolation, "0").IsValid);
        Assert.True(SettingValidator.Validate(xeFgInterpolation, "3").IsValid);
        var hudLimit = schema.FindById("OptiFG.HUDLimit")!;
        Assert.Equal(SettingInputKind.Stepper, hudLimit.InputKind);
        Assert.False(SettingValidator.Validate(hudLimit, "0").IsValid);
        Assert.True(SettingValidator.Validate(hudLimit, "1").IsValid);
        var networkModel = schema.FindById("XeSS.NetworkModel")!;
        Assert.Equal(SettingValueKind.Integer, networkModel.ValueKind);
        Assert.Equal(["KPSS", "Splat", "Model 3", "Model 4", "Model 5", "Model 6"], networkModel.Options.Select(x => x.Label));
        Assert.True(networkModel.OptionsAreSuggestions);
        Assert.True(SettingValidator.Validate(networkModel, "6").IsValid);
        var upscalerBackend = schema.FindById("FSR.UpscalerIndex")!;
        Assert.Equal(SettingValueKind.Enum, upscalerBackend.ValueKind);
        Assert.Equal(["FSR 4.0.2", "FSR 3.1.5", "FSR 2.3.4"], upscalerBackend.Options.Select(x => x.Label));
        Assert.False(SettingValidator.Validate(upscalerBackend, "3").IsValid);

        var vendor = schema.FindById("Spoofing.SpoofedVendorId")!;
        Assert.Equal(SettingValueKind.Integer, vendor.ValueKind);
        Assert.True(vendor.OptionsAreSuggestions);
        Assert.Equal(["0x10de", "0x8086", "0x1002"], vendor.Options.Select(x => x.Value));
        Assert.Equal(int.MinValue, vendor.Minimum);
        Assert.Equal(int.MaxValue, vendor.Maximum);
        Assert.True(SettingValidator.Validate(vendor, "0x1002").IsValid);
        Assert.True(SettingValidator.Validate(vendor, "4098").IsValid);
        Assert.True(SettingValidator.Validate(vendor, "-1").IsValid);
        Assert.False(SettingValidator.Validate(vendor, "0xFFFFFFFF").IsValid);
        Assert.False(SettingValidator.Validate(vendor, "0x100000000").IsValid);
        var device = schema.FindById("Spoofing.SpoofedDeviceId")!;
        Assert.Equal(["RTX 4090 (0x2684)", "Intel Arc B580 (0xE20B)", "Radeon RX 9070 XT (0x7550)"], device.Options.Select(x => x.Label));
        Assert.Equal(["Top left", "Top right", "Bottom left", "Bottom right", "Top center", "Bottom center", "Custom position"], schema.FindById("Menu.FpsOverlayPos")!.Options.Select(x => x.Label));
        Assert.Equal(["Just FPS", "Simple", "Detailed", "Detailed + graph", "Full", "Full + graph", "Low latency timings", "Custom rows"], schema.FindById("Menu.FpsOverlayType")!.Options.Select(x => x.Label));
        Assert.Equal(["0", "1", "2", "3", "4"], schema.FindById("Log.LogLevel")!.Options.Select(x => x.Value));
        Assert.Equal(["1", "2", "3", "4", "5", "6"], schema.FindById("DLSSG.InterpolationCount")!.Options.Select(x => x.Value));
        Assert.True(SettingValidator.Validate(schema.FindById("DLSSG.InterpolationCount")!, "6").IsValid);
        Assert.False(SettingValidator.Validate(schema.FindById("DLSSG.InterpolationCount")!, "7").IsValid);
        var latest = schema.FindById("DLSS.RenderPresetForAll")!;
        Assert.Contains(latest.Options, x => x.Value == "16");
        Assert.Contains(latest.Options, x => x.Value == "0x00FFFFFF");
        Assert.True(SettingValidator.Validate(latest, "16777215").IsValid);

        var ftInput = schema.FindById("FrameGen.FTInput")!;
        Assert.Equal("FrameGen.FGOutput=XeFG", ftInput.Options.Single(x => x.Value == "2").Condition);
        Assert.True(SettingValidator.Validate(ftInput, "2").IsValid);
        var fsr4Gpu = schema.FindById("FSR.Fsr4GpuSupport")!;
        Assert.Equal(["0", "1", "2", "3", "4"], fsr4Gpu.Options.Select(x => x.Value));
        Assert.Equal(["0", "1", "2", "3"], schema.FindById("FSR.Fsr4Model")!.Options.Select(x => x.Value));
        var fpsRows = schema.FindById("Menu.FpsOverlayRows")!;
        Assert.Equal(SettingValueKind.String, fpsRows.ValueKind);
        Assert.True(fpsRows.OptionsAreSuggestions);
        Assert.True(SettingValidator.Validate(fpsRows, "fps,-frametime,lowlatency").IsValid);
        Assert.Contains("ordered", fpsRows.ContractNote, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, schema.FindById("Menu.FpsOverlayRowSpacing")!.Minimum);
        Assert.Equal(32, schema.FindById("Menu.FpsOverlayRowSpacing")!.Maximum);
        var overlayInterval = schema.FindById("Menu.FpsOverlayUpdateInterval")!;
        Assert.Equal(SettingValueKind.Double, overlayInterval.ValueKind);
        Assert.True(SettingValidator.Validate(overlayInterval, "250.5").IsValid);
        Assert.Equal(3, schema.FindById("Menu.FpsScale")!.Maximum);
        var overlayRounding = schema.FindById("Menu.FpsOverlayRounding")!;
        Assert.Equal(SettingValueKind.Double, overlayRounding.ValueKind);
        Assert.True(SettingValidator.Validate(overlayRounding, "2.5").IsValid);
        var blurStrength = schema.FindById("Menu.BlurStrength")!;
        Assert.Equal(0, blurStrength.Minimum);
        Assert.Equal(4, blurStrength.Maximum);
        var overlayColor = schema.FindById("Menu.FpsOverlayLabelColor")!;
        Assert.True(SettingValidator.Validate(overlayColor, "0xFFFFFF").IsValid);
        Assert.True(SettingValidator.Validate(overlayColor, "-1").IsValid);
        Assert.False(SettingValidator.Validate(overlayColor, "0xFFFFFFFF").IsValid);

        var qualityRatio = schema.FindById("QualityOverrides.QualityRatioBalanced")!;
        Assert.Equal(SettingValueKind.Double, qualityRatio.ValueKind);
        Assert.Equal(SettingInputKind.Text, qualityRatio.InputKind);
        Assert.Equal("Balanced", qualityRatio.DisplayName);
        Assert.True(SettingValidator.Validate(qualityRatio, "1.7").IsValid);
        Assert.True(SettingValidator.Validate(qualityRatio, "auto").IsValid);
        // double.TryParse accepts these literal strings regardless of range
        // bounds; NaN in particular compares false against both Minimum and
        // Maximum, so a bare range check alone would not reject it.
        Assert.False(SettingValidator.Validate(qualityRatio, "NaN").IsValid);
        Assert.False(SettingValidator.Validate(qualityRatio, "Infinity").IsValid);
        Assert.False(SettingValidator.Validate(qualityRatio, "-Infinity").IsValid);

        var fpsOverlay = schema.FindById("Menu.FpsOverlayType")!;
        Assert.Contains("FPS overlay type", fpsOverlay.SourceComment, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(';', fpsOverlay.SourceComment);

        // Upstream Config.cpp resets AllowedFrameAhead to auto outside 1-3.
        var allowedFrameAhead = schema.FindById("FrameGen.AllowedFrameAhead")!;
        Assert.Equal(SettingValueKind.Enum, allowedFrameAhead.ValueKind);
        Assert.Equal(["1", "2", "3"], allowedFrameAhead.Options.Select(x => x.Value));
        Assert.False(SettingValidator.Validate(allowedFrameAhead, "0").IsValid);
        Assert.False(SettingValidator.Validate(allowedFrameAhead, "4").IsValid);
        Assert.True(SettingValidator.Validate(allowedFrameAhead, "1").IsValid);
        Assert.True(SettingValidator.Validate(allowedFrameAhead, "3").IsValid);
    }

    [Fact]
    public void Integer_settings_accept_hexadecimal_values()
    {
        var schema = new Opti10SchemaProvider();
        var dispatchFlags = schema.FindById("NvngxFG.DispatchFlags")!;
        Assert.Equal(SettingValueKind.Integer, dispatchFlags.ValueKind);
        Assert.True(SettingValidator.Validate(dispatchFlags, "0x14100000").IsValid);
        Assert.True(SettingValidator.Validate(dispatchFlags, "0XABCD").IsValid);
        Assert.True(SettingValidator.Validate(dispatchFlags, "336068608").IsValid);
        Assert.True(SettingValidator.Validate(dispatchFlags, "0x7FFFFFFF").IsValid);
        Assert.True(SettingValidator.Validate(dispatchFlags, "-1").IsValid);
        Assert.False(SettingValidator.Validate(dispatchFlags, "0xFFFFFFFF").IsValid);
        // Config::readUInt uses std::stoi into a signed Int32 before converting
        // to UInt32: negative decimal inputs wrap, while high unsigned hex values
        // exceed std::stoi's range and are ignored.
        Assert.False(SettingValidator.Validate(dispatchFlags, "-0x10").IsValid);
        Assert.False(SettingValidator.Validate(dispatchFlags, "0xFFFFFFFFFFFFFFFF").IsValid);
        Assert.False(SettingValidator.Validate(dispatchFlags, "2147483648").IsValid);
        Assert.False(SettingValidator.Validate(dispatchFlags, "-2147483649").IsValid);
        Assert.False(SettingValidator.Validate(dispatchFlags, "0xZZ").IsValid);
        Assert.False(SettingValidator.Validate(dispatchFlags, "not-a-number").IsValid);
    }

    [Fact]
    public void Generated_default_raw_values_have_no_leading_or_trailing_whitespace()
    {
        var v09 = new Opti09SchemaProvider();
        var v10 = new Opti10SchemaProvider();
        Assert.All(v09.Settings.Concat(v10.Settings), setting => Assert.Equal(setting.DefaultRawValue, setting.DefaultRawValue.Trim()));
    }

    [Fact]
    public void Section_visibility_hides_every_key_in_that_section()
    {
        var schema = new Opti10SchemaProvider();
        var definition = schema.FindById("FrameGen.Enabled")!;
        Assert.Equal(EditorVisibility.Hidden, EditorVisibilityPolicy.Resolve(definition, OptiSchemaFamily.V10, [new(OptiSchemaFamily.V10, "FrameGen", EditorVisibility.Hidden)]));
    }

    [Fact]
    public async Task Visibility_store_round_trips_section_overrides()
    {
        var folder = Path.Combine(Path.GetTempPath(), "OptiEditorTests", Guid.NewGuid().ToString("N")); Directory.CreateDirectory(folder);
        try { var store = new EditorVisibilityStore(folder); await store.SaveAsync([new(OptiSchemaFamily.V09, "Plugins", EditorVisibility.Hidden)]); var item = Assert.Single(await store.LoadAsync()); Assert.Equal("Plugins", item.Section); }
        finally { Directory.Delete(folder, true); }
    }

    [Fact]
    public async Task Source_comment_visibility_defaults_to_visible_and_round_trips()
    {
        var folder = Path.Combine(Path.GetTempPath(), "OptiEditorTests", Guid.NewGuid().ToString("N")); Directory.CreateDirectory(folder);
        try { var store = new SourceCommentVisibilityStore(folder); Assert.True(await store.LoadAsync()); await store.SaveAsync(false); Assert.False(await store.LoadAsync()); }
        finally { Directory.Delete(folder, true); }
    }
}
