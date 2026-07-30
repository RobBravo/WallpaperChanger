using System.Xml.Linq;
using Xunit;

namespace WallpaperChanger.App.Tests;

public class ApplicationManifestTests
{
    [Fact]
    public void Application_manifest_declares_per_monitor_v2_dpi_awareness()
    {
        var repositoryRoot = FindRepositoryRoot();
        var manifest = XDocument.Load(Path.Combine(
            repositoryRoot,
            "src",
            "WallpaperChanger.App",
            "app.manifest"));
        var windowsSettings = XNamespace.Get("http://schemas.microsoft.com/SMI/2016/WindowsSettings");

        Assert.Equal("PerMonitorV2", manifest.Descendants(windowsSettings + "dpiAwareness").Single().Value);
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
