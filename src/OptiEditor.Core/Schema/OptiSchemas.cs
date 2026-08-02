using System.Globalization;
using OptiEditor.Core.Ini;
using OptiEditor.Core.Models;
using OptiEditor.Core.Shortcuts;

namespace OptiEditor.Core.Schema;

public enum SettingValueKind { Boolean, Integer, Double, Enum, String, Shortcut }
public enum SettingAvailability { ExistingKeyOnly }
public enum EditorVisibility { Visible, Hidden }
public enum SettingInputKind { Text, Stepper }
public sealed record SettingOption(string Value, string Label);
public sealed record SettingGroupDefinition(string Id, string DisplayName, int Order);
public sealed record SettingDefinition(string Id, IniKey IniKey, string DisplayName, string Description, string GroupId, int Order, SettingValueKind ValueKind, bool SupportsAuto, IReadOnlyList<SettingOption> Options, string DefaultRawValue = "auto", double? Minimum = null, double? Maximum = null, bool IsAdvanced = false, SettingAvailability Availability = SettingAvailability.ExistingKeyOnly, EditorVisibility DefaultEditorVisibility = EditorVisibility.Visible, SettingInputKind InputKind = SettingInputKind.Text, double? Step = null, string SourceComment = "");
public sealed record SettingValidationResult(bool IsValid, string? Error) { public static SettingValidationResult Valid { get; } = new(true, null); }
public sealed class UnsupportedOptiSchemaException(OptiSchemaFamily family) : InvalidOperationException($"OptiScaler schema family '{family}' is not supported.");
public interface IOptiSchemaProvider { OptiSchemaFamily Family { get; } IReadOnlyList<SettingGroupDefinition> Groups { get; } IReadOnlyList<SettingDefinition> Settings { get; } SettingDefinition? FindById(string id); }

public abstract class OptiSchemaProviderBase : IOptiSchemaProvider
{
    protected OptiSchemaProviderBase(OptiSchemaFamily family)
    {
        Family = family;
        Settings = OptiSchemaOverlays.Apply(family, GeneratedOptiScalerSchemaCatalog.Create(family));
        Groups = Settings.Select(x => x.GroupId).Distinct(StringComparer.OrdinalIgnoreCase).Select((id, order) => new SettingGroupDefinition(id, id, order)).ToArray();
    }

    public OptiSchemaFamily Family { get; }
    public IReadOnlyList<SettingGroupDefinition> Groups { get; }
    public IReadOnlyList<SettingDefinition> Settings { get; }
    public SettingDefinition? FindById(string id)
    {
        var resolved = LegacyOptiSchemaIds.Resolve(id);
        return Settings.FirstOrDefault(x => string.Equals(x.Id, resolved, StringComparison.OrdinalIgnoreCase));
    }
}

public sealed class Opti09SchemaProvider() : OptiSchemaProviderBase(OptiSchemaFamily.V09);
public sealed class Opti10SchemaProvider() : OptiSchemaProviderBase(OptiSchemaFamily.V10);

public sealed class OptiSchemaResolver
{
    private readonly IOptiSchemaProvider _v09 = new Opti09SchemaProvider();
    private readonly IOptiSchemaProvider _v10 = new Opti10SchemaProvider();
    public IOptiSchemaProvider Resolve(OptiSchemaFamily family) => family switch { OptiSchemaFamily.V09 => _v09, OptiSchemaFamily.V10 => _v10, _ => throw new UnsupportedOptiSchemaException(family) };
}

public static class SettingValidator
{
    public static SettingValidationResult Validate(SettingDefinition definition, string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return new(false, "A value is required.");
        if (raw.IndexOfAny(['\r', '\n', '\0']) >= 0) return new(false, "The value contains invalid characters.");
        if (definition.SupportsAuto && string.Equals(raw, "auto", StringComparison.OrdinalIgnoreCase)) return SettingValidationResult.Valid;
        if (definition.ValueKind == SettingValueKind.Boolean && !bool.TryParse(raw, out _)) return new(false, "Select Auto, Enabled, or Disabled.");
        if (definition.ValueKind == SettingValueKind.Integer)
        {
            if (!TryParseInteger(raw, out var integer)) return new(false, "Enter a valid integer, or a hexadecimal value such as 0x14100000.");
            if (definition.Minimum is not null && integer < definition.Minimum || definition.Maximum is not null && integer > definition.Maximum) return new(false, $"Enter a value from {definition.Minimum?.ToString(CultureInfo.InvariantCulture) ?? "-∞"} to {definition.Maximum?.ToString(CultureInfo.InvariantCulture) ?? "∞"}.");
        }
        if (definition.ValueKind == SettingValueKind.Shortcut && !ShortcutValueConverter.TryParseKnown(raw, out _)) return new(false, "Enter Auto, -1, a decimal virtual key, or 0xNN.");
        if (definition.ValueKind == SettingValueKind.Double)
        {
            if (!double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var number)) return new(false, "Enter a valid number.");
            if (definition.Minimum is not null && number < definition.Minimum || definition.Maximum is not null && number > definition.Maximum) return new(false, $"Enter a value from {definition.Minimum?.ToString(CultureInfo.InvariantCulture) ?? "-∞"} to {definition.Maximum?.ToString(CultureInfo.InvariantCulture) ?? "∞"}.");
        }
        if (definition.ValueKind == SettingValueKind.Enum && !definition.Options.Any(x => string.Equals(x.Value, raw, StringComparison.OrdinalIgnoreCase))) return new(false, "Select a supported option.");
        return SettingValidationResult.Valid;
    }

    // OptiScaler accepts hexadecimal flag/ID values (e.g. DLSSG.DispatchFlags = 0x14100000)
    // for settings typed as Integer, alongside plain decimal values.
    private static bool TryParseInteger(string raw, out long value)
    {
        var negative = raw.StartsWith('-');
        var unsigned = negative ? raw[1..] : raw;
        if (unsigned.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            if (!ulong.TryParse(unsigned[2..], NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var hex)) { value = 0; return false; }
            value = negative ? -(long)hex : (long)hex;
            return true;
        }
        return long.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
    }
}
