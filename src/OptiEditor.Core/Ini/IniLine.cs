namespace OptiEditor.Core.Ini;

public enum IniLineKind { Blank, Comment, Section, KeyValue, Unknown }

public abstract class IniLine(int originalLineNumber, string originalText, string lineEnding)
{
    public int OriginalLineNumber { get; } = originalLineNumber;
    public string OriginalText { get; } = originalText;
    // The ending this line parsed with, kept so a backfill (below) can be
    // undone later. Never reassigned after construction.
    public string OriginalLineEnding { get; } = lineEnding;
    // Settable internally only: a line that was the last line of a file with
    // no trailing newline parses with LineEnding = "". If a new line is later
    // inserted after it, that empty ending must be backfilled first, or the
    // two lines render concatenated onto one physical line (see
    // IniDocument.ApplyPatch). If that inserted line is later reverted and
    // this is once again the file's last line, IniDocument restores
    // LineEnding from OriginalLineEnding to undo the backfill.
    public string LineEnding { get; internal set; } = lineEnding;
    public abstract IniLineKind Kind { get; }
    public abstract string Render();
    public abstract IniLine Clone();
}

public sealed class IniRawLine(int lineNumber, string text, string ending, IniLineKind kind) : IniLine(lineNumber, text, ending)
{
    public override IniLineKind Kind { get; } = kind;
    public override string Render() => OriginalText;
    public override IniLine Clone() { var copy = new IniRawLine(OriginalLineNumber, OriginalText, OriginalLineEnding, Kind); copy.LineEnding = LineEnding; return copy; }
}

public sealed class IniSectionLine(int lineNumber, string text, string ending, string sectionName) : IniLine(lineNumber, text, ending)
{
    public string SectionName { get; } = sectionName;
    public override IniLineKind Kind => IniLineKind.Section;
    public override string Render() => OriginalText;
    public override IniLine Clone() { var copy = new IniSectionLine(OriginalLineNumber, OriginalText, OriginalLineEnding, SectionName); copy.LineEnding = LineEnding; return copy; }
}

public sealed class IniKeyValueLine : IniLine
{
    private readonly string _prefix;
    private readonly string _suffix;
    public IniKeyValueLine(int lineNumber, string originalText, string ending, string sectionName, string keyName, string prefix, string value, string suffix, bool inserted = false)
        : base(lineNumber, originalText, ending) { SectionName = sectionName; KeyName = keyName; _prefix = prefix; OriginalValue = value; Value = value; _suffix = suffix; IsInserted = inserted; }
    public string SectionName { get; }
    public string KeyName { get; }
    public string OriginalValue { get; }
    public string Value { get; private set; }
    public bool IsInserted { get; }
    public bool IsModified => IsInserted || !string.Equals(Value, OriginalValue, StringComparison.Ordinal);
    public override IniLineKind Kind => IniLineKind.KeyValue;
    public void SetValue(string value) => Value = value;
    public void Revert() => SetValue(OriginalValue);
    public override string Render() => IsModified ? _prefix + Value + _suffix : OriginalText;
    public override IniLine Clone() { var copy = new IniKeyValueLine(OriginalLineNumber, OriginalText, OriginalLineEnding, SectionName, KeyName, _prefix, OriginalValue, _suffix, IsInserted); copy.LineEnding = LineEnding; copy.SetValue(Value); return copy; }
}
