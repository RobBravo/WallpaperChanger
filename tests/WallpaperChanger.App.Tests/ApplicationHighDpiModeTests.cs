using System.Xml.Linq;
using Xunit;

namespace WallpaperChanger.App.Tests;

public class ApplicationHighDpiModeTests
{
    [Fact]
    public void Application_project_configures_per_monitor_v2_dpi_awareness()
    {
        var repositoryRoot = FindRepositoryRoot();
        var project = XDocument.Load(Path.Combine(
            repositoryRoot,
            "src",
            "WallpaperChanger.App",
            "WallpaperChanger.App.csproj"));

        Assert.Equal("PerMonitorV2", project.Descendants("ApplicationHighDpiMode").Single().Value);
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
