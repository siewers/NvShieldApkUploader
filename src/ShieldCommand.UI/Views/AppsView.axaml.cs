using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using FluentAvalonia.UI.Controls;
using ShieldCommand.Core.Models;
using ShieldCommand.UI.Helpers;
using ShieldCommand.UI.ViewModels;

namespace ShieldCommand.UI.Views;

public sealed partial class AppsView : UserControl
{
    public AppsView()
    {
        InitializeComponent();

        var installButton = this.FindControl<Button>("InstallApkButton")!;
        installButton.Click += OnInstallApkClick;

        PackageList.AddHandler(PointerReleasedEvent, OnPackageListPointerReleased, RoutingStrategies.Tunnel);
        PackageList.DoubleTapped += OnPackageListDoubleTapped;
    }

    private void OnPackageListDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (PackageList.SelectedItem is InstalledPackage package && DataContext is AppsViewModel vm)
        {
            _ = ShowInfoAndUninstallAsync(package, vm);
        }
    }

    private void OnPackageListPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (e.InitialPressMouseButton != MouseButton.Right)
        {
            return;
        }

        if (PackageList.SelectedItem is not InstalledPackage package
            || DataContext is not AppsViewModel vm)
        {
            return;
        }

        e.Handled = true;

        var flyout = new MenuFlyout
        {
            OverlayDismissEventPassThrough = true,
            Items =
            {
                MenuHelper.CreateItem("Info", "\uf05a", () => _ = ShowInfoAndUninstallAsync(package, vm)),
                MenuHelper.CreateGoogleSearchItem(package.PackageName),
                new Separator(),
                MenuHelper.CreateItem("Uninstall", "\uf2ed", () => _ = ShowUninstallAsync(package, vm)),
            }
        };

        flyout.ShowAt(PackageList, true);
    }

    private static async Task ShowInfoAndUninstallAsync(InstalledPackage package, AppsViewModel vm)
    {
        var detailed = await vm.AdbService.GetPackageInfoAsync(package.PackageName, includeSize: true);
        if (await PackageInfoDialog.ShowAsync(detailed, "Uninstall"))
        {
            await vm.UninstallCommand.ExecuteAsync(package);
        }
    }

    private static async Task ShowUninstallAsync(InstalledPackage package, AppsViewModel vm)
    {
        var dialog = new ContentDialog
        {
            Title = "Uninstall App",
            Content = $"Are you sure you want to uninstall {package.PackageName}?",
            PrimaryButtonText = "Uninstall",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close,
        };

        if (await dialog.ShowAsync() == ContentDialogResult.Primary)
        {
            await vm.UninstallCommand.ExecuteAsync(package);
        }
    }

    private async void OnInstallApkClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not AppsViewModel appsVm)
        {
            return;
        }

        var mainWindow = TopLevel.GetTopLevel(this) as Window;
        if (mainWindow?.DataContext is not MainWindowViewModel mainVm)
        {
            return;
        }

        var installView = new InstallView
        {
            DataContext = mainVm.InstallPage,
            MinWidth = 500,
        };

        var dialog = new ContentDialog
        {
            Title = "Install APK",
            Content = installView,
            CloseButtonText = "Close",
            DefaultButton = ContentDialogButton.Close,
        };

        await dialog.ShowAsync();

        if (mainVm.InstallPage.DidInstall)
        {
            mainVm.InstallPage.ResetDidInstall();
            appsVm.RefreshCommand.Execute(null);
        }
    }
}
