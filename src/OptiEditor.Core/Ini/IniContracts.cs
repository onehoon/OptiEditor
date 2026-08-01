using System.Globalization;
using System.Text;

namespace OptiEditor.Core.Ini;

public readonly record struct IniKey(string Section, string Name);
public enum IniValueMode { Auto, Explicit }
public readonly record struct IniValue<T>(IniValueMode Mode, T? Value);
public sealed record IniEntry(IniKey Key, string Value, int LineNumber);
public enum IniDiagnosticSeverity { Warning, Error }
public sealed record IniDiagnostic(IniDiagnosticSeverity Severity, int LineNumber, string Message);
public enum MissingSectionBehavior { Fail, Create }
public sealed record IniPatch(IniKey Key, string NewValue, MissingSectionBehavior MissingSectionBehavior = MissingSectionBehavior.Fail);
public sealed record IniPatchResult(IniKey Key, bool WasChanged, bool WasInserted, bool HadDuplicateEntries, string? OldValue, string? NewValue, int? ModifiedLineNumber, string? Error);
public sealed record IniFileSnapshot(string FullPath, long FileLength, DateTime LastWriteTimeUtc, string ContentHash);
public sealed record IniLoadResult(IniDocument Document, IniFileSnapshot Snapshot, Encoding Encoding, bool HasBom, string DominantLineEnding, IReadOnlyList<IniDiagnostic> Warnings);
public sealed record IniSaveResult(bool Success, string? BackupPath, IniFileSnapshot? Snapshot, IReadOnlyList<IniKey> ChangedKeys, string? Error);

public interface IIniDocumentReader
{
    bool Contains(IniKey key); string? GetRawValue(IniKey key); IniEntry? GetEffectiveEntry(IniKey key);
    IReadOnlyList<IniEntry> GetAllEntries(IniKey key); IReadOnlyList<string> GetSectionNames(); IReadOnlyList<IniEntry> GetEntries(string section);
}

public sealed class IniFileChangedExternallyException(string message = "The INI file was changed externally.") : IOException(message);
public sealed class IniSaveException(string message, Exception? inner = null) : IOException(message, inner);
