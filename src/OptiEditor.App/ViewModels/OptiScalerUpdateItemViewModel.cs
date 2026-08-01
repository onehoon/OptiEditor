using CommunityToolkit.Mvvm.ComponentModel;
using OptiEditor.Core.Models;

namespace OptiEditor.App.ViewModels;

public partial class OptiScalerUpdateItemViewModel(OptiInstallation installation) : ObservableObject
{
    public OptiInstallation Installation { get; private set; } = installation;
    public string DirectoryIdentity => Installation.InstallDirectory;
    [ObservableProperty] public partial bool IsSelected { get; set; }
    [ObservableProperty] public partial bool IsSelectionEnabled { get; set; } = true;
    public void Update(OptiInstallation installation) { Installation = installation; OnPropertyChanged(nameof(Installation)); }
}
