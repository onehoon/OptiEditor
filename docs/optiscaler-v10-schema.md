# OptiScaler V0.10 schema contract

`schema/optiscaler-v10-master.json` is the versioned, machine-readable V0.10
contract for the OptiEditor Editor and Presets flows. Its `upstream` block pins
the OptiScaler master commit and the SHA-256 of that commit's root
`OptiScaler.ini`. Each setting records its reader, accepted type and bounds,
closed choices or open suggestions, parser notes, INI comment, and source links.

The root INI defines which keys may appear. Upstream `Config.cpp`, `Config.h`,
`State.h`, `OptiTypes` mappings, and the overlay parser define how those keys
are read and which values are accepted. Comment examples for open fields remain
suggestions; custom upscaler/provider names and custom FPS overlay row lists
stay editable.

Refresh the app catalog and exported contract from one pinned upstream commit:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\tools\Sync-OptiScalerSchema.ps1 -V10Commit <commit-sha> -V10Only
dotnet run --project .\tools\OptiScalerSchemaExport -- <commit-sha> <root-ini-sha256> .\schema\optiscaler-v10-master.json
```

The `-V10Only` refresh replaces only the generated V0.10 block and checks that
the V0.9 catalog block is byte-for-byte unchanged. The JSON exporter reads the
resolved V0.10 definitions used by the app, so Dashboard consumers receive the
same options, custom-value handling, bounds, and conditions.
The artifact also defines shared `readerContracts` for `auto`, typed parsing,
integer conversion, and overlay transformations so consumers can distinguish
raw input syntax from the effective values OptiScaler applies.
