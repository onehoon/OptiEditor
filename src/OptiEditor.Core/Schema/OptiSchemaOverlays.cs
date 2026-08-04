using OptiEditor.Core.Models;

namespace OptiEditor.Core.Schema;

// User-confirmed display semantics for stable OptiScaler INI keys.
// Keep this small: upstream source remains the authority for all other keys.
internal static class OptiSchemaOverlays
{
    private static readonly IReadOnlyList<SettingOption> XeFgInterpolationCount =
    [
        new("1", "2X"),
        new("2", "3X"),
        new("3", "4X")
    ];

    private static readonly IReadOnlyList<SettingOption> XeSsNetworkModels =
    [
        new("0", "KPSS"),
        new("1", "Splat"),
        new("2", "Model 3"),
        new("3", "Model 4"),
        new("4", "Model 5"),
        new("5", "Model 6")
    ];

    private static readonly IReadOnlyList<SettingOption> FsrUpscalerBackends =
    [
        new("0", "FSR 4.0.2"),
        new("1", "FSR 3.1.5"),
        new("2", "FSR 2.3.4")
    ];

    private static readonly IReadOnlyList<SettingOption> FpsOverlayPositions = [new("0", "Top Left"), new("1", "Top Right"), new("2", "Bottom Left"), new("3", "Bottom Right")];
    private static readonly IReadOnlyList<SettingOption> FpsOverlayTypes = [new("0", "Just FPS"), new("1", "Simple"), new("2", "Detailed"), new("3", "Detailed + Graph"), new("4", "Full"), new("5", "Full + Graph"), new("6", "Reflex timings")];
    private static readonly IReadOnlyList<SettingOption> LogLevels = [new("0", "Trace"), new("1", "Debug"), new("2", "Info"), new("3", "Warning"), new("4", "Error")];
    private static readonly IReadOnlyList<SettingOption> FsrFrameGenerationBackends = [new("0", "FSR 4.0.0"), new("1", "FSR 3.1.6")];
    private static readonly IReadOnlyList<SettingOption> Fsr4Presets = [new("0", "Native AA"), new("1", "Ultra Quality / Quality"), new("2", "Balanced"), new("3", "Performance"), new("4", "DRS"), new("5", "Ultra Performance")];
    private static readonly IReadOnlyList<SettingOption> DlssRenderPresets = [new("0", "Default"), new("1", "A"), new("2", "B"), new("3", "C"), new("4", "D"), new("5", "E"), new("6", "F"), new("7", "G"), new("8", "H"), new("9", "I"), new("10", "J"), new("11", "K"), new("12", "L"), new("13", "M"), new("14", "N"), new("15", "O")];
    private static readonly IReadOnlyList<SettingOption> DlssdRenderPresets = DlssRenderPresets.Take(6).ToArray();
    private static readonly IReadOnlyList<SettingOption> Downscalers = [new("0", "FSR 1"), new("1", "Bicubic"), new("2", "Catmull-Rom"), new("3", "Lanczos 2"), new("4", "Lanczos 3"), new("5", "Kaiser 2"), new("6", "Kaiser 3"), new("7", "MAGIC")];
    private static readonly IReadOnlyList<SettingOption> LatencyFlexModes = [new("0", "Conservative"), new("1", "Aggressive"), new("2", "Reflex frame IDs")];
    private static readonly IReadOnlyList<SettingOption> Fsr4ForceModels = [new("0", "No override"), new("1", "FP8"), new("2", "INT8")];
    private static readonly IReadOnlyList<SettingOption> DlssgInterpolationCounts = [new("1", "2X"), new("2", "3X"), new("3", "4X"), new("4", "5X"), new("5", "6X")];
    private static readonly IReadOnlyList<SettingOption> SpoofedVendors = [new("0x10de", "Nvidia (0x10de)"), new("0x8086", "Intel (0x8086)"), new("0x1002", "AMD (0x1002)")];
    private static readonly IReadOnlyList<SettingOption> SpoofedDevices = [new("0x2684", "RTX 4090 (0x2684)"), new("0xE20B", "Intel Arc B580 (0xE20B)"), new("0x7550", "Radeon RX 9070 XT (0x7550)")];
    private static readonly IReadOnlyList<SettingOption> AnisotropyLevels = [new("2", "2x"), new("4", "4x"), new("8", "8x"), new("16", "16x")];
    private static readonly IReadOnlyList<SettingOption> FrameTimeInputSources = [new("0", "Input (DLSSG/FSRFG/OptiFG)"), new("1", "Time between Present calls"), new("2", "Zero (XeFG only)")];
    private static readonly IReadOnlyList<SettingOption> AllowedFrameAheadCounts = [new("1", "1"), new("2", "2"), new("3", "3")];

    public static IReadOnlyList<SettingDefinition> Apply(OptiSchemaFamily family, IReadOnlyList<SettingDefinition> definitions) => definitions
        .Select(definition => definition.Id switch
        {
            "XeFG.InterpolationCount" => Choice(definition, XeFgInterpolationCount, "Sets interpolated frame count."),
            "OptiFG.HUDLimit" => definition with
            {
                Minimum = 1,
                InputKind = SettingInputKind.Stepper,
                Step = 1
            },
            // MANUAL OVERRIDE: upstream Config.cpp resets this to auto when it's outside 1-3
            // (release/0.9 and master both: `if (value < 1 || value > 3) reset()`); confirmed
            // by hand as a discrete 1/2/3 choice, not a free-form range. Do not revert this to
            // a plain Minimum/Maximum bound on schema resync.
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
            "DLSS.RenderPresetForAll" => Choice(definition, DlssRenderPresets, "Sets the DLSS render preset for all modes."),
            "DLSSD.RenderPresetForAll" => Choice(definition, DlssdRenderPresets, "Sets the DLSSD render preset for all modes."),
            "Menu.FpsOverlayPos" => Choice(definition, FpsOverlayPositions, "Sets the FPS overlay position."),
            "Menu.FpsOverlayType" => Choice(definition, FpsOverlayTypes, "Sets the FPS overlay type."),
            "OutputScaling.Downscaler" => Choice(definition, Downscalers, "Selects the output downscaler."),
            "Log.LogLevel" => Choice(definition, LogLevels, "Sets the log verbosity."),
            "Spoofing.SpoofedVendorId" => Choice(definition, SpoofedVendors, "Selects the GPU vendor ID to spoof."),
            "Spoofing.SpoofedDeviceId" => Choice(definition, SpoofedDevices, "Selects the GPU device ID to spoof."),
            "Anisotropy.AnisotropyOverride" => Choice(definition, AnisotropyLevels, "Overrides maximum texture anisotropic filtering."),
            // MANUAL OVERRIDE: upstream declares FTInput as a free-form string, but Config.cpp only
            // accepts "0"/"1"/"2" -- confirmed by hand against the real accepted values. Do not remove
            // this on schema resync or let an automated pass "clean it up" back to a text field.
            "FrameGen.FTInput" => Choice(definition, FrameTimeInputSources, "Frametime source, would affect frame pacing."),
            "DLSSG.InterpolationCount" when family == OptiSchemaFamily.V10 => Choice(definition, DlssgInterpolationCounts, "Sets interpolated frame count."),
            "fakenvapi.LatencyFlexMode" when family == OptiSchemaFamily.V10 => Choice(definition, LatencyFlexModes, "Selects the latency flexibility mode."),
            "FSR.Fsr4ForceModel" when family == OptiSchemaFamily.V10 => Choice(definition, Fsr4ForceModels, "Forces an FSR 4 model precision override."),
            _ => definition
        })
        .ToArray();

    private static SettingDefinition Choice(SettingDefinition definition, IReadOnlyList<SettingOption> options, string description) => definition with
    {
        ValueKind = SettingValueKind.Enum,
        Options = options,
        Description = description,
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
}
