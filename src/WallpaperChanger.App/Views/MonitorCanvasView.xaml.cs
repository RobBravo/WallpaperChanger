using System.Collections;
using System.Windows;
using System.Windows.Controls;
using WallpaperChanger.App.ViewModels;
using WpfPanel = System.Windows.Controls.Panel;
using WpfSize = System.Windows.Size;

namespace WallpaperChanger.App.Views;

public partial class MonitorCanvasView : System.Windows.Controls.UserControl
{
    public static readonly DependencyProperty ItemsSourceProperty = DependencyProperty.Register(
        nameof(ItemsSource),
        typeof(IEnumerable),
        typeof(MonitorCanvasView));

    public static readonly DependencyProperty SelectedMonitorProperty = DependencyProperty.Register(
        nameof(SelectedMonitor),
        typeof(VirtualMonitorViewModel),
        typeof(MonitorCanvasView),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    public MonitorCanvasView()
    {
        InitializeComponent();
    }

    public IEnumerable? ItemsSource
    {
        get => (IEnumerable?)GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    public VirtualMonitorViewModel? SelectedMonitor
    {
        get => (VirtualMonitorViewModel?)GetValue(SelectedMonitorProperty);
        set => SetValue(SelectedMonitorProperty, value);
    }

    private void PreviewImageFailed(object sender, ExceptionRoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.Image image || image.Parent is not Grid grid)
        {
            return;
        }

        image.Visibility = Visibility.Collapsed;
        var fallback = grid.Children.OfType<TextBlock>().FirstOrDefault(child => child.Name == "PreviewFallback");
        if (fallback is not null)
        {
            fallback.Visibility = Visibility.Visible;
        }

        e.Handled = true;
    }
}

public sealed class MonitorLayoutPanel : WpfPanel
{
    protected override WpfSize MeasureOverride(WpfSize availableSize)
    {
        foreach (UIElement child in InternalChildren)
        {
            child.Measure(availableSize);
        }

        return new WpfSize();
    }

    protected override WpfSize ArrangeOverride(WpfSize finalSize)
    {
        foreach (UIElement child in InternalChildren)
        {
            if (child is FrameworkElement { DataContext: VirtualMonitorViewModel monitor })
            {
                child.Arrange(MonitorCanvasLayout.CalculateBounds(
                    finalSize,
                    monitor.LayoutAspectRatio,
                    monitor.NormalizedLeft,
                    monitor.NormalizedTop,
                    monitor.NormalizedWidth,
                    monitor.NormalizedHeight));
            }
        }

        return finalSize;
    }
}

public static class MonitorCanvasLayout
{
    public static Rect CalculateBounds(
        WpfSize availableSize,
        double layoutAspectRatio,
        double normalizedLeft,
        double normalizedTop,
        double normalizedWidth,
        double normalizedHeight)
    {
        if (availableSize.Width <= 0 || availableSize.Height <= 0)
        {
            return Rect.Empty;
        }

        var aspectRatio = layoutAspectRatio > 0 ? layoutAspectRatio : 1d;
        var layoutWidth = Math.Min(availableSize.Width, availableSize.Height * aspectRatio);
        var layoutHeight = layoutWidth / aspectRatio;
        var left = (availableSize.Width - layoutWidth) / 2 + normalizedLeft * layoutWidth;
        var top = (availableSize.Height - layoutHeight) / 2 + normalizedTop * layoutHeight;

        return new Rect(left, top, normalizedWidth * layoutWidth, normalizedHeight * layoutHeight);
    }
}
