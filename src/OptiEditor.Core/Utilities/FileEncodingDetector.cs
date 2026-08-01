using System.Text;

namespace OptiEditor.Core.Utilities;

public sealed record DetectedEncoding(Encoding Encoding, bool HasBom);
public static class FileEncodingDetector
{
    public static DetectedEncoding Detect(ReadOnlySpan<byte> bytes)
    {
        if (bytes.StartsWith(new byte[] { 0xEF, 0xBB, 0xBF })) return new(new UTF8Encoding(true, true), true);
        if (bytes.StartsWith(new byte[] { 0xFF, 0xFE })) return new(new UnicodeEncoding(false, true, true), true);
        if (bytes.StartsWith(new byte[] { 0xFE, 0xFF })) return new(new UnicodeEncoding(true, true, true), true);
        try { _ = new UTF8Encoding(false, true).GetString(bytes); return new(new UTF8Encoding(false, true), false); }
        catch (DecoderFallbackException) { throw new InvalidDataException("The INI file encoding is unsupported or ambiguous."); }
    }
}
