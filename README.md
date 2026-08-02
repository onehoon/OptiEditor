# OptiEditor

> [!WARNING]
> **OptiEditor is currently under active development. Do not use it with your games or important OptiScaler installations yet.**

OptiEditor is a Windows desktop utility for discovering, configuring, presetting, and locally updating existing [OptiScaler](https://github.com/optiscaler/OptiScaler) installations.

> [!IMPORTANT]
> **OptiEditor is an independent community project. It is not an official OptiScaler application and is not developed, distributed, or supported by the OptiScaler project.**

The configuration editor currently supports **OptiScaler 0.9.x and 0.10.x**. The installed version family is detected from the OptiScaler binary `FileVersion`; users do not select the version manually.

## Features

### Installation discovery

- Add, remove, enable, and disable custom scan folders.
- Recursively search enabled folders for `OptiScaler.ini`.
- Automatically scan enabled folders when OptiEditor starts.
- Validate the OptiScaler proxy binary using Windows PE version metadata instead of trusting the filename alone.
- Detect supported proxy filenames in the same directory as `OptiScaler.ini`:
  - `dxgi.dll`
  - `winmm.dll`
  - `version.dll`
  - `dbghelp.dll`
  - `d3d12.dll`
  - `wininet.dll`
  - `winhttp.dll`
  - `OptiScaler.asi`
- Display the detected game name, executable, OptiScaler version, proxy filename, and installation folder.
- Search discovered installations, open their folders, copy INI paths, and rescan manually.

### Version-aware configuration editor

- Separate setting schemas for OptiScaler 0.9.x and 0.10.x.
- Grouped controls for upscaling, frame generation, FSR, DLSS, overlay, image quality, shortcuts, output scaling, resolution, display, texture, and other supported settings.
- Dedicated controls for Auto/Enabled/Disabled values, enumerated options, numeric values, and shortcut keys.
- Shortcut key capture with editable raw values such as `auto`, `-1`, decimal virtual-key codes, and `0xNN`.
- Edit only settings that physically exist in the selected `OptiScaler.ini`.
- Preserve unsupported or future values until the user explicitly changes them.
- Preserve comments, unknown lines, ordering, whitespace, encoding, BOM, and line endings.
- Detect external file changes before saving and refuse to overwrite a modified INI.
- Create `OptiScaler.ini.optieditor.bak` before a successful INI replacement.
- Revert individual settings, revert all changes, reload from disk, or reset supported Auto-capable settings to `auto`.
- Configure which editor settings are visible separately for OptiScaler 0.9 and 0.10.

### Presets

- Built-in frame-pacing presets for both supported OptiScaler families.
- Create separate user presets for OptiScaler 0.9 and 0.10.
- Search, edit, duplicate, and delete user presets.
- Select a compatible discovered game and preview changes before applying a preset.
- Choose individual preset entries to apply.
- Apply preset values to the editor first; the INI is not written until **Save** is selected.
- Skip settings that are not present in the target INI.

### OptiScaler Update

- Select a local OptiScaler binary as the replacement source.
- Accept any source filename when its PE metadata identifies it as OptiScaler and contains readable version information.
- Display the source path, file version, product version, and file size.
- Select targets individually or by installed family: **OptiScaler 0.9**, **OptiScaler 0.10**, or **All**.
- Search installations without clearing existing selections.
- Rescan selected installation directories immediately before replacement.
- Stage and verify the source with SHA-256 before modifying targets.
- Preserve each target's existing proxy filename. For example, a selected source named `source.bin` can replace an installed `dxgi.dll` while the final filename remains `dxgi.dll`.
- Process each installation independently so one failure does not stop the remaining targets.
- Skip locked files and report access, validation, and replacement failures per installation.
- Rescan affected installations after replacement and refresh the shared installation catalog.

> [!WARNING]
> **OptiScaler Update permanently overwrites the selected installed proxy binaries and does not create backups.** Close the game before replacement and keep your own copy of any binary you may want to restore.

OptiScaler Update does not compare source and installed versions. It permits upgrades, downgrades, equal-version replacement, and replacement between the 0.9 and 0.10 families. It replaces only the detected primary OptiScaler proxy binary and does not modify `OptiScaler.ini` or auxiliary libraries.

### OptiEditor updates

- Check for OptiEditor updates at application startup through Velopack.
- Automatically download, apply, and restart when an update is available.
- Continue opening the currently installed version if the update check fails.

## Requirements

- Windows 10 version 1809, build 17763 or later, or Windows 11
- x64 Windows
- One or more existing OptiScaler installations to scan and edit

## Installation

1. Open the [OptiEditor Releases](https://github.com/onehoon/OptiEditor/releases) page.
2. Download and run the latest Velopack Setup executable.
3. Start OptiEditor and add the folders that contain your game installations.

> [!NOTE]
> OptiEditor requires **Windows App Runtime 2.3.1 x64**, which is already installed on many Windows systems. If OptiEditor does not start, install it from the [Microsoft Windows App SDK download page](https://learn.microsoft.com/en-us/windows/apps/windows-app-sdk/downloads) and try again.

The Setup package handles the required .NET Desktop Runtime. Windows App Runtime is not installed automatically by OptiEditor.

## Basic usage

1. Open **Folders** and select **Add Folder**.
2. Enable the folders to scan and select **Save Changes**.
3. Select **Scan Now**, or restart OptiEditor to run the automatic startup scan.
4. Open **Games** and select **Edit Settings** for a detected installation.
5. Review changes and select **Save** to update `OptiScaler.ini`.
6. Use **Presets** to create reusable partial configurations or apply a built-in preset.
7. Use **OptiScaler Update** only when you intentionally want to replace installed OptiScaler proxy binaries.

## Discovery rules

An installation is listed only when:

- `OptiScaler.ini` exists in the scanned directory.
- A supported proxy filename exists in that same directory.
- The proxy file's PE metadata identifies it as OptiScaler.
- Its numeric `FileVersion` maps to a supported editor family: 0.9.x or 0.10.x.

OptiEditor does not infer the version from the INI contents, directory name, or proxy filename. If multiple verified proxy binaries report conflicting versions, the installation is excluded until the conflict is resolved.

## Local data

OptiEditor stores its own configuration under:

```text
%LocalAppData%\OptiEditor\
```

This includes scan folders, editor visibility settings, user presets, logs, and temporary update staging data. The current installation list is rebuilt from disk scans rather than treated as a permanent installation database.

## Development

OptiEditor is built with:

- C# and .NET 10
- WinUI 3 / Windows App SDK
- CommunityToolkit.Mvvm
- Velopack

Build the solution for x64:

```powershell
dotnet restore OptiEditor.sln
dotnet build OptiEditor.sln --configuration Release -p:Platform=x64
```

Run the Core test suite:

```powershell
dotnet test tests/OptiEditor.Core.Tests/OptiEditor.Core.Tests.csproj --configuration Release
```

## Important notes

- OptiEditor does not download or bundle OptiScaler binaries.
- OptiEditor does not migrate an INI between OptiScaler version families.
- Installing an unsupported future OptiScaler version may make that installation unavailable in the current editor until support is added.
- Always review changes before saving or replacing files.
