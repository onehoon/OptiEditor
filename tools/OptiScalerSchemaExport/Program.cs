using System.Text.Encodings.Web;
using System.Text.Json;
using OptiEditor.Core.Schema;

if (args.Length < 2)
{
    Console.Error.WriteLine("Usage: OptiScalerSchemaExport <upstream-commit> <root-ini-sha256> [output-path]");
    return 2;
}

var commit = args[0];
var iniSha256 = args[1].ToLowerInvariant();
var outputPath = args.Length > 2 ? args[2] : Path.Combine("schema", "optiscaler-v10-master.json");
var settings = new Opti10SchemaProvider().Settings;
if (settings.Count != 357)
    throw new InvalidOperationException($"Expected 357 root INI settings, found {settings.Count}.");

var artifact = new
{
    schemaId = "optiscaler.v10.master",
    schemaVersion = 1,
    version = "0.10",
    settingCount = settings.Count,
    upstream = new
    {
        repository = "optiscaler/OptiScaler",
        reference = "master",
        commit,
        rootIniPath = "OptiScaler.ini",
        rootIniSha256 = iniSha256
    },
    readerContracts = new object[]
    {
        new
        {
            id = "readString",
            accepts = "Any string value",
            auto = "Missing values default to auto; auto is case-insensitive and produces no override.",
            invalid = "Not applicable to raw strings.",
            behavior = "Preserves the original value unless the caller requests lowercase conversion."
        },
        new
        {
            id = "readWString",
            accepts = "Any value convertible from the INI string to a wide string",
            auto = "Missing values default to auto; auto is case-insensitive and produces no override.",
            invalid = "Not applicable to raw strings.",
            behavior = "Preserves the original value unless the caller requests lowercase conversion."
        },
        new
        {
            id = "readBool",
            accepts = "true or false, case-insensitive",
            auto = "Missing values and auto produce no override.",
            invalid = "Other strings produce no override.",
            behavior = "Reads through readString with lowercase conversion."
        },
        new
        {
            id = "readInt",
            accepts = "Base-10 signed Int32 or 0x-prefixed hexadecimal signed Int32; the entire value must be consumed.",
            auto = "Missing values and auto produce no override.",
            invalid = "Malformed input or values outside Int32 produce no override.",
            behavior = "Uses std::stoi; leading whitespace and a sign are accepted by that parser, but trailing unconsumed characters are rejected."
        },
        new
        {
            id = "readUInt",
            accepts = "The same signed Int32 lexical range as readInt, then converts the parsed result to UInt32.",
            auto = "Missing values and auto produce no override.",
            invalid = "Malformed input or values outside Int32 produce no override; for example 0xFFFFFFFF is rejected.",
            behavior = "Negative decimal values wrap during Int32-to-UInt32 conversion; for example -1 becomes UInt32.MaxValue."
        },
        new
        {
            id = "readFloat",
            accepts = "A value accepted in full by C++ float stream extraction (float32).",
            auto = "Missing values and auto produce no override.",
            invalid = "Malformed input, trailing characters, and values outside float32 produce no override.",
            behavior = "Per-setting code may clamp or reject the parsed float; see each setting's minimum, maximum, and contractNote."
        },
        new
        {
            id = "readEnum<T>",
            accepts = "readUInt input whose converted UInt32 value is less than the enum Count sentinel.",
            auto = "Missing values and auto produce no override.",
            invalid = "Parse failures and values at or above Count produce no override.",
            behavior = "Enum option values in each setting describe the exposed canonical choices."
        },
        new
        {
            id = "readUpscaler",
            accepts = "Known API-specific upscaler codes or a custom provider name.",
            auto = "Missing values and auto select the runtime default.",
            invalid = "Unknown non-auto strings are treated as custom provider names.",
            behavior = "Known codes use the source mapping and API compatibility remaps; suggestions are not an allowlist."
        },
        new
        {
            id = "ReadOverlaySettings",
            accepts = "Per-setting Boolean, index, float, color, or text formats defined by the overlay parser.",
            auto = "Missing values and auto produce no override.",
            invalid = "Invalid typed values produce no override; valid values may be clamped or mapped by the overlay parser.",
            behavior = "Colors parse through readUInt and mask to 24-bit RGB; index fields clamp to their last available index. Per-setting bounds and transformations are recorded on each setting."
        }
    },
    settings = settings.Select(setting => new
    {
        setting.Id,
        section = setting.IniKey.Section,
        key = setting.IniKey.Name,
        setting.DisplayName,
        setting.Description,
        group = setting.GroupId,
        setting.Order,
        valueType = setting.ValueKind.ToString().ToLowerInvariant(),
        setting.SupportsAuto,
        defaultValue = setting.DefaultRawValue,
        setting.Minimum,
        setting.Maximum,
        input = setting.InputKind.ToString().ToLowerInvariant(),
        optionsMode = setting.OptionsAreSuggestions ? "suggestions" : setting.ValueKind == SettingValueKind.Enum ? "closed" : "none",
        options = setting.Options.Select(option => new { option.Value, option.Label, option.Condition }),
        sourceReader = setting.SourceReader,
        setting.ContractNote,
        setting.SourceComment,
        sourceFiles = GetSourceFiles(setting.Id, setting.GroupId)
            .Select(path => $"https://github.com/optiscaler/OptiScaler/blob/{commit}/{path}")
    })
};

var fullPath = Path.GetFullPath(outputPath);
Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
var json = JsonSerializer.Serialize(artifact, new JsonSerializerOptions
{
    WriteIndented = true,
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
});
await File.WriteAllTextAsync(fullPath, json + Environment.NewLine, new System.Text.UTF8Encoding(false));
Console.WriteLine($"Wrote {settings.Count} settings from {commit} to {fullPath}");
return 0;

static IReadOnlyList<string> GetSourceFiles(string id, string group)
{
    var files = new List<string> { "OptiScaler/Config.cpp", "OptiScaler/Config.h" };
    if (id is "FrameGen.FGInput" or "FrameGen.FGOutput") files.Add("OptiScaler/State.h");
    if (id.StartsWith("Upscalers.", StringComparison.Ordinal)) files.Add("OptiScaler/OptiTypes.cpp");
    if (id is "Sharpness.Shader" or "Reprojection.FillMode") files.Add("OptiScaler/OptiTypes.h");
    if (group == "Menu") files.Add("OptiScaler/menu/menu_common.cpp");
    return files;
}
