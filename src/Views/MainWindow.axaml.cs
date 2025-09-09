using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Interactivity;
using SYNODownloadStationLinker.ViewModels;

namespace SYNODownloadStationLinker.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private void Control_OnLoaded(object? sender, RoutedEventArgs e)
    {
        if (this.DataContext is MainWindowViewModel viewModel)
        {
             viewModel.OnLoaded();
        }
    }

    private async void TopLevel_OnClosed(object? sender, EventArgs e)
    {
        if (this.DataContext is MainWindowViewModel viewModel)
        {
           await viewModel.OnClosed();
        }
    }

    private void MinBtn_OnClick(object? sender, RoutedEventArgs e)
    {
        this.ShowInTaskbar = false;
        this.WindowState = WindowState.Minimized;
    }

    private void CloseBtn_OnClick(object? sender, RoutedEventArgs e)
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.Shutdown();
        }
    }

    private void InputElement_OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            // 开始窗口拖拽
            BeginMoveDrag(e);
        }
    }
}