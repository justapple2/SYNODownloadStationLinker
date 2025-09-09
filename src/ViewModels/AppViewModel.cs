using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace SYNODownloadStationLinker.ViewModels;

public partial class AppViewModel : ObservableObject
{
    [RelayCommand]
    private void Exit()
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.Shutdown();
        }
    }

    [RelayCommand]
    private void Show()
    {
        var app = Application.Current as App;
        if (app?.CurrentWindow is null) return;

        app.CurrentWindow.ShowInTaskbar = true;
        app.CurrentWindow.WindowState = WindowState.Normal;
        // 激活窗口并置于前台
        app.CurrentWindow.Activate();
    }
}