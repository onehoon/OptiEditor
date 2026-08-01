using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using OptiEditor.App.Services;
using OptiEditor.Core.Models;
using OptiEditor.Core.Presets;

namespace OptiEditor.App.ViewModels;
public partial class PresetsViewModel : ObservableObject
{
 public ObservableCollection<PresetDefinition> Presets { get; } = [];
 public IEnumerable<PresetDefinition> FilteredPresets => Presets.Where(MatchesSearch);
 [ObservableProperty] public partial string SearchText { get; set; } = "";
 [ObservableProperty] public partial string StatusText { get; set; } = "Loading presets...";
 public async Task LoadAsync() { try { var user = await AppServices.Presets.LoadAsync(); Presets.Clear(); foreach (var p in AppServices.BuiltInPresets.GetAll().Concat(user).OrderBy(x => x.Source).ThenBy(x => x.Name)) Presets.Add(p); StatusText = Presets.Count == 0 ? "No user presets have been created." : $"{Presets.Count} presets available."; OnPropertyChanged(nameof(FilteredPresets)); } catch (Exception ex) { AppServices.Logger.Error("Preset list could not be loaded.", ex); StatusText = "Presets could not be loaded. See logs for details."; } }
 public async Task DuplicateAsync(PresetDefinition source) { var users = (await AppServices.Presets.LoadAsync()).ToList(); var name = source.Name + " Copy"; var n = 2; while (users.Any(x => x.Family == source.Family && string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase))) name = source.Name + " Copy " + n++; var now = DateTimeOffset.UtcNow; users.Add(source with { Id = Guid.NewGuid(), Name = name, Source = PresetSource.User, CreatedAt = now, UpdatedAt = now }); await AppServices.Presets.SaveAsync(users); await LoadAsync(); }
 public async Task DeleteAsync(PresetDefinition preset) { if (preset.Source != PresetSource.User) return; var users = (await AppServices.Presets.LoadAsync()).Where(x => x.Id != preset.Id).ToList(); await AppServices.Presets.SaveAsync(users); await LoadAsync(); }
 public async Task<string?> SaveAsync(PresetDefinition preset)
 {
  try { var users = (await AppServices.Presets.LoadAsync()).ToList(); var error = new PresetValidationService(AppServices.Schemas).Validate(preset, users); if (error is not null) return error;
  var existing = users.FindIndex(x => x.Id == preset.Id); if (existing >= 0) users[existing] = preset; else users.Add(preset);
  await AppServices.Presets.SaveAsync(users); await LoadAsync(); return null; }
  catch (Exception ex) { AppServices.Logger.Error("Preset could not be saved.", ex); return "Preset could not be saved safely. See logs for details."; }
 }
 partial void OnSearchTextChanged(string value) => OnPropertyChanged(nameof(FilteredPresets));
 private bool MatchesSearch(PresetDefinition preset) => string.IsNullOrWhiteSpace(SearchText) || preset.Name.Contains(SearchText.Trim(), StringComparison.OrdinalIgnoreCase) || preset.Description?.Contains(SearchText.Trim(), StringComparison.OrdinalIgnoreCase) == true || preset.Family.ToString().Contains(SearchText.Trim(), StringComparison.OrdinalIgnoreCase);
}
