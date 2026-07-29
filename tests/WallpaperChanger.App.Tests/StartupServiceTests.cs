using WallpaperChanger.App.Services;
using Xunit;

namespace WallpaperChanger.App.Tests;

public class StartupServiceTests
{
    [Fact]
    public void Registers_the_application_in_the_run_key_with_a_quoted_executable_path()
    {
        var writer = new FakeRunKeyWriter();
        var service = new StartupService(writer, "WallpaperChanger", @"C:\Apps\WallpaperChanger.exe");

        service.EnsureRegistered();

        Assert.NotNull(writer.LastValue);
        Assert.Equal(("WallpaperChanger", @"""C:\Apps\WallpaperChanger.exe"""), writer.LastValue.Value);
    }

    private sealed class FakeRunKeyWriter : IRunKeyWriter
    {
        public (string Name, string Command)? LastValue { get; private set; }

        public void SetValue(string name, string command)
        {
            LastValue = (name, command);
        }
    }
}
