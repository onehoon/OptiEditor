using OptiEditor.Core.Models;

namespace OptiEditor.Core.Schema;

// Source-backed corrections for contracts the generic INI reader cannot infer
// from a single Config.cpp read call. Keep V09 values isolated from V10.
internal static class OptiSchemaOverlays
{
    private static readonly IReadOnlyList<SettingOption> XeFgInterpolationChoices =
    [new("1", "2X"), new("2", "3X"), new("3", "4X")];
    private static readonly IReadOnlyList<SettingOption> XeSsNetworkModels =
    [new("0", "KPSS"), new("1", "Splat"), new("2", "Model 3"), new("3", "Model 4"), new("4", "Model 5"), new("5", "Model 6")];
    private static readonly IReadOnlyList<SettingOption> FsrUpscalerBackends =
    [new("0", "FSR 4.0.2"), new("1", "FSR 3.1.5"), new("2", "FSR 2.3.4")];
    private static readonly IReadOnlyList<SettingOption> FpsOverlayPositionsV09 =
    [new("0", "Top Left"), new("1", "Top Right"), new("2", "Bottom Left"), new("3", "Bottom Right")];
    private static readonly IReadOnlyList<SettingOption> FpsOverlayTypesV09 =
    [new("0", "Just FPS"), new("1", "Simple"), new("2", "Detailed"), new("3", "Detailed + Graph"), new("4", "Full"), new("5", "Full + Graph"), new("6", "Reflex timings")];
    private static readonly IReadOnlyList<SettingOption> LogLevels =
    [new("0", "Trace"), new("1", "Debug"), new("2", "Info"), new("3", "Warning"), new("4", "Error")];
    private static readonly IReadOnlyList<SettingOption> FsrFrameGenerationBackends =
    [new("0", "FSR 4.0.0"), new("1", "FSR 3.1.6")];
    private static readonly IReadOnlyList<SettingOption> Fsr4Presets =
    [new("0", "Native AA"), new("1", "Ultra Quality / Quality"), new("2", "Balanced"), new("3", "Performance"), new("4", "DRS"), new("5", "Ultra Performance")];
    private static readonly IReadOnlyList<SettingOption> DlssRenderPresets = CreateRenderPresets(16);
    private static readonly IReadOnlyList<SettingOption> DlssdRenderPresets = CreateRenderPresets(6);
    private static readonly IReadOnlyList<SettingOption> Downscalers =
    [new("0", "FSR 1"), new("1", "Bicubic"), new("2", "Catmull-Rom"), new("3", "Lanczos 2"), new("4", "Lanczos 3"), new("5", "Kaiser 2"), new("6", "Kaiser 3"), new("7", "MAGIC")];
    private static readonly IReadOnlyList<SettingOption> LatencyFlexModes =
    [new("0", "Conservative"), new("1", "Aggressive"), new("2", "Reflex frame IDs")];
    private static readonly IReadOnlyList<SettingOption> DlssgInterpolationCounts =
    [new("1", "2X"), new("2", "3X"), new("3", "4X"), new("4", "5X"), new("5", "6X"), new("6", "7X")];
    private static readonly IReadOnlyList<SettingOption> SpoofedVendors =
    [new("0x10de", "Nvidia (0x10de)"), new("0x8086", "Intel (0x8086)"), new("0x1002", "AMD (0x1002)")];
    private static readonly IReadOnlyList<SettingOption> SpoofedDevices =
    [new("0x2684", "RTX 4090 (0x2684)"), new("0xE20B", "Intel Arc B580 (0xE20B)"), new("0x7550", "Radeon RX 9070 XT (0x7550)")];
    private static readonly IReadOnlyList<SettingOption> AnisotropySuggestions =
    new[] { 2, 4, 8, 16 }.Select(x => new SettingOption(x.ToString(), $"{x}x")).ToArray();
    private static readonly IReadOnlyList<SettingOption> FrameTimeInputSourcesV09 =
    [new("0", "Input (DLSSG/FSRFG/OptiFG)"), new("1", "Time between Present calls"), new("2", "Zero (XeFG only)")];
    private static readonly IReadOnlyList<SettingOption> AllowedFrameAheadCounts =
    [new("1", "1"), new("2", "2"), new("3", "3")];
    private static readonly IReadOnlyList<SettingOption> FpsOverlayPositionsV10 =
    [new("0", "Top left"), new("1", "Top right"), new("2", "Bottom left"), new("3", "Bottom right"), new("4", "Top center"), new("5", "Bottom center"), new("6", "Custom position")];
    private static readonly IReadOnlyList<SettingOption> FpsOverlayTypesV10 =
    [new("0", "Just FPS"), new("1", "Simple"), new("2", "Detailed"), new("3", "Detailed + graph"), new("4", "Full"), new("5", "Full + graph"), new("6", "Low latency timings"), new("7", "Custom rows")];
    private static readonly IReadOnlyList<SettingOption> FpsOverlayRows =
    new[] { "fps", "upscale", "frametime", "frametimegraph", "upscaler", "upscalergraph", "lowlatency" }.Select(x => new SettingOption(x, x)).ToArray();
    private static readonly IReadOnlyList<SettingOption> FpsOverlayFpsRows =
    [new("0", "Just FPS"), new("1", "Frame time"), new("2", "Average FPS")];
    private static readonly IReadOnlyList<SettingOption> FpsOverlayShadows =
    [new("0", "Off"), new("1", "Shadow"), new("2", "Outline")];
    private static readonly IReadOnlyList<SettingOption> FgInputs =
    [new("NoFG", "No frame generation"), new("Upscaler", "Upscaler"), new("DLSSG", "DLSSG"), new("NvngxFG", "NvngxFG"), new("FSRFG", "FSRFG"), new("FSRFG30", "FSRFG30"), new("XeFG", "XeFG")];
    private static readonly IReadOnlyList<SettingOption> FgOutputs =
    [new("NoFG", "No frame generation"), new("FSRFG", "FSRFG"), new("DLSSG", "DLSSG"), new("XeFG", "XeFG"), new("Reprojection", "Reprojection"), new("FlipMeter", "FlipMeter")];
    private static readonly IReadOnlyList<SettingOption> FgNvngxReplacementSuggestions =
    new[] { "none", "nukems", "arturs", "ffx", "combo" }.Select(x => new SettingOption(x, x)).ToArray();
    private static readonly IReadOnlyList<SettingOption> FtInputOptions =
    [new("0", "Frame generation input"), new("1", "Time between Present calls"), new("2", "Zero", "FrameGen.FGOutput=XeFG")];
    private static readonly IReadOnlyList<SettingOption> UpscalerSuggestionsDx11 =
    new[] { "xess", "xess_12", "fsr21", "fsr21_12", "fsr22", "fsr22_12", "ffx", "ffx_12", "dlss", "dlss_12", "dlssd", "fsr31", "fsr31_12" }.Select(x => new SettingOption(x, x)).ToArray();
    private static readonly IReadOnlyList<SettingOption> UpscalerSuggestionsDx12 =
    new[] { "xess", "xess_12", "fsr21", "fsr21_12", "fsr22", "fsr22_12", "ffx", "ffx_12", "dlss", "dlss_12", "dlssd", "fsr31", "fsr31_12" }.Select(x => new SettingOption(x, x)).ToArray();
    private static readonly IReadOnlyList<SettingOption> UpscalerSuggestionsVulkan =
    new[] { "xess", "xess_12", "fsr21", "fsr21_12", "fsr22", "fsr22_12", "ffx", "ffx_12", "dlss", "dlss_12", "dlssd", "fsr31", "fsr31_12" }.Select(x => new SettingOption(x, x)).ToArray();
    private static readonly IReadOnlyList<SettingOption> RrUpscalerSuggestions =
    new[] { "dlssd", "xess", "fsr21", "fsr22", "ffx", "fsr31", "dlss" }.Select(x => new SettingOption(x, x)).ToArray();
    private static readonly IReadOnlyList<SettingOption> SharpnessShaders =
    [new("rcas", "RCAS"), new("da", "Depth-based edge detection"), new("lcda", "Local-contrast depth-based edge detection")];
    private static readonly IReadOnlyList<SettingOption> ReprojectionFillModes =
    [new("strech", "Stretch"), new("dithering", "Dithering"), new("noise", "Noise"), new("debug", "Debug"), new("soft", "Soft")];
    private static readonly IReadOnlyList<SettingOption> Fsr4GpuSupportOptions =
    [new("0", "None"), new("1", "FP8"), new("2", "INT8"), new("3", "INT8_2"), new("4", "INT8Lite")];
    private static readonly IReadOnlyList<SettingOption> Fsr4ModelOptions =
    [new("0", "FP8"), new("1", "INT8"), new("2", "INT8_2"), new("3", "INT8Lite")];

    public static IReadOnlyList<SettingDefinition> Apply(OptiSchemaFamily family, IReadOnlyList<SettingDefinition> definitions) => definitions
        .Select(definition => family == OptiSchemaFamily.V09 ? ApplyV09(definition) : ApplyV10(definition))
        .ToArray();

    private static SettingDefinition ApplyV09(SettingDefinition definition) => definition.Id switch
    {
        "XeFG.InterpolationCount" => Choice(definition, XeFgInterpolationChoices, "Sets interpolated frame count."),
        "OptiFG.HUDLimit" => definition with { Minimum = 1, InputKind = SettingInputKind.Stepper, Step = 1 },
        "FrameGen.AllowedFrameAhead" => Choice(definition, AllowedFrameAheadCounts, "Number of frames the FG is allowed to be ahead of the game."),
        "QualityOverrides.QualityRatioOverrideEnabled" => Text(definition, "Enables custom upscaling ratios for each quality mode."),
        "QualityOverrides.QualityRatioDLAA" => Text(definition, "DLAA ratio. Default Auto value: 1.0.", "DLAA"),
        "QualityOverrides.QualityRatioUltraQuality" => Text(definition, "Ultra Quality ratio. Default Auto value: 1.3.", "Ultra Quality"),
        "QualityOverrides.QualityRatioQuality" => Text(definition, "Quality ratio. Default Auto value: 1.5.", "Quality"),
        "QualityOverrides.QualityRatioBalanced" => Text(definition, "Balanced ratio. Default Auto value: 1.7.", "Balanced"),
        "QualityOverrides.QualityRatioPerformance" => Text(definition, "Performance ratio. Default Auto value: 2.0.", "Performance"),
        "QualityOverrides.QualityRatioUltraPerformance" => Text(definition, "Ultra Performance ratio. Default Auto value: 3.0.", "Ultra Performance"),
        "XeSS.NetworkModel" => Choice(definition, XeSsNetworkModels, "Select XeSS network model. Currently may not have an effect."),
        "FSR.UpscalerIndex" => Choice(definition, FsrUpscalerBackends, "Selects the upscaler backend for FSR 3.X/4."),
        "FSR.FGIndex" => Choice(definition, FsrFrameGenerationBackends, "Selects the FSR frame generation backend."),
        "FSR.Fsr4Preset" => Choice(definition, Fsr4Presets, "Selects the FSR 4 quality preset."),
        "DLSS.RenderPresetForAll" => Choice(definition, DlssRenderPresets.Take(16).ToArray(), "Sets the DLSS render preset for all modes."),
        "DLSSD.RenderPresetForAll" => Choice(definition, DlssdRenderPresets.Take(6).ToArray(), "Sets the DLSSD render preset for all modes."),
        "Menu.FpsOverlayPos" => Choice(definition, FpsOverlayPositionsV09, "Sets the FPS overlay position."),
        "Menu.FpsOverlayType" => Choice(definition, FpsOverlayTypesV09, "Sets the FPS overlay type."),
        "OutputScaling.Downscaler" => Choice(definition, Downscalers, "Selects the output downscaler."),
        "Log.LogLevel" => Choice(definition, LogLevels, "Sets the log verbosity."),
        "Spoofing.SpoofedVendorId" => Choice(definition, SpoofedVendors, "Selects the GPU vendor ID to spoof."),
        "Spoofing.SpoofedDeviceId" => Choice(definition, SpoofedDevices, "Selects the GPU device ID to spoof."),
        "Anisotropy.AnisotropyOverride" => Choice(definition, AnisotropySuggestions, "Overrides maximum texture anisotropic filtering."),
        "FrameGen.FTInput" => Choice(definition, FrameTimeInputSourcesV09, "Frametime source, would affect frame pacing."),
        _ => definition
    };

    private static SettingDefinition ApplyV10(SettingDefinition definition)
    {
        if (definition.Id.StartsWith("DLSS.RenderPreset", StringComparison.Ordinal) && definition.IniKey.Name != "RenderPresetOverride")
            return V10Choice(definition, DlssRenderPresets, "DLSS render preset: 0–16 or NV_PRESET_LATEST (0x00FFFFFF).");
        if (definition.Id.StartsWith("DLSSD.RenderPreset", StringComparison.Ordinal) && definition.IniKey.Name != "RenderPresetOverride")
            return V10Choice(definition, DlssdRenderPresets, "DLSSD render preset: 0–6 or NV_PRESET_LATEST (0x00FFFFFF).");

        return definition.Id switch
        {
            "Upscalers.Dx11Upscaler" => Suggestions(definition, UpscalerSuggestionsDx11, "Built-in DX11 upscaler codes or a custom provider name are accepted.", "CodeToUpscaler mapping; unknown tokens resolve as custom provider names."),
            "Upscalers.Dx12Upscaler" => Suggestions(definition, UpscalerSuggestionsDx12, "Built-in DX12 upscaler codes or a custom provider name are accepted.", "CodeToUpscalerFfx mapping; unknown tokens resolve as custom provider names."),
            "Upscalers.VulkanUpscaler" => Suggestions(definition, UpscalerSuggestionsVulkan, "Built-in Vulkan upscaler codes or a custom provider name are accepted.", "CodeToUpscalerFfx mapping; unknown tokens resolve as custom provider names."),
            "Upscalers.RRUpscaler" => Suggestions(definition, RrUpscalerSuggestions, "DLSSD, supported regular upscalers when RRAllowRegularUpscalers is enabled, or a custom provider name.", "Custom RR providers are accepted; regular upscalers depend on RRAllowRegularUpscalers."),
            "FrameGen.FGInput" => V10Choice(definition, FgInputs, "Frame generation input token from the upstream State.h mapping."),
            "FrameGen.FGOutput" => V10Choice(definition, FgOutputs, "Frame generation output token from the upstream State.h mapping."),
            "FrameGen.FGNvngxReplacement" => Suggestions(definition, FgNvngxReplacementSuggestions, "Built-in replacement names or a custom Nvngx FG provider name.", "Applies when FGInput is NvngxFG or FGOutput is DLSSG/FlipMeter."),
            "OptiFG.HUDLimit" => definition with { Minimum = 1, InputKind = SettingInputKind.Stepper, Step = 1 },
            "FrameGen.FTInput" => V10Choice(definition, FtInputOptions, "Values 0 and 1 are generally accepted; value 2 is only applied with FGOutput=XeFG."),
            "FSR.Fsr4GpuSupport" => V10Choice(definition, Fsr4GpuSupportOptions, "Overrides detected FSR 4 GPU support; values come from the upstream FSR4Support enum."),
            "FSR.Fsr4Model" => V10Choice(definition, Fsr4ModelOptions, "FSR 4 model from the upstream FSR4Model enum."),
            "FSR.Fsr4Preset" => V10Choice(definition, Fsr4Presets, "FSR 4 preset index 0–5."),
            "QualityOverrides.QualityRatioOverrideEnabled" => Text(definition, "Enables custom upscaling ratios for each quality mode."),
            "QualityOverrides.QualityRatioDLAA" => Text(definition, "DLAA ratio. Default Auto value: 1.0.", "DLAA"),
            "QualityOverrides.QualityRatioUltraQuality" => Text(definition, "Ultra Quality ratio. Default Auto value: 1.3.", "Ultra Quality"),
            "QualityOverrides.QualityRatioQuality" => Text(definition, "Quality ratio. Default Auto value: 1.5.", "Quality"),
            "QualityOverrides.QualityRatioBalanced" => Text(definition, "Balanced ratio. Default Auto value: 1.7.", "Balanced"),
            "QualityOverrides.QualityRatioPerformance" => Text(definition, "Performance ratio. Default Auto value: 2.0.", "Performance"),
            "QualityOverrides.QualityRatioUltraPerformance" => Text(definition, "Ultra Performance ratio. Default Auto value: 3.0.", "Ultra Performance"),
            "FrameGen.AllowedFrameAhead" => V10Choice(definition, AllowedFrameAheadCounts, "Values 1–3 are accepted; values outside this range reset to Auto."),
            "XeFG.InterpolationCount" => Numeric(definition, SettingValueKind.Integer, 1, null, XeFgInterpolationChoices, "Parser minimum is 1; the usable upper count depends on runtime/output capability."),
            "XeFG.RoundingDMFG" => Numeric(definition, SettingValueKind.Integer, 0, 2, null, "Values 0–2 are accepted; values outside this range reset to Auto."),
            "XeFG.OverrideInterpolationCount" => Numeric(definition, SettingValueKind.Integer, 0, 5, null, "Values 0–5 are accepted; values outside this range reset to Auto."),
            "XeFG.FramerateTargetDMFG" => Numeric(definition, SettingValueKind.Double, 10, null, null, "Values below 10 are reset to Auto; no upper bound is applied by Config.cpp."),
            "DLSSG.InterpolationCount" => Numeric(definition, SettingValueKind.Integer, 1, 6, DlssgInterpolationCounts, "Values 1–6 are accepted."),
            "DLSSG.OverrideInterpolationCount" => Numeric(definition, SettingValueKind.Integer, 0, 6, DlssgInterpolationCounts.Prepend(new SettingOption("0", "Auto/disabled")).ToArray(), "Values 0–6 are accepted; values outside this range reset to Auto."),
            "FlipMeter.InterpolationCount" => Numeric(definition, SettingValueKind.Integer, 1, 5, null, "Values 1–5 are accepted."),
            "XeSS.NetworkModel" => Numeric(definition, SettingValueKind.Integer, null, null, XeSsNetworkModels, "Config reads an integer; the in-game menu offers model indices 0–5."),
            "Menu.Scale" => Numeric(definition, SettingValueKind.Double, 0.5, 2, null, "The INI parser clamps this continuous float to 0.5–2.0."),
            "Menu.FpsOverlayPos" => V10Choice(definition, FpsOverlayPositionsV10, "Index 0–6; 6 uses FpsOverlayPosX and FpsOverlayPosY."),
            "Menu.FpsOverlayType" => V10Choice(definition, FpsOverlayTypesV10, "Index 0–7; 6 is low-latency timings and 7 uses the custom row list."),
            "Menu.FpsOverlayRows" => Suggestions(definition, FpsOverlayRows, "Ordered comma-separated row keys; prefix a row with '-' to hide it.", "Custom ordered list: fps, upscale, frametime, frametimegraph, upscaler, upscalergraph, lowlatency."),
            "Menu.FpsOverlayFpsRow" => V10Choice(definition, FpsOverlayFpsRows, "FPS row selection index 0–2."),
            "Menu.FpsOverlayShadow" => V10Choice(definition, FpsOverlayShadows, "Shadow style index 0–2."),
            "Menu.FpsOverlayPosX" or "Menu.FpsOverlayPosY" => Numeric(definition, SettingValueKind.Double, 0, 1, null, "Custom overlay position is clamped to 0–1."),
            "Menu.FpsOverlayRowSpacing" => Numeric(definition, SettingValueKind.Double, 0, 32, null, "Parser clamps the row spacing to 0–32."),
            "Menu.FpsOverlayUpdateInterval" => Numeric(definition, SettingValueKind.Double, 0, 1000, null, "Parser clamps the update interval to 0–1000 ms."),
            "Menu.FpsOverlayAlpha" or "Menu.FpsOverlayShadowStrength" => Numeric(definition, SettingValueKind.Double, 0, 1, null, "Parser clamps this value to 0–1."),
            "Menu.FpsScale" or "Menu.FpsOverlayGraphHeight" => Numeric(definition, SettingValueKind.Double, 0.5, 3, null, "Parser clamps this value to 0.5–3."),
            "Menu.FpsOverlayFontSize" => Numeric(definition, SettingValueKind.Double, 8, 48, null, "Parser clamps font size to 8–48."),
            "Menu.FpsOverlayLetterSpacing" => Numeric(definition, SettingValueKind.Double, -2, 8, null, "Parser clamps letter spacing to -2–8."),
            "Menu.FpsOverlayLabelScale" or "Menu.FpsOverlayValueScale" => Numeric(definition, SettingValueKind.Double, 0.5, 2, null, "Parser clamps this value to 0.5–2."),
            "Menu.FpsOverlayRounding" or "Menu.Rounding" => Numeric(definition, SettingValueKind.Double, 0, 16, null, "Parser clamps this value to 0–16."),
            "Menu.FpsOverlayLabelColor" or "Menu.FpsOverlayValueColor" or "Menu.FpsOverlayGraphColor" or "Menu.FpsOverlayBGColor" or "Menu.FpsOverlayShadowColor" => Numeric(definition, SettingValueKind.Integer, int.MinValue, int.MaxValue, null, "Parsed as signed Int32, converted to UInt32, then masked to 24-bit RGB."),
            "Spoofing.SpoofedVendorId" => Numeric(definition, SettingValueKind.Integer, int.MinValue, int.MaxValue, SpoofedVendors, "Parsed as signed Int32 then converted to UInt32; decimal or hexadecimal examples are suggestions, not an allowlist."),
            "Spoofing.SpoofedDeviceId" => Numeric(definition, SettingValueKind.Integer, int.MinValue, int.MaxValue, SpoofedDevices, "Parsed as signed Int32 then converted to UInt32; decimal or hexadecimal examples are suggestions, not an allowlist."),
            "Anisotropy.AnisotropyOverride" => Numeric(definition, SettingValueKind.Integer, 1, 16, AnisotropySuggestions, "Any integer from 1–16 is accepted; the INI comment examples are suggestions."),
            "Sharpness.Shader" => V10Choice(definition, SharpnessShaders, "Shader codes from the upstream CodeToEnum mapping."),
            "Reprojection.FillMode" => V10Choice(definition, ReprojectionFillModes, "Fill mode codes from the upstream CodeToEnum mapping."),
            "fakenvapi.LatencyFlexMode" => V10Choice(definition, LatencyFlexModes, "LatencyFlex mode index 0–2."),
            "Log.LogLevel" => V10Choice(definition, LogLevels, "Log level index 0–4."),
            "OutputScaling.Downscaler" => V10Choice(definition, Downscalers, "Downscaler index 0–7."),
            "FSR.UpscalerIndex" => V10Choice(definition, FsrUpscalerBackends, "FSR upscaler backend index 0–2."),
            "FSR.FGIndex" => V10Choice(definition, FsrFrameGenerationBackends, "FSR frame generation backend index 0–1."),
            _ => definition
        };
    }

    private static SettingDefinition Choice(SettingDefinition definition, IReadOnlyList<SettingOption> options, string description) => definition with
    {
        ValueKind = SettingValueKind.Enum,
        Options = options,
        Description = description,
        InputKind = SettingInputKind.Text,
        Step = null
    };

    private static SettingDefinition V10Choice(SettingDefinition definition, IReadOnlyList<SettingOption> options, string description) => Choice(definition, options, description) with
    {
        ContractNote = description,
        SourceReader = definition.GroupId == "Menu" ? "ReadOverlaySettings" : definition.SourceReader
    };

    private static SettingDefinition Suggestions(SettingDefinition definition, IReadOnlyList<SettingOption> options, string description, string? contractNote = null) => definition with
    {
        ValueKind = SettingValueKind.String,
        Options = options,
        Description = description,
        OptionsAreSuggestions = true,
        ContractNote = contractNote,
        SourceReader = definition.GroupId == "Menu" ? "ReadOverlaySettings" : definition.SourceReader,
        Minimum = null,
        Maximum = null,
        InputKind = SettingInputKind.Text,
        Step = null
    };

    private static SettingDefinition Numeric(SettingDefinition definition, SettingValueKind kind, double? minimum, double? maximum, IReadOnlyList<SettingOption>? suggestions, string contractNote) => definition with
    {
        ValueKind = kind,
        Options = suggestions ?? [],
        Description = contractNote,
        OptionsAreSuggestions = suggestions is { Count: > 0 },
        ContractNote = contractNote,
        SourceReader = definition.GroupId == "Menu" ? "ReadOverlaySettings" : definition.SourceReader,
        Minimum = minimum,
        Maximum = maximum,
        InputKind = SettingInputKind.Text,
        Step = null
    };

    private static SettingDefinition Text(SettingDefinition definition, string description, string? displayName = null) => definition with
    {
        DisplayName = displayName ?? definition.DisplayName,
        Description = description,
        InputKind = SettingInputKind.Text,
        Step = null
    };

    private static IReadOnlyList<SettingOption> CreateRenderPresets(int maximum) =>
        Enumerable.Range(0, maximum + 1).Select(x => new SettingOption(x.ToString(), x == 0 ? "Default" : ((char)('A' + x - 1)).ToString()))
            .Append(new SettingOption("0x00FFFFFF", "Latest (0x00FFFFFF)"))
            .Append(new SettingOption("16777215", "Latest (decimal)"))
            .ToArray();
}
