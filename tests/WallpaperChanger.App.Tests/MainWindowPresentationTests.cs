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
        var presentation = XNamespace.Get("http://schemas.microsoft.com/winfx/2006/xaml/presentation");

        Assert.True(File.Exists(canvasPath));

        var canvas = XDocument.Load(canvasPath);
        var canvasText = canvas.ToString();
        var window = XDocument.Load(Path.Combine(repositoryRoot, "src", "WallpaperChanger.App", "MainWindow.xaml")).ToString();

        Assert.Contains("ItemsSource=\"{Binding ItemsSource, RelativeSource={RelativeSource AncestorType=UserControl}}\"", canvasText);
        Assert.Empty(canvas.Descendants(presentation + "Viewbox"));
        Assert.Single(canvas.Descendants(), element => element.Name.LocalName == "MonitorLayoutPanel");
        Assert.Contains("IsPortrait", canvasText);
        Assert.Contains("ProposedImagePath", canvasText);
        Assert.Contains("AutomationProperties.Name", canvasText);
        Assert.Contains("IsSelected", canvasText);
        Assert.Contains("BorderThickness", canvasText);
        Assert.Contains("ItemsSource=\"{Binding VirtualMonitors}\"", window);
        Assert.Contains("SelectedMonitor=\"{Binding SelectedVirtualMonitor, Mode=TwoWay}\"", window);
    }

    [Fact]
    public void Monitor_canvas_shows_its_empty_preview_message_when_image_loading_fails()
    {
        var repositoryRoot = FindRepositoryRoot();
        var canvas = XDocument.Load(Path.Combine(repositoryRoot, "src", "WallpaperChanger.App", "Views", "MonitorCanvasView.xaml"));
        var presentation = XNamespace.Get("http://schemas.microsoft.com/winfx/2006/xaml/presentation");
        var x = XNamespace.Get("http://schemas.microsoft.com/winfx/2006/xaml");

        var image = Assert.Single(canvas.Descendants(presentation + "Image"));
        Assert.Equal("PreviewImageFailed", image.Attribute("ImageFailed")?.Value);

        var fallback = Assert.Single(
            canvas.Descendants(presentation + "TextBlock"),
            element => element.Attribute(x + "Name")?.Value == "PreviewFallback");
        Assert.Equal("No preview available", fallback.Attribute("Text")?.Value);
    }

    [Fact]
    public void Dashboard_exposes_proposal_controls_and_details()
    {
        var repositoryRoot = FindRepositoryRoot();
        var window = XDocument.Load(Path.Combine(repositoryRoot, "src", "WallpaperChanger.App", "MainWindow.xaml")).ToString();
        var row = XDocument.Load(Path.Combine(repositoryRoot, "src", "WallpaperChanger.App", "Views", "MonitorRowView.xaml")).ToString();
        var canvas = XDocument.Load(Path.Combine(repositoryRoot, "src", "WallpaperChanger.App", "Views", "MonitorCanvasView.xaml")).ToString();

        Assert.Contains("NewProposalCommand", window);
        Assert.Contains("ImageCount", row);
        Assert.Contains("ProposedImageFileName", row);
        Assert.Contains("ProposalStatus", row);
        Assert.Contains("ProposedImagePath", canvas);
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
