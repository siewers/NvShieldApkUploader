using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ShieldCommand.Core.Models;
using ShieldCommand.Core.Services;

namespace ShieldCommand.UI.ViewModels;

public sealed partial class AppsViewModel : ViewModelBase
{
    public AdbService AdbService { get; }

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string _statusText = string.Empty;

    public ObservableCollection<InstalledPackage> Packages { get; } = [];

    public AppsViewModel(AdbService adbService)
    {
        AdbService = adbService;
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        IsBusy = true;
        StatusText = "Loading packages...";

        var packages = await AdbService.GetInstalledPackagesAsync();
        Packages.Clear();
        foreach (var pkg in packages)
        {
            Packages.Add(pkg);
        }

        StatusText = $"{Packages.Count} packages found";
        IsBusy = false;
    }

    [RelayCommand]
    private async Task UninstallAsync(InstalledPackage package)
    {
        IsBusy = true;
        StatusText = $"Uninstalling {package.PackageName}...";

        var result = await AdbService.UninstallPackageAsync(package.PackageName);

        if (result.Success)
        {
            Packages.Remove(package);
            StatusText = $"Uninstalled {package.PackageName}";
        }
        else
        {
            StatusText = $"Failed to uninstall: {(string.IsNullOrEmpty(result.Error) ? result.Output : result.Error)}";
        }

        IsBusy = false;
    }
}
