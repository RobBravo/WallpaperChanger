using System.Collections;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using WallpaperChanger.App.ViewModels;

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

    public static readonly DependencyProperty LayoutAspectRatioProperty = DependencyProperty.Register(
        nameof(LayoutAspectRatio),
        typeof(double),
        typeof(MonitorCanvasView),
        new PropertyMetadata(1d));

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

    public double LayoutAspectRatio
    {
        get => (double)GetValue(LayoutAspectRatioProperty);
        set => SetValue(LayoutAspectRatioProperty, value);
    }
}

public sealed class NormalizedCoordinateConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is double coordinate ? coordinate * 1000 : 0d;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}

public sealed class LayoutHeightConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is double aspectRatio && aspectRatio > 0 ? 1000 / aspectRatio : 1000d;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
