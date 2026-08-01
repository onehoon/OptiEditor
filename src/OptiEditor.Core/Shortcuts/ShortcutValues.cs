using System.Globalization;

namespace OptiEditor.Core.Shortcuts;
public enum ShortcutValueMode { Auto, Disabled, Key, Unknown }
public sealed record ShortcutValue(ShortcutValueMode Mode, int? VirtualKeyCode, string? OriginalRawValue);
public sealed record ShortcutParseResult(ShortcutValue Value, string? Warning = null);
public interface IShortcutValueConverter { ShortcutParseResult Parse(string rawValue); string Format(ShortcutValue value); }
public sealed class ShortcutValueConverter : IShortcutValueConverter
{
    public static bool TryParseKnown(string rawValue, out ShortcutValue value)
    {
        var parsed = new ShortcutValueConverter().Parse(rawValue);
        value = parsed.Value;
        return value.Mode is not ShortcutValueMode.Unknown;
    }
    public ShortcutParseResult Parse(string rawValue)
    {
        if (string.Equals(rawValue, "auto", StringComparison.OrdinalIgnoreCase)) return new(new(ShortcutValueMode.Auto, null, rawValue));
        if (rawValue == "-1") return new(new(ShortcutValueMode.Disabled, null, rawValue));
        var hex = rawValue.StartsWith("0x", StringComparison.OrdinalIgnoreCase); var text = hex ? rawValue[2..] : rawValue;
        if (int.TryParse(text, hex ? NumberStyles.AllowHexSpecifier : NumberStyles.Integer, CultureInfo.InvariantCulture, out var code) && code >= 0 && code <= 255) return new(new(ShortcutValueMode.Key, code, rawValue));
        return new(new(ShortcutValueMode.Unknown, null, rawValue), "Unknown shortcut value.");
    }
    public string Format(ShortcutValue value) => value.Mode switch { ShortcutValueMode.Auto => "auto", ShortcutValueMode.Disabled => "-1", ShortcutValueMode.Key when value.VirtualKeyCode is { } code => $"0x{code:X2}", _ => value.OriginalRawValue ?? "auto" };
}
public static class ShortcutConflictDetector
{
    public static IReadOnlyDictionary<string, IReadOnlyList<string>> FindConflicts(IEnumerable<(string Id, ShortcutValue Value)> shortcuts)
    {
        var matches = shortcuts.Where(x => x.Value.Mode == ShortcutValueMode.Key && x.Value.VirtualKeyCode is not null).GroupBy(x => x.Value.VirtualKeyCode!.Value).Where(x => x.Count() > 1).SelectMany(x => x.Select(current => new { current.Id, Others = x.Where(other => other.Id != current.Id).Select(other => other.Id).ToArray() }));
        return matches.ToDictionary(x => x.Id, x => (IReadOnlyList<string>)x.Others, StringComparer.OrdinalIgnoreCase);
    }
}
