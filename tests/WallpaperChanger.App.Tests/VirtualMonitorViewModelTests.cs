using System.ComponentModel;
using System.Reflection;
using WallpaperChanger.App.ViewModels;
using WallpaperChanger.Core.Models;
using Xunit;

namespace WallpaperChanger.App.Tests;

public class VirtualMonitorViewModelTests
{
    [Fact]
    public void PreviewImagePath_uses_the_current_image_when_no_proposal_exists()
    {
        var monitor = CreateMonitor("current.jpg");

        Assert.Equal("current.jpg", GetPreviewImagePath(monitor));
    }

    [Fact]
    public void PreviewImagePath_uses_the_proposed_image_when_present()
    {
        var monitor = CreateMonitor("current.jpg");
        SetPath(monitor, nameof(VirtualMonitorViewModel.ProposedImagePath), "proposed.jpg");

        Assert.Equal("proposed.jpg", GetPreviewImagePath(monitor));
    }

    [Fact]
    public void PreviewImagePath_notifies_when_its_source_paths_change()
    {
        var monitor = CreateMonitor("current.jpg");
        var changes = new List<string?>();
        monitor.PropertyChanged += (_, eventArgs) => changes.Add(eventArgs.PropertyName);

        SetPath(monitor, nameof(VirtualMonitorViewModel.CurrentImagePath), "updated.jpg");
        SetPath(monitor, nameof(VirtualMonitorViewModel.ProposedImagePath), "proposed.jpg");

        Assert.Equal(2, changes.Count(propertyName => propertyName == "PreviewImagePath"));
    }

    private static VirtualMonitorViewModel CreateMonitor(string? currentImagePath)
    {
        return new VirtualMonitorViewModel(
            new MonitorDescriptor("monitor-1", "DISPLAY1", 0, 0, 1920, 1080, true),
            0,
            0,
            1,
            1,
            currentImagePath);
    }

    private static string? GetPreviewImagePath(VirtualMonitorViewModel monitor)
    {
        return typeof(VirtualMonitorViewModel)
            .GetProperty("PreviewImagePath")
            ?.GetValue(monitor) as string;
    }

    private static void SetPath(VirtualMonitorViewModel monitor, string propertyName, string path)
    {
        var property = typeof(VirtualMonitorViewModel).GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public);
        Assert.NotNull(property);
        property.SetValue(monitor, path);
    }
}
