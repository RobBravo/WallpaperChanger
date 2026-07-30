using System.Buffers.Binary;
using System.Xml.Linq;
using Xunit;

namespace WallpaperChanger.App.Tests;

public class ApplicationIconTests
{
    [Fact]
    public void Application_icon_is_a_multi_resolution_asset_wired_to_the_application_and_window()
    {
        var repositoryRoot = FindRepositoryRoot();
        var iconPath = Path.Combine(repositoryRoot, "src", "WallpaperChanger.App", "Assets", "WallpaperChanger.ico");
        var iconBytes = File.ReadAllBytes(iconPath);
        var project = XDocument.Load(Path.Combine(repositoryRoot, "src", "WallpaperChanger.App", "WallpaperChanger.App.csproj"));
        var window = XDocument.Load(Path.Combine(repositoryRoot, "src", "WallpaperChanger.App", "MainWindow.xaml"));
        var trayIconService = File.ReadAllText(Path.Combine(repositoryRoot, "src", "WallpaperChanger.App", "Services", "TrayIconService.cs"));

        Assert.Equal("Assets\\WallpaperChanger.ico", project.Descendants("ApplicationIcon").Single().Value);
        Assert.Equal("/Assets/WallpaperChanger.ico", window.Root?.Attribute("Icon")?.Value);
        Assert.Contains("pack://application:,,,/Assets/WallpaperChanger.ico", trayIconService);
        Assert.True(iconBytes.Length >= 6);
        Assert.Equal((ushort)0, BinaryPrimitives.ReadUInt16LittleEndian(iconBytes));
        Assert.Equal((ushort)1, BinaryPrimitives.ReadUInt16LittleEndian(iconBytes.AsSpan(2)));

        var imageCount = BinaryPrimitives.ReadUInt16LittleEndian(iconBytes.AsSpan(4));
        Assert.True(imageCount >= 3);
        Assert.Contains(16, GetImageWidths(iconBytes, imageCount));
        Assert.Contains(32, GetImageWidths(iconBytes, imageCount));
        Assert.Contains(48, GetImageWidths(iconBytes, imageCount));
    }

    private static IEnumerable<int> GetImageWidths(byte[] iconBytes, ushort imageCount)
    {
        for (var imageIndex = 0; imageIndex < imageCount; imageIndex++)
        {
            var width = iconBytes[6 + (imageIndex * 16)];
            yield return width == 0 ? 256 : width;
        }
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
