using Microsoft.Win32;

namespace WallpaperChanger.App.Services;

public interface IRunKeyWriter
{
    void SetValue(string name, string command);
}

public sealed class StartupService
{
    private readonly IRunKeyWriter runKeyWriter;
    private readonly string applicationName;
    private readonly string executablePath;

    public StartupService(IRunKeyWriter runKeyWriter, string applicationName, string executablePath)
    {
        this.runKeyWriter = runKeyWriter;
        this.applicationName = applicationName;
        this.executablePath = executablePath;
    }

    public void EnsureRegistered()
    {
        runKeyWriter.SetValue(applicationName, $"\"{executablePath}\"");
    }
}

public sealed class CurrentUserRunKeyWriter : IRunKeyWriter
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";

    public void SetValue(string name, string command)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true);
        key?.SetValue(name, command, RegistryValueKind.String);
    }
}
