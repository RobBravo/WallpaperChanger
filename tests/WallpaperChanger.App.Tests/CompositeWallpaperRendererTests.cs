using System.Drawing;
using System.Drawing.Imaging;
using WallpaperChanger.App.Services;
using WallpaperChanger.Core.Models;
using Xunit;

namespace WallpaperChanger.App.Tests;

public sealed class CompositeWallpaperRendererTests : IDisposable
{
    private readonly string temporaryDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));

    public CompositeWallpaperRendererTests()
    {
        Directory.CreateDirectory(temporaryDirectory);
    }

    [Fact]
    public void Render_translates_negative_monitor_origins_and_fills_gaps_black()
    {
        var leftImage = CreateSolidImage("left.png", 100, 100, Color.Red);
        var rightImage = CreateSolidImage("right.png", 100, 100, Color.Blue);
        var monitors = new[]
        {
            new MonitorDescriptor("left", "DISPLAY1", -100, 0, 100, 100, false),
            new MonitorDescriptor("right", "DISPLAY2", 50, 0, 100, 100, true)
        };

        using var bitmap = new CompositeWallpaperRenderer().Render(
            monitors,
            new Dictionary<string, string> { ["left"] = leftImage, ["right"] = rightImage });

        Assert.Equal(250, bitmap.Width);
        Assert.Equal(100, bitmap.Height);
        Assert.Equal(Color.Red.ToArgb(), bitmap.GetPixel(0, 50).ToArgb());
        Assert.Equal(Color.Black.ToArgb(), bitmap.GetPixel(125, 50).ToArgb());
        Assert.Equal(Color.Blue.ToArgb(), bitmap.GetPixel(150, 50).ToArgb());
    }

    [Fact]
    public void Render_positions_portrait_and_landscape_monitors_with_negative_top()
    {
        var portraitImage = CreateSolidImage("portrait.png", 60, 120, Color.MediumPurple);
        var landscapeImage = CreateSolidImage("landscape.png", 140, 60, Color.ForestGreen);
        var monitors = new[]
        {
            new MonitorDescriptor("portrait", "DISPLAY1", 0, -50, 60, 120, true),
            new MonitorDescriptor("landscape", "DISPLAY2", 60, 0, 140, 60, false)
        };

        using var bitmap = new CompositeWallpaperRenderer().Render(
            monitors,
            new Dictionary<string, string> { ["portrait"] = portraitImage, ["landscape"] = landscapeImage });

        Assert.Equal(200, bitmap.Width);
        Assert.Equal(120, bitmap.Height);
        Assert.Equal(Color.MediumPurple.ToArgb(), bitmap.GetPixel(30, 0).ToArgb());
        Assert.Equal(Color.ForestGreen.ToArgb(), bitmap.GetPixel(100, 50).ToArgb());
        Assert.Equal(Color.Black.ToArgb(), bitmap.GetPixel(100, 0).ToArgb());
    }

    [Fact]
    public void Render_uses_a_centered_cover_crop()
    {
        var sourceImage = Path.Combine(temporaryDirectory, "source.png");
        using (var source = new Bitmap(4, 2, PixelFormat.Format32bppArgb))
        {
            for (var y = 0; y < source.Height; y++)
            {
                source.SetPixel(0, y, Color.Red);
                source.SetPixel(1, y, Color.Lime);
                source.SetPixel(2, y, Color.Blue);
                source.SetPixel(3, y, Color.Yellow);
            }

            source.Save(sourceImage, ImageFormat.Png);
        }

        using var bitmap = new CompositeWallpaperRenderer().Render(
            new[] { new MonitorDescriptor("display", "DISPLAY1", 0, 0, 2, 2, true) },
            new Dictionary<string, string> { ["display"] = sourceImage });

        Assert.Equal(Color.Lime.ToArgb(), bitmap.GetPixel(0, 0).ToArgb());
        Assert.Equal(Color.Blue.ToArgb(), bitmap.GetPixel(1, 0).ToArgb());
    }

    [Fact]
    public void Render_preserves_physical_rectangles_for_a_mixed_scale_topology()
    {
        var portraitImage = CreateSolidImage("portrait-physical.png", 100, 100, Color.Red);
        var primaryImage = CreateSolidImage("primary-physical.png", 100, 100, Color.Lime);
        var rightImage = CreateSolidImage("right-physical.png", 100, 100, Color.Blue);
        var monitors = new[]
        {
            new MonitorDescriptor("portrait", "DISPLAY2", -1080, 0, 1080, 1920, false),
            new MonitorDescriptor("primary", "DISPLAY1", 0, 0, 1920, 1080, true),
            new MonitorDescriptor("right", "DISPLAY3", 1920, 0, 1920, 1080, false)
        };

        using var bitmap = new CompositeWallpaperRenderer().Render(
            monitors,
            new Dictionary<string, string>
            {
                ["portrait"] = portraitImage,
                ["primary"] = primaryImage,
                ["right"] = rightImage
            });

        Assert.Equal(4920, bitmap.Width);
        Assert.Equal(1920, bitmap.Height);
        Assert.Equal(Color.Red.ToArgb(), bitmap.GetPixel(100, 1500).ToArgb());
        Assert.Equal(Color.Lime.ToArgb(), bitmap.GetPixel(1100, 100).ToArgb());
        Assert.Equal(Color.Blue.ToArgb(), bitmap.GetPixel(3100, 100).ToArgb());
        Assert.Equal(Color.Black.ToArgb(), bitmap.GetPixel(1100, 1500).ToArgb());
    }

    public void Dispose()
    {
        Directory.Delete(temporaryDirectory, recursive: true);
    }

    private string CreateSolidImage(string fileName, int width, int height, Color color)
    {
        var path = Path.Combine(temporaryDirectory, fileName);
        using var bitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.Clear(color);
        bitmap.Save(path, ImageFormat.Png);
        return path;
    }
}
