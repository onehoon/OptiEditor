using System.Text;
using System.Text.RegularExpressions;

namespace OptiEditor.Core.Ini;

public static partial class IniParser
{
    [GeneratedRegex(@"^(?<leading>\s*)\[(?<name>[^\]]+)\](?<tail>\s*)$")]
    private static partial Regex SectionPattern();
    [GeneratedRegex(@"^(?<leading>\s*)(?<key>[^=;#\s][^=]*?)(?<before>\s*)=(?<after>\s*)(?<value>.*)$")]
    private static partial Regex KeyPattern();

    public static IniDocument Parse(string text, Encoding encoding, bool hasBom)
    {
        var lines = new List<IniLine>(); var diagnostics = new List<IniDiagnostic>(); string? section = null; var endings = new List<string>();
        var position = 0; var lineNumber = 1;
        while (position < text.Length)
        {
            var start = position; while (position < text.Length && text[position] is not '\r' and not '\n') position++;
            var body = text[start..position]; var ending = "";
            if (position < text.Length) { ending = text[position++] == '\r' && position < text.Length && text[position] == '\n' ? "\r\n" : text[position - 1].ToString(); endings.Add(ending); }
            lines.Add(ParseLine(lineNumber++, body, ending, ref section, diagnostics));
        }
        if (text.Length == 0) { }
        var dominant = endings.GroupBy(x => x).OrderByDescending(x => x.Count()).Select(x => x.Key).FirstOrDefault() ?? "\r\n";
        if (endings.Distinct().Count() > 1) diagnostics.Add(new(IniDiagnosticSeverity.Warning, 0, "Mixed line endings were preserved."));
        return new IniDocument(lines, encoding, hasBom, dominant, diagnostics);
    }

    private static IniLine ParseLine(int number, string body, string ending, ref string? section, List<IniDiagnostic> diagnostics)
    {
        if (string.IsNullOrWhiteSpace(body)) return new IniRawLine(number, body, ending, IniLineKind.Blank);
        if (body.TrimStart().StartsWith(';') || body.TrimStart().StartsWith('#')) return new IniRawLine(number, body, ending, IniLineKind.Comment);
        var sectionMatch = SectionPattern().Match(body);
        if (sectionMatch.Success) { section = sectionMatch.Groups["name"].Value.Trim(); return new IniSectionLine(number, body, ending, section); }
        var keyMatch = KeyPattern().Match(body);
        if (!keyMatch.Success) { diagnostics.Add(new(IniDiagnosticSeverity.Warning, number, "Unknown INI line was preserved.")); return new IniRawLine(number, body, ending, IniLineKind.Unknown); }
        if (section is null) diagnostics.Add(new(IniDiagnosticSeverity.Warning, number, "Key found before any section."));
        var rawValue = keyMatch.Groups["value"].Value; var split = FindCommentStart(rawValue); var value = split < 0 ? rawValue.TrimEnd() : rawValue[..split].TrimEnd();
        var suffix = rawValue[value.Length..];
        var key = keyMatch.Groups["key"].Value.TrimEnd();
        if (key.Length == 0) { diagnostics.Add(new(IniDiagnosticSeverity.Warning, number, "Empty key name was preserved.")); return new IniRawLine(number, body, ending, IniLineKind.Unknown); }
        var prefix = keyMatch.Groups["leading"].Value + keyMatch.Groups["key"].Value + keyMatch.Groups["before"].Value + "=" + keyMatch.Groups["after"].Value;
        return new IniKeyValueLine(number, body, ending, section ?? "", key, prefix, value, suffix);
    }
    private static int FindCommentStart(string value) { for (var i = 1; i < value.Length; i++) if ((value[i] == ';' || value[i] == '#') && char.IsWhiteSpace(value[i - 1])) return i; return -1; }
}
