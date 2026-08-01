using OptiEditor.Core.Models;
using OptiEditor.Core.Presets;

namespace OptiEditor.App.ViewModels;

public sealed record EditorNavigationRequest(OptiInstallation Installation, PresetDefinition? Preset = null);
