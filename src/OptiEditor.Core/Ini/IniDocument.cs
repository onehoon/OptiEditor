using System.Globalization;
using System.Text;

namespace OptiEditor.Core.Ini;

public sealed class IniDocument : IIniDocumentReader
{
    private readonly List<IniLine> _lines;
    private readonly List<IniDiagnostic> _diagnostics;
    private static readonly IniKeyComparer KeyComparer = new();
    internal IniDocument(IEnumerable<IniLine> lines, Encoding encoding, bool hasBom, string dominantLineEnding, IEnumerable<IniDiagnostic> diagnostics)
    { _lines = lines.ToList(); Encoding = encoding; HasBom = hasBom; DominantLineEnding = dominantLineEnding; _diagnostics = diagnostics.ToList(); }
    public Encoding Encoding { get; }
    public bool HasBom { get; }
    public string DominantLineEnding { get; }
    public IReadOnlyList<IniDiagnostic> Diagnostics => _diagnostics;
    public bool IsDirty => _lines.OfType<IniKeyValueLine>().Any(x => x.IsModified);
    public IReadOnlyCollection<IniKey> ModifiedKeys => _lines.OfType<IniKeyValueLine>().Where(x => x.IsModified).Select(x => new IniKey(x.SectionName, x.KeyName)).Distinct(KeyComparer).ToArray();
    public bool Contains(IniKey key) => GetEffectiveLine(key) is not null;
    public string? GetRawValue(IniKey key) => GetEffectiveLine(key)?.Value;
    public IniEntry? GetEffectiveEntry(IniKey key) { var line = GetEffectiveLine(key); return line is null ? null : Entry(line); }
    public IReadOnlyList<IniEntry> GetAllEntries(IniKey key) => MatchingLines(key).Select(Entry).ToArray();
    public IReadOnlyList<string> GetSectionNames() => _lines.OfType<IniSectionLine>().Select(x => x.SectionName).ToArray();
    public IReadOnlyList<IniEntry> GetEntries(string section) => _lines.OfType<IniKeyValueLine>().Where(x => string.Equals(x.SectionName, section, StringComparison.OrdinalIgnoreCase)).Select(Entry).ToArray();
    public bool TryGetBoolean(IniKey key, out bool value) => bool.TryParse(GetRawValue(key), out value);
    public bool TryGetInt32(IniKey key, out int value) => int.TryParse(GetRawValue(key), NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
    public bool TryGetDouble(IniKey key, out double value) => double.TryParse(GetRawValue(key), NumberStyles.Float, CultureInfo.InvariantCulture, out value);
    public bool TryGetHexInt32(IniKey key, out int value)
    { var raw = GetRawValue(key); if (raw?.StartsWith("0x", StringComparison.OrdinalIgnoreCase) == true) raw = raw[2..]; return int.TryParse(raw, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out value); }
    public IniValue<string> GetValue(IniKey key) { var raw = GetRawValue(key); return string.Equals(raw, "auto", StringComparison.OrdinalIgnoreCase) ? new(IniValueMode.Auto, null) : new(IniValueMode.Explicit, raw); }
    public IniPatchResult ApplyPatch(IniPatch patch)
    {
        if (string.IsNullOrWhiteSpace(patch.Key.Section) || string.IsNullOrWhiteSpace(patch.Key.Name)) return Fail(patch, "Section and key are required.");
        if (patch.NewValue.IndexOfAny(['\r', '\n', '\0']) >= 0) return Fail(patch, "INI values cannot contain line breaks or null characters.");
        var all = MatchingLines(patch.Key).ToList();
        if (all.Count > 0)
        {
            var line = all[^1]; if (line.Value == patch.NewValue) return new(patch.Key, false, false, all.Count > 1, line.Value, patch.NewValue, line.OriginalLineNumber, null);
            var old = line.Value; line.SetValue(patch.NewValue); return new(patch.Key, true, false, all.Count > 1, old, patch.NewValue, line.OriginalLineNumber, null);
        }
        var sectionIndex = _lines.FindLastIndex(x => x is IniSectionLine section && string.Equals(section.SectionName, patch.Key.Section, StringComparison.OrdinalIgnoreCase));
        if (sectionIndex < 0) return Fail(patch, "The target section does not exist.");
        var insertAt = sectionIndex + 1; while (insertAt < _lines.Count && _lines[insertAt] is not IniSectionLine) insertAt++;
        // The line directly before the insertion point can be the file's last
        // physical line, which parses with an empty LineEnding when the file
        // has no trailing newline. RenderText concatenates each line's text
        // directly onto its own ending, so inserting after it without first
        // giving it a real ending would glue the new key onto the same line.
        var previousLine = _lines[insertAt - 1];
        if (previousLine.LineEnding.Length == 0) previousLine.LineEnding = DominantLineEnding;
        _lines.Insert(insertAt, new IniKeyValueLine(0, "", DominantLineEnding, patch.Key.Section, patch.Key.Name, patch.Key.Name + "=", patch.NewValue, "", true));
        return new(patch.Key, true, true, false, null, patch.NewValue, null, null);
    }
    public IReadOnlyList<IniPatchResult> ApplyPatches(IEnumerable<IniPatch> patches) => patches.Select(ApplyPatch).ToArray();
    public bool Revert(IniKey key)
    {
        var changed = false;
        for (var i = _lines.Count - 1; i >= 0; i--)
            if (_lines[i] is IniKeyValueLine line && KeyComparer.Equals(new(line.SectionName, line.KeyName), key))
                if (line.IsInserted) { _lines.RemoveAt(i); changed = true; } else if (line.IsModified) { line.Revert(); changed = true; }
        return changed;
    }
    public void RevertAll() { for (var i = _lines.Count - 1; i >= 0; i--) if (_lines[i] is IniKeyValueLine line) { if (line.IsInserted) _lines.RemoveAt(i); else line.Revert(); } }
    public IniDocument Clone() => new(_lines.Select(x => x.Clone()), Encoding, HasBom, DominantLineEnding, _diagnostics);
    public string RenderText() => string.Concat(_lines.Select(x => x.Render() + x.LineEnding));
    public byte[] RenderBytes()
    {
        var textBytes = Encoding.GetBytes(RenderText()); if (!HasBom) return textBytes;
        var preamble = Encoding.GetPreamble(); return preamble.Length == 0 ? textBytes : preamble.Concat(textBytes).ToArray();
    }
    internal void AddDiagnostic(IniDiagnostic diagnostic) => _diagnostics.Add(diagnostic);
    private IEnumerable<IniKeyValueLine> MatchingLines(IniKey key) => _lines.OfType<IniKeyValueLine>().Where(x => KeyComparer.Equals(new(x.SectionName, x.KeyName), key));
    private IniKeyValueLine? GetEffectiveLine(IniKey key) => MatchingLines(key).LastOrDefault();
    private static IniEntry Entry(IniKeyValueLine line) => new(new(line.SectionName, line.KeyName), line.Value, line.OriginalLineNumber);
    private static IniPatchResult Fail(IniPatch patch, string error) => new(patch.Key, false, false, false, null, patch.NewValue, null, error);
    private sealed class IniKeyComparer : IEqualityComparer<IniKey> { public bool Equals(IniKey x, IniKey y) => string.Equals(x.Section, y.Section, StringComparison.OrdinalIgnoreCase) && string.Equals(x.Name, y.Name, StringComparison.OrdinalIgnoreCase); public int GetHashCode(IniKey value) => HashCode.Combine(StringComparer.OrdinalIgnoreCase.GetHashCode(value.Section), StringComparer.OrdinalIgnoreCase.GetHashCode(value.Name)); }
}
