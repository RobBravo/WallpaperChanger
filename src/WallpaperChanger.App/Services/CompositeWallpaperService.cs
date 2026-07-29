using System.Drawing.Imaging;
using System.IO;
using WallpaperChanger.Core.Abstractions;

namespace WallpaperChanger.App.Services;

public sealed class CompositeWallpaperService : IWallpaperService
{
    private readonly IMonitorRegistry monitorRegistry;
    private readonly CompositeWallpaperRenderer renderer;
    private readonly ISystemWallpaperGateway wallpaperGateway;
    private readonly string outputDirectory;
    private readonly SemaphoreSlim applyLock = new(1, 1);

    public CompositeWallpaperService(
        IMonitorRegistry monitorRegistry,
        CompositeWallpaperRenderer renderer,
        ISystemWallpaperGateway wallpaperGateway,
        string? outputDirectory = null)
    {
        this.monitorRegistry = monitorRegistry;
        this.renderer = renderer;
        this.wallpaperGateway = wallpaperGateway;
        this.outputDirectory = outputDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "WallpaperChanger");
    }

    public async Task ApplyAsync(IReadOnlyDictionary<string, string> imagePathsByMonitorId, CancellationToken cancellationToken = default)
    {
        await applyLock.WaitAsync(cancellationToken);
        var temporaryPath = string.Empty;

        try
        {
            Directory.CreateDirectory(outputDirectory);
            temporaryPath = Path.Combine(outputDirectory, $"composite-wallpaper-{Guid.NewGuid():N}.bmp");
            var outputPath = Path.Combine(outputDirectory, "composite-wallpaper.bmp");

            using (var bitmap = renderer.Render(monitorRegistry.GetConnectedMonitors(), imagePathsByMonitorId))
            {
                bitmap.Save(temporaryPath, ImageFormat.Bmp);
            }

            File.Move(temporaryPath, outputPath, overwrite: true);
            temporaryPath = string.Empty;
            wallpaperGateway.Apply(outputPath);
        }
        finally
        {
            if (!string.IsNullOrEmpty(temporaryPath))
            {
                File.Delete(temporaryPath);
            }

            applyLock.Release();
        }
    }
}
