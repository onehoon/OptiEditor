using System.Globalization;
using OptiEditor.Core.Shortcuts;

namespace OptiEditor.Core.Schema;

public static class SettingValueComparer
{
    public static bool AreEquivalent(SettingDefinition definition, string left, string right) => definition.ValueKind switch
    {
        SettingValueKind.Boolean => bool.TryParse(left, out var a) && bool.TryParse(right, out var b) ? a == b : string.Equals(left, right, StringComparison.OrdinalIgnoreCase),
        SettingValueKind.Integer => int.TryParse(left, NumberStyles.Integer, CultureInfo.InvariantCulture, out var a) && int.TryParse(right, NumberStyles.Integer, CultureInfo.InvariantCulture, out var b) ? a == b : false,
        SettingValueKind.Double => double.TryParse(left, NumberStyles.Float, CultureInfo.InvariantCulture, out var a) && double.TryParse(right, NumberStyles.Float, CultureInfo.InvariantCulture, out var b) ? a.Equals(b) : false,
        SettingValueKind.Shortcut => ShortcutValueConverter.TryParseKnown(left, out var a) && ShortcutValueConverter.TryParseKnown(right, out var b) && a.Mode == b.Mode && a.VirtualKeyCode == b.VirtualKeyCode,
        SettingValueKind.Enum => string.Equals(left, right, StringComparison.OrdinalIgnoreCase),
        _ => string.Equals(left, right, StringComparison.Ordinal)
    };
}
