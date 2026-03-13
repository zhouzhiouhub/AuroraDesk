using System.Windows;
using System.Windows.Input;
using AuroraDesk.App.ViewModels;

namespace AuroraDesk.App;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;

    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = _viewModel;

        Microsoft.Win32.SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
    }

    private void OnDisplaySettingsChanged(object? sender, EventArgs e)
    {
        Dispatcher.BeginInvoke(() => _viewModel.OnDisplaySettingsChanged());
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is DependencyObject source && FindAncestor<System.Windows.Controls.Primitives.ButtonBase>(source) is not null)
            return;

        if (e.ClickCount == 2)
        {
            ToggleMaximize();
            return;
        }

        if (e.LeftButton != MouseButtonState.Pressed)
            return;

        if (WindowState == WindowState.Maximized)
        {
            var mouseOnWindow = e.GetPosition(this);
            var horizontalPercent = mouseOnWindow.X / Math.Max(ActualWidth, 1);
            var screenPos = PointToScreen(mouseOnWindow);
            var restoreBounds = RestoreBounds;

            WindowState = WindowState.Normal;
            Left = screenPos.X - (restoreBounds.Width * horizontalPercent);
            Top = Math.Max(0, screenPos.Y - 14);
        }

        DragMove();
    }

    private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        => WindowState = WindowState.Minimized;

    private void MaximizeButton_Click(object sender, RoutedEventArgs e)
        => ToggleMaximize();

    private void CloseButton_Click(object sender, RoutedEventArgs e)
        => Close();

    private void ToggleMaximize()
        => WindowState = WindowState == WindowState.Maximized
            ? WindowState.Normal
            : WindowState.Maximized;

    private void Monitor_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.DataContext is MonitorDisplayItem item)
            _viewModel.SelectMonitor(item);
    }

    private void LibraryItem_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.DataContext is WallpaperThumbnailItem item)
            _viewModel.SelectLibraryItem(item);
    }

    protected override void OnClosed(EventArgs e)
    {
        Microsoft.Win32.SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
        base.OnClosed(e);
    }

    private static T? FindAncestor<T>(DependencyObject? child) where T : DependencyObject
    {
        while (child is not null)
        {
            if (child is T target)
                return target;

            child = System.Windows.Media.VisualTreeHelper.GetParent(child);
        }

        return null;
    }
}
