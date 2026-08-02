using OptiEditor.Core.Models;

namespace OptiEditor.Core.Presets;

public interface IBuiltInPresetProvider
{
    IReadOnlyList<PresetDefinition> GetAll();
}

public sealed class BuiltInPresetProvider : IBuiltInPresetProvider
{
    private static readonly DateTimeOffset BuiltInDate = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    public IReadOnlyList<PresetDefinition> GetAll() =>
    [
        new(Guid.Parse("cff47ac0-65aa-488b-a208-f4cfbb4abfdb"), "FSR4.1.1 for AMD RDNA2", "Only for AMD RDNA2 GPU", OptiSchemaFamily.V10, PresetSource.BuiltIn, [new("FSR.Fsr4ForceModel", "2"), new("Plugins.LoadCustomAmdxc64OnRdna2", "true")], BuiltInDate, BuiltInDate),
        new(Guid.Parse("efcb722c-cfec-4e77-af11-dd89205c28c4"), "FSR4.1.1 for Opti 0.9.x", null, OptiSchemaFamily.V09, PresetSource.BuiltIn, [new("FSR.Fsr4ForceEnableInt8", "true")], BuiltInDate, BuiltInDate),
        new(Guid.Parse("b3dfd5bc-733b-4590-ad9f-da0d723babb3"), "DLSSG to XeFG", "DLSS Streamline to XeFG", OptiSchemaFamily.V09, PresetSource.BuiltIn, [new("FrameGen.Enabled", "true"), new("FrameGen.FGInput", "dlssg"), new("FrameGen.FGOutput", "xefg")], BuiltInDate, BuiltInDate),
        new(Guid.Parse("6b9d49aa-5ec3-41a0-bd4f-8e81cdd5f1ba"), "DLSSG to XeFG", "DLSSG Streamline to XeFG", OptiSchemaFamily.V10, PresetSource.BuiltIn, [new("FrameGen.Enabled", "true"), new("FrameGen.FGInput", "dlssg"), new("FrameGen.FGOutput", "xefg")], BuiltInDate, BuiltInDate),
        new(Guid.Parse("53cd318a-cbc7-4b77-8d1f-6d0cc8aa1cdc"), "FSRFG to XeFG", null, OptiSchemaFamily.V09, PresetSource.BuiltIn, [new("FrameGen.Enabled", "true"), new("FrameGen.FGInput", "fsrfg"), new("FrameGen.FGOutput", "xefg")], BuiltInDate, BuiltInDate),
        new(Guid.Parse("0c0c3e82-6588-4c3e-8be4-893a3be7956d"), "FSRFG to XeFG", null, OptiSchemaFamily.V10, PresetSource.BuiltIn, [new("FrameGen.Enabled", "true"), new("FrameGen.FGInput", "fsrfg"), new("FrameGen.FGOutput", "xefg")], BuiltInDate, BuiltInDate),
        new(Guid.Parse("99d411a9-1abe-4c81-bd52-ad5864f83ab2"), "Upscaler to XeFG", null, OptiSchemaFamily.V09, PresetSource.BuiltIn, [new("FrameGen.Enabled", "true"), new("FrameGen.FGInput", "upscaler"), new("FrameGen.FGOutput", "xefg")], BuiltInDate, BuiltInDate),
        new(Guid.Parse("e270c18e-1e8c-440b-a07d-55ebb821c76e"), "Upscaler to XeFG", null, OptiSchemaFamily.V10, PresetSource.BuiltIn, [new("FrameGen.Enabled", "true"), new("FrameGen.FGInput", "upscaler"), new("FrameGen.FGOutput", "xefg")], BuiltInDate, BuiltInDate),
        new(Guid.Parse("0b4aecf1-ed91-45d6-bdd4-8f7917ced83f"), "OptiPatcher", "Enable LoadASI Plugins", OptiSchemaFamily.V09, PresetSource.BuiltIn, [new("Plugins.LoadAsiPlugins", "true")], BuiltInDate, BuiltInDate),
        new(Guid.Parse("1d3e4199-9479-4167-8436-ba6003c2f2ef"), "OptiPatcher", "Enable LoadASI Plugins", OptiSchemaFamily.V10, PresetSource.BuiltIn, [new("Plugins.LoadAsiPlugins", "true")], BuiltInDate, BuiltInDate),
        new(Guid.Parse("6e9704f0-04b2-4fbf-ac46-6cff3be68023"), "Log", "Enable Log", OptiSchemaFamily.V09, PresetSource.BuiltIn, [new("Log.LogToFile", "true")], BuiltInDate, BuiltInDate),
        new(Guid.Parse("2b7ec483-7d1d-4364-bc5b-41f985987c64"), "Log", "Enable Log", OptiSchemaFamily.V10, PresetSource.BuiltIn, [new("Log.LogToFile", "true")], BuiltInDate, BuiltInDate)
    ];
}
