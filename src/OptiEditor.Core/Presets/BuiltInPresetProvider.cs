namespace OptiEditor.Core.Presets;

public interface IBuiltInPresetProvider
{
    IReadOnlyList<PresetDefinition> GetAll();
}

public sealed class BuiltInPresetProvider : IBuiltInPresetProvider
{
    public IReadOnlyList<PresetDefinition> GetAll() => [];
}
