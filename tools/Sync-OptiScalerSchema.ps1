param(
    [string] $Repository = "optiscaler/OptiScaler",
    [string] $V09Ref = "release/0.9",
    [string] $V10Ref = "master",
    [string] $OutputPath = "src/OptiEditor.Core/Schema/GeneratedOptiScalerSchemaCatalog.cs"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Get-UpstreamText([string] $Ref, [string] $Path) {
    $escapedRef = [uri]::EscapeDataString($Ref)
    return (Invoke-WebRequest -UseBasicParsing "https://raw.githubusercontent.com/$Repository/$escapedRef/$Path").Content
}

function Get-OptionalUpstreamText([string] $Ref, [string] $Path) {
    try { return Get-UpstreamText $Ref $Path }
    catch [System.Net.WebException] { return '' }
}

function Get-Commit([string] $Ref) {
    $result = git ls-remote "https://github.com/$Repository.git" "refs/heads/$Ref"
    $exitCode = $LASTEXITCODE
    $match = $result | Select-Object -First 1
    if ($exitCode -ne 0 -or [string]::IsNullOrWhiteSpace($match)) { throw "Unable to resolve upstream branch '$Ref'." }
    return ($match -split "\s+")[0]
}

function Escape-CSharp([string] $Value) { return $Value.Replace('\', '\\').Replace('"', '\"').Replace("`r`n", '\n').Replace("`n", '\n').Replace("`r", '\n') }
function Format-CSharpNumber($Value) {
    if ($null -eq $Value) { return 'null' }
    $text = "$Value".TrimEnd('.')
    $number = [decimal]::Parse($text, [Globalization.CultureInfo]::InvariantCulture)
    return $number.ToString('0.############################', [Globalization.CultureInfo]::InvariantCulture) + 'd'
}

function Parse-SourceMetadata([string] $Config, [string] $ConfigHeader, [string] $OptiTypesHeader, [string] $OptiTypesSource) {
    $reads = @{}
    $known = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    $readPattern = 'read(?<kind>Bool|Int|UInt|Float|String|WString|Enum<(?<enum>[^>]+)>)\(\s*"(?<section>[^"]+)"\s*,\s*"(?<key>[^"]+)"'
    foreach ($match in [regex]::Matches($Config, $readPattern)) {
        $kind = $match.Groups['kind'].Value
        $id = "$($match.Groups['section'].Value)`0$($match.Groups['key'].Value.Trim())"
        $known.Add($id) | Out-Null
        $reads[$id] = [pscustomobject]@{ Kind = $kind; EnumType = $match.Groups['enum'].Value; Offset = $match.Index; Property = $null; IsDiscreteChoice = $false; Options = @(); Minimum = $null; Maximum = $null }
    }

    foreach ($match in [regex]::Matches($Config, 'ini\.SetValue\(\s*"(?<section>[^"]+)"\s*,\s*"(?<key>[^"]+)"')) {
        $known.Add("$($match.Groups['section'].Value)`0$($match.Groups['key'].Value.Trim())") | Out-Null
    }

    $enumSource = "$ConfigHeader`n$OptiTypesHeader"
    function Get-EnumOptions([string] $EnumType) {
        $definition = [regex]::Match($enumSource, "enum(?: class)?\s+$([regex]::Escape($EnumType))(?:\s*:\s*[^\{]+)?\s*\{(?<body>.*?)\};", [Text.RegularExpressions.RegexOptions]::Singleline)
        if (-not $definition.Success) { throw "Could not resolve enum '$EnumType' from upstream source." }
        $value = 0; $options = [System.Collections.Generic.List[object]]::new()
        foreach ($part in $definition.Groups['body'].Value -split ',') {
            $token = ($part -replace '//.*', '').Trim()
            if (-not $token) { continue }
            $name = ($token -split '=')[0].Trim()
            if ($token -match '=\s*(?<value>\d+)') { $value = [int]$matches['value'] }
            if ($name -notin @('Count', '_') -and $name -notmatch 'COUNT$') { $options.Add([pscustomobject]@{ Value = "$value"; Label = $name }) }
            $value++
        }
        return $options.ToArray()
    }

    $converterOptions = @{}
    foreach ($converter in [regex]::Matches($OptiTypesSource, 'CodeTo(?<name>\w+)\([^\)]*\)\s*\{.*?mapping\s*=\s*\{(?<body>.*?)\};', [Text.RegularExpressions.RegexOptions]::Singleline)) {
        $options = [System.Collections.Generic.List[object]]::new()
        foreach ($entry in [regex]::Matches($converter.Groups['body'].Value, '\{\s*"(?<value>[^"]+)"\s*,\s*[^:}]+::(?<label>\w+)\s*\}')) {
            $options.Add([pscustomobject]@{ Value = $entry.Groups['value'].Value; Label = $entry.Groups['label'].Value })
        }
        if ($options.Count -gt 0) { $converterOptions[$converter.Groups['name'].Value] = $options.ToArray() }
    }

    foreach ($metadata in $reads.Values) {
        $nextRead = $Config.IndexOf('read', $metadata.Offset + 4, [StringComparison]::Ordinal)
        $length = if ($nextRead -lt 0) { $Config.Length - $metadata.Offset } else { $nextRead - $metadata.Offset }
        $remaining = $Config.Substring($metadata.Offset, $length)
        $property = [regex]::Match($remaining, '(?<property>\w+)\.set_from_config')
        if ($property.Success) { $metadata.Property = $property.Groups['property'].Value }
        if ($metadata.Kind -like 'Enum<*') {
            $metadata.Options = Get-EnumOptions $metadata.EnumType
            $metadata.Minimum = 0
            $metadata.Maximum = $metadata.Options.Count - 1
            continue
        }

        if ($metadata.Kind -in @('Float', 'Int', 'UInt')) {
            $castEnum = [regex]::Match($remaining, '(?:static_cast<(?<enum>\w+)>\(setting\.value\(\)\)|\((?<enum2>\w+)\)\s*std::clamp)')
            if ($castEnum.Success) {
                $enumName = if ($castEnum.Groups['enum'].Success) { $castEnum.Groups['enum'].Value } else { $castEnum.Groups['enum2'].Value }
                $metadata.Options = Get-EnumOptions $enumName
                $metadata.Minimum = 0
                $metadata.Maximum = $metadata.Options.Count - 1
                continue
            }
            $clamp = [regex]::Match($remaining, 'std::clamp\(setting\.value\(\),\s*(?<min>-?[\d.]+)f?\s*,\s*(?<max>-?[\d.]+)f?\)')
            if ($clamp.Success) { $metadata.Minimum = $clamp.Groups['min'].Value; $metadata.Maximum = $clamp.Groups['max'].Value; continue }
            $minimum = [regex]::Match($remaining, 'setting(?:\.value\(\))?\s*>=\s*(?<value>-?[\d.]+)')
            $maximum = [regex]::Match($remaining, 'setting(?:\.value\(\))?\s*<=\s*(?<value>-?[\d.]+)')
            if ($minimum.Success) { $metadata.Minimum = $minimum.Groups['value'].Value }
            if ($maximum.Success) { $metadata.Maximum = $maximum.Groups['value'].Value }
            continue
        }

        if ($metadata.Kind -eq 'String') {
            $converter = [regex]::Match($remaining, '\.transform\(CodeTo(?<name>\w+)\)')
            if ($converter.Success -and $converterOptions.ContainsKey($converter.Groups['name'].Value)) { $metadata.Options = $converterOptions[$converter.Groups['name'].Value] }
            if ($metadata.Options.Count -eq 0) {
                $choices = [System.Collections.Generic.List[object]]::new()
                foreach ($match in [regex]::Matches($remaining, 'lstrcmpiA\([^,]+,\s*"(?<value>[^"]+)"\)\s*==\s*0')) {
                    $value = $match.Groups['value'].Value
                    if (-not $choices.Exists([Predicate[object]] { param($item) [string]::Equals($item.Value, $value, [StringComparison]::OrdinalIgnoreCase) })) {
                        $choices.Add([pscustomobject]@{ Value = $value; Label = $value })
                    }
                }
                if ($choices.Count -ge 2) { $metadata.Options = $choices.ToArray() }
            }
        }

    }

    return [pscustomobject]@{ Reads = $reads; Known = $known }
}

function Get-IniStringChoices([string] $SourceComment) {
    $choices = [System.Collections.Generic.List[string]]::new()
    foreach ($line in $SourceComment -split "`n") {
        foreach ($token in $line -split ',') {
            $leading = [regex]::Match($token, '^\s*(?<value>[a-z][a-z0-9_]*)(?=\s*(?:\(|-|,|$))')
            if ($leading.Success -and -not ($choices -contains $leading.Groups['value'].Value)) { $choices.Add($leading.Groups['value'].Value) }
        }
    }
    if ($SourceComment -match '-\s*Default\s*\(auto\)\s*is\s*nofg\s*$') { return @($choices | Where-Object { -not [string]::Equals($_, 'nofg', [StringComparison]::OrdinalIgnoreCase) }) }
    return $choices.ToArray()
}

function Apply-MenuDiscreteChoices($Reads, [string] $MenuSource) {
    $choicesByProperty = @{}
    $arrayPattern = 'const\s+char\*\s+(?<name>\w+)\[\]\s*=\s*\{(?<values>.*?)\};'
    foreach ($array in [regex]::Matches($MenuSource, $arrayPattern, [Text.RegularExpressions.RegexOptions]::Singleline)) {
        $tail = $MenuSource.Substring($array.Index, [Math]::Min(1800, $MenuSource.Length - $array.Index))
        $property = [regex]::Match($tail, 'config->(?<property>\w+)')
        if (-not $property.Success) { continue }
        $options = [System.Collections.Generic.List[object]]::new()
        foreach ($value in [regex]::Matches($array.Groups['values'].Value, '"(?<value>-?\d+(?:\.\d+)?)"')) {
            $options.Add([pscustomobject]@{ Value = $value.Groups['value'].Value; Label = $value.Groups['value'].Value })
        }
        if ($options.Count -ge 2) { $choicesByProperty[$property.Groups['property'].Value] = $options.ToArray() }
    }

    foreach ($metadata in $Reads.Values) {
        if ($metadata.Kind -ne 'Float' -or [string]::IsNullOrWhiteSpace($metadata.Property)) { continue }
        if ($choicesByProperty.ContainsKey($metadata.Property)) {
            $metadata.Options = $choicesByProperty[$metadata.Property]
            $metadata.IsDiscreteChoice = $true
        }
    }
}

function Get-Entries([string] $Ref) {
    $ini = Get-UpstreamText $Ref "OptiScaler.ini"
    $config = Get-UpstreamText $Ref "OptiScaler/Config.cpp"
    $header = Get-UpstreamText $Ref "OptiScaler/Config.h"
    $typesHeader = Get-OptionalUpstreamText $Ref "OptiScaler/OptiTypes.h"
    $typesSource = Get-OptionalUpstreamText $Ref "OptiScaler/OptiTypes.cpp"
    $metadata = Parse-SourceMetadata $config $header $typesHeader $typesSource
    $menuSource = Get-UpstreamText $Ref "OptiScaler/menu/menu_common.cpp"
    Apply-MenuDiscreteChoices $metadata.Reads $menuSource

    $section = $null; $comments = [System.Collections.Generic.List[string]]::new(); $entries = [System.Collections.Generic.List[object]]::new(); $order = 0
    foreach ($line in $ini -split "`r?`n") {
        if ($line -match '^\[(.+)\]$') { $section = $matches[1]; $comments.Clear(); continue }
        if ($line -match '^\s*;\s?(?<comment>.*)$') { $comments.Add($matches['comment']) | Out-Null; continue }
        if ($section -and $line -match '^([^;#\s][^=]*)=(.*)$') {
            $key = $matches[1].Trim(); $default = $matches[2].Trim(); $id = "$section`0$key"
            $sourceComment = (($comments | Where-Object { $_ -and $_ -notmatch '^-{3,}\s*$' }) -join "`n").Trim()
            $comments.Clear()
            if (-not $metadata.Known.Contains($id)) { continue }
            $source = $metadata.Reads[$id]
            $kind = if ($null -eq $source) { 'String' } else { $source.Kind }
            [object[]] $options = @()
            if ($null -ne $source) { $options = @($source.Options) }
            if ($kind -eq 'String') {
                $documentedChoices = @(Get-IniStringChoices $sourceComment)
                if ($documentedChoices.Count -ge 2) {
                    if ($options.Count -gt 0) {
                        $options = @($documentedChoices | ForEach-Object {
                            $documented = $_
                            $match = $options | Where-Object { [string]::Equals($_.Value, $documented, [StringComparison]::OrdinalIgnoreCase) } | Select-Object -First 1
                            if ($null -ne $match) { [pscustomobject]@{ Value = $documented; Label = $match.Label } }
                        } | Where-Object { $null -ne $_ })
                    }
                    else {
                        $options = @($documentedChoices | ForEach-Object { [pscustomobject]@{ Value = $_; Label = $_ } })
                    }
                }
            }
            $type = switch -Regex ($kind) {
                '^Bool$' { 'Boolean'; break }
                '^(Int|UInt)$' { if ($options.Count -gt 0) { 'Enum' } else { 'Integer' }; break }
                '^Float$' { if ($source.IsDiscreteChoice) { 'Enum' } else { 'Double' }; break }
                '^Enum<' { 'Enum'; break }
                '^String$' { if ($options.Count -gt 0) { 'Enum' } else { 'String' }; break }
                default { 'String'; break }
            }
            $entries.Add([pscustomobject]@{ Section = $section; Key = $key; Default = $default; SourceComment = $sourceComment; Type = $type; Order = $order++; Minimum = if ($null -eq $source) { $null } else { $source.Minimum }; Maximum = if ($null -eq $source) { $null } else { $source.Maximum }; Options = $options })
        }
    }
    return $entries
}

function Render-Entries($Entries) {
    return (($Entries | ForEach-Object {
        $options = if ($_.Options.Count -eq 0) { '[]' } else { '[' + (($_.Options | ForEach-Object { "new(`"$(Escape-CSharp $_.Value)`", `"$(Escape-CSharp $_.Label)`")" }) -join ', ') + ']' }
        $minimum = Format-CSharpNumber $_.Minimum
        $maximum = Format-CSharpNumber $_.Maximum
        "        new(`"$(Escape-CSharp $_.Section).$(Escape-CSharp $_.Key)`", new IniKey(`"$(Escape-CSharp $_.Section)`", `"$(Escape-CSharp $_.Key)`"), `"$(Escape-CSharp $_.Key)`", string.Empty, `"$(Escape-CSharp $_.Section)`", $($_.Order), SettingValueKind.$($_.Type), true, $options, `"$(Escape-CSharp $_.Default)`", $minimum, $maximum, SourceComment: `"$(Escape-CSharp $_.SourceComment)`"),"
    }) -join "`n")
}

$v09Commit = Get-Commit $V09Ref
$v10Commit = Get-Commit $V10Ref
# Fetch every upstream file for a pinned commit SHA, not the floating branch
# name. Get-Entries makes several independent raw.githubusercontent.com
# requests (OptiScaler.ini, Config.cpp/h, OptiTypes.h/cpp, menu_common.cpp);
# resolving each against the live branch tip let a commit landing upstream
# mid-run mix pre- and post-change content into one non-reproducible
# generation, which is what made the daily/manual schema checks flap.
$v09 = Get-Entries $v09Commit
$v10 = Get-Entries $v10Commit

$content = @"
// <auto-generated />
// Source: $Repository ($V09Ref @ $v09Commit; $V10Ref @ $v10Commit)
// Generated from the upstream root OptiScaler.ini and its Config.cpp reader/SaveIni logic.
using OptiEditor.Core.Ini;
using OptiEditor.Core.Models;

namespace OptiEditor.Core.Schema;

internal static class GeneratedOptiScalerSchemaCatalog
{
    public static IReadOnlyList<SettingDefinition> Create(OptiSchemaFamily family) => family switch
    {
        OptiSchemaFamily.V09 => V09,
        OptiSchemaFamily.V10 => V10,
        _ => throw new UnsupportedOptiSchemaException(family)
    };

    private static readonly IReadOnlyList<SettingDefinition> V09 =
    [
$(Render-Entries $v09)
    ];

    private static readonly IReadOnlyList<SettingDefinition> V10 =
    [
$(Render-Entries $v10)
    ];
}
"@

$fullOutput = [IO.Path]::GetFullPath((Join-Path (Get-Location) $OutputPath))
[IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($fullOutput)) | Out-Null
[IO.File]::WriteAllText($fullOutput, $content.Replace("`r`n", "`n"), [Text.UTF8Encoding]::new($false))
Write-Host "Generated $fullOutput ($($v09.Count) 0.9 keys, $($v10.Count) 0.10 keys)."
