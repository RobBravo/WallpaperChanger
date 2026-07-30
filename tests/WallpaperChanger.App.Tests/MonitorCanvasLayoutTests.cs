using WallpaperChanger.App.Views;
using Xunit;

namespace WallpaperChanger.App.Tests;

public class MonitorCanvasLayoutTests
{
    [Fact]
    public void CalculateBounds_centers_and_scales_normalized_cards_without_distorting_the_virtual_desktop()
    {
        var bounds = MonitorCanvasLayout.CalculateBounds(
            new System.Windows.Size(800, 800),
            layoutAspectRatio: 2,
            normalizedLeft: 0.25,
            normalizedTop: 0.25,
            normalizedWidth: 0.5,
            normalizedHeight: 0.5);

        Assert.Equal(200, bounds.X, 6);
        Assert.Equal(300, bounds.Y, 6);
        Assert.Equal(400, bounds.Width, 6);
        Assert.Equal(200, bounds.Height, 6);
    }

    [Fact]
    public void CalculateBounds_preserves_a_portrait_card_aspect_ratio()
    {
        var bounds = MonitorCanvasLayout.CalculateBounds(
            new System.Windows.Size(800, 600),
            layoutAspectRatio: 0.5,
            normalizedLeft: 0.25,
            normalizedTop: 0.25,
            normalizedWidth: 0.5,
            normalizedHeight: 0.5);

        Assert.Equal(325, bounds.X, 6);
        Assert.Equal(150, bounds.Y, 6);
        Assert.Equal(150, bounds.Width, 6);
        Assert.Equal(300, bounds.Height, 6);
    }

    [Fact]
    public void CalculateBounds_centers_the_virtual_desktop_when_height_constrains_the_layout()
    {
        var bounds = MonitorCanvasLayout.CalculateBounds(
            new System.Windows.Size(800, 300),
            layoutAspectRatio: 2,
            normalizedLeft: 0.25,
            normalizedTop: 0.25,
            normalizedWidth: 0.5,
            normalizedHeight: 0.5);

        Assert.Equal(250, bounds.X, 6);
        Assert.Equal(75, bounds.Y, 6);
        Assert.Equal(300, bounds.Width, 6);
        Assert.Equal(150, bounds.Height, 6);
    }
}
