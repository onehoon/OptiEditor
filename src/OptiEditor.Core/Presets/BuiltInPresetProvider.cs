using OptiEditor.Core.Models;

namespace OptiEditor.Core.Presets;
public interface IBuiltInPresetProvider { IReadOnlyList<PresetDefinition> GetAll(); }
public sealed class BuiltInPresetProvider : IBuiltInPresetProvider
{
    public IReadOnlyList<PresetDefinition> GetAll() => [.. Families().SelectMany(f => new[] { Create(f, "default", "FSR FG Frame Pacing – Default", "Uses the standard FidelityFX frame-pacing values.", "0.1", "0.1"), Create(f, "set-a", "FSR FG Frame Pacing – Set A", "Uses a larger safety margin with the standard variance factor.", "0.75", "0.1"), Create(f, "set-b", "FSR FG Frame Pacing – Set B", "Uses a small safety margin and a higher variance factor.", "0.01", "0.3") })];
    private static IEnumerable<OptiSchemaFamily> Families() { yield return OptiSchemaFamily.V09; yield return OptiSchemaFamily.V10; }
    private static PresetDefinition Create(OptiSchemaFamily family, string suffix, string name, string description, string safety, string variance)
    { var id = new Guid(System.Security.Cryptography.MD5.HashData(System.Text.Encoding.UTF8.GetBytes($"builtin.fsrfg.{suffix}.{family}"))); var now = DateTimeOffset.UnixEpoch; return new(id, name, description, family, PresetSource.BuiltIn, [new("fsrfg.pacing", "true"), new("fsrfg.safety-margin", safety), new("fsrfg.variance", variance)], now, now); }
}
