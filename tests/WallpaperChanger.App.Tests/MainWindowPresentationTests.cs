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
        Assert.Contains("PreviewImagePath", canvasText);
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
        var panel = XDocument.Load(Path.Combine(repositoryRoot, "src", "WallpaperChanger.App", "Views", "SelectedMonitorPanel.xaml")).ToString();
        var canvas = XDocument.Load(Path.Combine(repositoryRoot, "src", "WallpaperChanger.App", "Views", "MonitorCanvasView.xaml")).ToString();

        Assert.Contains("NewProposalCommand", panel);
        Assert.Contains("ImageCount", panel);
        Assert.Contains("ProposedImageFileName", panel);
        Assert.Contains("ProposalStatus", panel);
        Assert.Contains("PreviewImagePath", canvas);
    }

    [Fact]
    public void Main_window_hosts_a_selected_monitor_panel_with_safe_proposal_controls()
    {
        var repositoryRoot = FindRepositoryRoot();
        var panelPath = Path.Combine(repositoryRoot, "src", "WallpaperChanger.App", "Views", "SelectedMonitorPanel.xaml");
        var window = XDocument.Load(Path.Combine(repositoryRoot, "src", "WallpaperChanger.App", "MainWindow.xaml")).ToString();

        Assert.True(File.Exists(panelPath));

        var panel = XDocument.Load(panelPath).ToString();

        Assert.Contains("SelectedMonitorRow", window);
        Assert.DoesNotContain("MonitorRowView", window);
        Assert.Contains("FolderPath", panel);
        Assert.Contains("BrowseFolderCommand", panel);
        Assert.Contains("ImageCount", panel);
        Assert.Contains("ProposedImageFileName", panel);
        Assert.Contains("IntervalValue", panel);
        Assert.Contains("NewProposalCommand", panel);
        Assert.Contains("ApplyNowCommand", panel);
        Assert.Contains("CanCreateProposal", panel);
        Assert.Contains("CanApplyProposal", panel);
        Assert.Contains("ProposalActionExplanation", panel);
        var panelCodeBehind = File.ReadAllText(Path.ChangeExtension(panelPath, ".xaml.cs"));
        Assert.Contains("BitmapCacheOption.OnLoad", panelCodeBehind);
        Assert.Contains("BitmapCreateOptions.IgnoreImageCache", panelCodeBehind);
    }

    [Fact]
    public void Selected_monitor_panel_shows_the_thumbnail_fallback_only_when_no_thumbnail_is_loaded()
    {
        var repositoryRoot = FindRepositoryRoot();
        var panel = XDocument.Load(Path.Combine(repositoryRoot, "src", "WallpaperChanger.App", "Views", "SelectedMonitorPanel.xaml"));
        var presentation = XNamespace.Get("http://schemas.microsoft.com/winfx/2006/xaml/presentation");
        var x = XNamespace.Get("http://schemas.microsoft.com/winfx/2006/xaml");

        var fallback = Assert.Single(
            panel.Descendants(presentation + "TextBlock"),
            element => element.Attribute(x + "Name")?.Value == "ProposalFallback");
        var triggers = string.Concat(fallback.Descendants(presentation + "DataTrigger").Select(element => element.ToString()));

        Assert.Contains("Source, ElementName=ProposalPreview", triggers);
        Assert.Contains("{x:Null}", triggers);
        Assert.Contains("Visibility", triggers);
    }

    [Fact]
    public void Monitor_canvas_binds_preview_to_the_view_model_preview_path()
    {
        var repositoryRoot = FindRepositoryRoot();
        var canvas = XDocument.Load(Path.Combine(repositoryRoot, "src", "WallpaperChanger.App", "Views", "MonitorCanvasView.xaml"));
        var presentation = XNamespace.Get("http://schemas.microsoft.com/winfx/2006/xaml/presentation");

        var image = Assert.Single(canvas.Descendants(presentation + "Image"));

        Assert.Equal("{Binding PreviewImagePath, NotifyOnTargetUpdated=True}", image.Attribute("Source")?.Value);
        Assert.Empty(canvas.Descendants(presentation + "PriorityBinding"));
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
