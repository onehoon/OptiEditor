using OptiEditor.Core.Ini;

namespace OptiEditor.Core.Schema;

public sealed class SettingValueBinding
{
    public SettingValueBinding(SettingDefinition definition, string rawValue)
    { Definition = definition; OriginalRawValue = rawValue; CurrentRawValue = rawValue; Validate(); }
    public SettingDefinition Definition { get; }
    public string OriginalRawValue { get; }
    public string CurrentRawValue { get; private set; }
    public bool IsModified => !string.Equals(OriginalRawValue, CurrentRawValue, StringComparison.Ordinal);
    public bool IsUnknownValue => !string.Equals(CurrentRawValue, "auto", StringComparison.OrdinalIgnoreCase) && Definition.ValueKind == SettingValueKind.Enum && !Definition.Options.Any(x => string.Equals(x.Value, CurrentRawValue, StringComparison.OrdinalIgnoreCase));
    public string? ValidationError { get; private set; }
    // Existing INI values we do not understand must remain saveable until the
    // user changes them. This preserves forward-compatible OptiScaler values.
    public bool HasValidationError => IsModified && ValidationError is not null;
    public void SetRawValue(string value) { CurrentRawValue = value; Validate(); }
    public void Revert() { CurrentRawValue = OriginalRawValue; Validate(); }
    public IniPatch ToPatch() => new(Definition.IniKey, CurrentRawValue);
    private void Validate() { var result = SettingValidator.Validate(Definition, CurrentRawValue); ValidationError = result.Error; }
}
public static class SettingBindingFactory
{
    public static IReadOnlyList<SettingValueBinding> CreateVisible(IOptiSchemaProvider provider, IniDocument document, ICollection<IniDiagnostic>? diagnostics = null) => provider.Settings.Where(x => document.Contains(x.IniKey)).Select(x => new SettingValueBinding(x, document.GetRawValue(x.IniKey)!)).ToArray();
}
