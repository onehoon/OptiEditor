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
        new(
            Guid.Parse("cff47ac0-65aa-488b-a208-f4cfbb4abfdb"),
            "FSR4.1.1 for AMD RDNA2",
            "Only for AMD RDNA2 GPU",
            OptiSchemaFamily.V10,
            PresetSource.BuiltIn,
            [new("FSR.Fsr4ForceModel", "2"), new("Plugins.LoadCustomAmdxc64OnRdna2", "true")],
            BuiltInDate,
            BuiltInDate),
        new(
            Guid.Parse("efcb722c-cfec-4e77-af11-dd89205c28c4"),
            "FSR4.1.1 for Opti 0.9.x",
            null,
            OptiSchemaFamily.V09,
            PresetSource.BuiltIn,
            [new("FSR.Fsr4ForceEnableInt8", "true")],
            BuiltInDate,
            BuiltInDate)
    ];
}
