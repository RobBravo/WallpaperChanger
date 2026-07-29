using System.Drawing;
using System.Drawing.Imaging;
using WallpaperChanger.Core.Models;

namespace WallpaperChanger.App.Services;

public sealed class CompositeWallpaperRenderer
{
    public Bitmap Render(
        IReadOnlyList<MonitorDescriptor> monitors,
        IReadOnlyDictionary<string, string> imagePathsByMonitorId)
    {
        var left = monitors.Min(monitor => monitor.Left);
        var top = monitors.Min(monitor => monitor.Top);
        var right = monitors.Max(monitor => monitor.Left + monitor.Width);
        var bottom = monitors.Max(monitor => monitor.Top + monitor.Height);

        var bitmap = new Bitmap(right - left, bottom - top, PixelFormat.Format32bppArgb);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.Clear(Color.Black);

        foreach (var monitor in monitors)
        {
            if (!imagePathsByMonitorId.TryGetValue(monitor.Id, out var imagePath))
            {
                continue;
            }

            using var image = Image.FromFile(imagePath);
            var target = new Rectangle(monitor.Left - left, monitor.Top - top, monitor.Width, monitor.Height);
            var scale = Math.Max((float)target.Width / image.Width, (float)target.Height / image.Height);
            var sourceWidth = target.Width / scale;
            var sourceHeight = target.Height / scale;
            var sourceX = (image.Width - sourceWidth) / 2;
            var sourceY = (image.Height - sourceHeight) / 2;

            graphics.DrawImage(image, target, sourceX, sourceY, sourceWidth, sourceHeight, GraphicsUnit.Pixel);
        }

        return bitmap;
    }
}
