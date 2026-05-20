using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using SYNODownloadStationLinker.ViewModels;

namespace SYNODownloadStationLinker.Views;

public partial class MainWindow : Window
{
    private MainWindowViewModel? subscribedViewModel;

    public MainWindow()
    {
        InitializeComponent();
    }

    private void Control_OnLoaded(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        if (subscribedViewModel is not null)
        {
            subscribedViewModel.NotificationRequested -= ViewModelOnNotificationRequested;
        }

        subscribedViewModel = viewModel;
        subscribedViewModel.NotificationRequested += ViewModelOnNotificationRequested;
        viewModel.OnLoaded();
    }

    private async void TopLevel_OnClosed(object? sender, EventArgs e)
    {
        if (DataContext is MainWindowViewModel viewModel)
        {
            viewModel.NotificationRequested -= ViewModelOnNotificationRequested;
            await viewModel.OnClosed();
        }
    }

    private void MinBtn_OnClick(object? sender, RoutedEventArgs e)
    {
        ShowInTaskbar = false;
        WindowState = WindowState.Minimized;
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
            BeginMoveDrag(e);
        }
    }

    private void ViewModelOnNotificationRequested(NotificationMessage notification)
    {
        Dispatcher.UIThread.Post(() => ShowDesktopNotification(notification));
    }

    private void ShowDesktopNotification(NotificationMessage notification)
    {
        var toast = new Window
        {
            Width = 360,
            Height = 112,
            CanResize = false,
            ShowInTaskbar = false,
            SystemDecorations = SystemDecorations.None,
            Topmost = true,
            Background = Brushes.Transparent,
            Content = CreateNotificationContent(notification)
        };

        toast.Opened += (_, _) =>
        {
            var screen = Screens.ScreenFromWindow(this) ?? Screens.Primary;
            if (screen is null)
            {
                return;
            }

            var margin = 18;
            toast.Position = new PixelPoint(
                screen.WorkingArea.Right - (int)toast.Width - margin,
                screen.WorkingArea.Bottom - (int)toast.Height - margin);
        };

        var closeTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(4)
        };
        closeTimer.Tick += (_, _) =>
        {
            closeTimer.Stop();
            toast.Close();
        };

        toast.Show();
        closeTimer.Start();
    }

    private static Control CreateNotificationContent(NotificationMessage notification)
    {
        var accent = notification.Severity switch
        {
            NotificationSeverity.Success => "#107C10",
            NotificationSeverity.Warning => "#9D5D00",
            NotificationSeverity.Error => "#C42B1C",
            _ => "#0067C0"
        };

        var contentStack = new StackPanel
        {
            Spacing = 4,
            Children =
            {
                new TextBlock
                {
                    Text = notification.Title,
                    FontSize = 14,
                    FontWeight = FontWeight.SemiBold,
                    Foreground = new SolidColorBrush(Color.Parse("#202020"))
                },
                new TextBlock
                {
                    Text = notification.Message,
                    FontSize = 12,
                    TextWrapping = TextWrapping.Wrap,
                    Foreground = new SolidColorBrush(Color.Parse("#5F5F5F"))
                }
            }
        };
        Grid.SetColumn(contentStack, 1);

        return new Border
        {
            Width = 360,
            Height = 112,
            Padding = new Thickness(14),
            BorderThickness = new Thickness(1),
            BorderBrush = new SolidColorBrush(Color.Parse("#E1E1E1")),
            Background = new SolidColorBrush(Color.Parse("#FFFFFF")),
            CornerRadius = new CornerRadius(8),
            Child = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("4,*"),
                ColumnSpacing = 12,
                Children =
                {
                    new Border
                    {
                        Background = new SolidColorBrush(Color.Parse(accent)),
                        CornerRadius = new CornerRadius(2)
                    },
                    contentStack
                }
            }
        };
    }
}
