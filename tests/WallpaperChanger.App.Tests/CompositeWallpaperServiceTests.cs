using System.Drawing;
using System.Drawing.Imaging;
using WallpaperChanger.App.Services;
using WallpaperChanger.Core.Abstractions;
using WallpaperChanger.Core.Models;
using Xunit;

namespace WallpaperChanger.App.Tests;

public sealed class CompositeWallpaperServiceTests : IDisposable
{
    private readonly string temporaryDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));

    public CompositeWallpaperServiceTests()
    {
        Directory.CreateDirectory(temporaryDirectory);
    }

    [Fact]
    public async Task ApplyAsync_writes_a_bmp_and_applies_it_once()
    {
        var imagePath = CreateImage("source.png");
        var gateway = new FakeSystemWallpaperGateway();
        var service = CreateService(gateway);

        await service.ApplyAsync(new Dictionary<string, string> { ["display"] = imagePath });

        var outputPath = Path.Combine(temporaryDirectory, "composite-wallpaper.bmp");
        Assert.True(File.Exists(outputPath));
        using var bitmap = new Bitmap(outputPath);
        Assert.Equal(ImageFormat.Bmp.Guid, bitmap.RawFormat.Guid);
        Assert.Equal(new[] { outputPath }, gateway.AppliedPaths);
    }

    [Fact]
    public async Task ApplyAsync_serializes_overlapping_calls()
    {
        var imagePath = CreateImage("source.png");
        using var firstCallStarted = new ManualResetEventSlim();
        using var allowFirstCallToFinish = new ManualResetEventSlim();
        var gateway = new FakeSystemWallpaperGateway(() =>
        {
            firstCallStarted.Set();
            allowFirstCallToFinish.Wait();
        });
        var service = CreateService(gateway);

        var firstApply = Task.Run(() => service.ApplyAsync(new Dictionary<string, string> { ["display"] = imagePath }));
        Assert.True(firstCallStarted.Wait(TimeSpan.FromSeconds(5)));
        var secondApply = service.ApplyAsync(new Dictionary<string, string> { ["display"] = imagePath });

        Assert.Equal(1, gateway.ApplyCount);
        allowFirstCallToFinish.Set();
        await Task.WhenAll(firstApply, secondApply);

        Assert.Equal(2, gateway.ApplyCount);
    }

    [Fact]
    public async Task ApplyAsync_releases_the_lock_when_the_gateway_throws()
    {
        var imagePath = CreateImage("source.png");
        var gateway = new FakeSystemWallpaperGateway { ExceptionToThrow = new InvalidOperationException("apply failed") };
        var service = CreateService(gateway);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ApplyAsync(new Dictionary<string, string> { ["display"] = imagePath }));
        gateway.ExceptionToThrow = null;
        await service.ApplyAsync(new Dictionary<string, string> { ["display"] = imagePath });

        Assert.Equal(2, gateway.ApplyCount);
    }

    public void Dispose()
    {
        Directory.Delete(temporaryDirectory, recursive: true);
    }

    private CompositeWallpaperService CreateService(ISystemWallpaperGateway gateway)
    {
        return new CompositeWallpaperService(
            new FakeMonitorRegistry(),
            new CompositeWallpaperRenderer(),
            gateway,
            temporaryDirectory);
    }

    private string CreateImage(string fileName)
    {
        var path = Path.Combine(temporaryDirectory, fileName);
        using var bitmap = new Bitmap(20, 10, PixelFormat.Format32bppArgb);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.Clear(Color.CornflowerBlue);
        bitmap.Save(path, ImageFormat.Png);
        return path;
    }

    private sealed class FakeMonitorRegistry : IMonitorRegistry
    {
        public IReadOnlyList<MonitorDescriptor> GetConnectedMonitors() =>
            new[] { new MonitorDescriptor("display", "DISPLAY1", 0, 0, 20, 10, true) };
    }

    private sealed class FakeSystemWallpaperGateway : ISystemWallpaperGateway
    {
        private readonly Action? onApply;

        public FakeSystemWallpaperGateway(Action? onApply = null)
        {
            this.onApply = onApply;
        }

        public List<string> AppliedPaths { get; } = [];

        public int ApplyCount => AppliedPaths.Count;

        public Exception? ExceptionToThrow { get; set; }

        public void Apply(string bitmapPath)
        {
            AppliedPaths.Add(bitmapPath);
            onApply?.Invoke();
            if (ExceptionToThrow is not null)
            {
                throw ExceptionToThrow;
            }
        }
    }
}
