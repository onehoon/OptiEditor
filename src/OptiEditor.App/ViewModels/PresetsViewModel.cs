using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using OptiEditor.App.Services;
using OptiEditor.Core.Models;
using OptiEditor.Core.Presets;

namespace OptiEditor.App.ViewModels;
public partial class PresetsViewModel : ObservableObject
{
 public ObservableCollection<PresetDefinition> Presets { get; } = [];
 [ObservableProperty] public partial string SearchText { get; set; } = "";
 [ObservableProperty] public partial string StatusText { get; set; } = "Loading presets...";
 public async Task LoadAsync() { var user = await AppServices.Presets.LoadAsync(); Presets.Clear(); foreach (var p in AppServices.BuiltInPresets.GetAll().Concat(user).OrderBy(x => x.Source).ThenBy(x => x.Name)) Presets.Add(p); StatusText = Presets.Count == 0 ? "No user presets have been created." : $"{Presets.Count} presets available."; }
 public async Task DuplicateAsync(PresetDefinition source) { var users = (await AppServices.Presets.LoadAsync()).ToList(); var name = source.Name + " Copy"; var n = 2; while (users.Any(x => x.Family == source.Family && string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase))) name = source.Name + " Copy " + n++; var now = DateTimeOffset.UtcNow; users.Add(source with { Id = Guid.NewGuid(), Name = name, Source = PresetSource.User, CreatedAt = now, UpdatedAt = now }); await AppServices.Presets.SaveAsync(users); await LoadAsync(); }
 public async Task DeleteAsync(PresetDefinition preset) { if (preset.Source != PresetSource.User) return; var users = (await AppServices.Presets.LoadAsync()).Where(x => x.Id != preset.Id).ToList(); await AppServices.Presets.SaveAsync(users); await LoadAsync(); }
 public async Task CreateAsync() { var users = (await AppServices.Presets.LoadAsync()).ToList(); var now = DateTimeOffset.UtcNow; users.Add(new(Guid.NewGuid(), "New Preset", "Edit this preset from a future editor dialog.", OptiSchemaFamily.V10, PresetSource.User, [new("framegen.enabled", "auto")], now, now)); await AppServices.Presets.SaveAsync(users); await LoadAsync(); }
}
