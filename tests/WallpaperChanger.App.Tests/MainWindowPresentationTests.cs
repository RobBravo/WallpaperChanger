using System.Xml.Linq;
using Xunit;

namespace WallpaperChanger.App.Tests;

public class MainWindowPresentationTests
{
    [Fact]
    public void Main_window_opens_maximized_with_the_requested_title()
    {
        var repositoryRoot = FindRepositoryRoot();
        var window = XDocument.Load(Path.Combine(repositoryRoot, "src", "WallpaperChanger.App", "MainWindow.xaml"));

        Assert.Equal("WalpaperChangeer - La mejor forma de gestionar tus fondos de escritorio", window.Root?.Attribute("Title")?.Value);
        Assert.Equal("Maximized", window.Root?.Attribute("WindowState")?.Value);
    }

    [Fact]
    public void Main_window_hosts_an_accessible_adaptive_monitor_canvas()
    {
        var repositoryRoot = FindRepositoryRoot();
        var canvasPath = Path.Combine(repositoryRoot, "src", "WallpaperChanger.App", "Views", "MonitorCanvasView.xaml");

        Assert.True(File.Exists(canvasPath));

        var canvas = XDocument.Load(canvasPath).ToString();
        var window = XDocument.Load(Path.Combine(repositoryRoot, "src", "WallpaperChanger.App", "MainWindow.xaml")).ToString();

        Assert.Contains("ItemsSource=\"{Binding ItemsSource, RelativeSource={RelativeSource AncestorType=UserControl}}\"", canvas);
        Assert.Contains("NormalizedLeft", canvas);
        Assert.Contains("NormalizedTop", canvas);
        Assert.Contains("NormalizedWidth", canvas);
        Assert.Contains("NormalizedHeight", canvas);
        Assert.Contains("LayoutAspectRatio", canvas);
        Assert.Contains("IsPortrait", canvas);
        Assert.Contains("CurrentImagePath", canvas);
        Assert.Contains("AutomationProperties.Name", canvas);
        Assert.Contains("IsSelected", canvas);
        Assert.Contains("BorderThickness", canvas);
        Assert.Contains("ItemsSource=\"{Binding VirtualMonitors}\"", window);
        Assert.Contains("SelectedMonitor=\"{Binding SelectedVirtualMonitor, Mode=TwoWay}\"", window);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "WallpaperChanger.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate the repository root.");
    }
}
