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
