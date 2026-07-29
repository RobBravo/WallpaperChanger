using System.Text.Json;
using WallpaperChanger.Core.Abstractions;
using WallpaperChanger.Core.Models;

namespace WallpaperChanger.Core.Services;

public sealed class JsonSettingsStore : ISettingsStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true
    };

    private readonly string _filePath;

    public JsonSettingsStore(string filePath)
    {
        _filePath = filePath;
    }

    public async Task<IReadOnlyList<WallpaperMonitorProfile>> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_filePath))
        {
            return Array.Empty<WallpaperMonitorProfile>();
        }

        await using var stream = File.OpenRead(_filePath);
        return await JsonSerializer.DeserializeAsync<List<WallpaperMonitorProfile>>(stream, SerializerOptions, cancellationToken)
            ?? Array.Empty<WallpaperMonitorProfile>();
    }

    public async Task SaveAsync(IReadOnlyCollection<WallpaperMonitorProfile> profiles, CancellationToken cancellationToken = default)
    {
        var directory = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var tempPath = Path.Combine(directory ?? Path.GetTempPath(), $"{Path.GetFileName(_filePath)}.{Guid.NewGuid():N}.tmp");

        await using (var stream = File.Create(tempPath))
        {
            await JsonSerializer.SerializeAsync(stream, profiles, SerializerOptions, cancellationToken);
        }

        if (File.Exists(_filePath))
        {
            File.Replace(tempPath, _filePath, null);
        }
        else
        {
            File.Move(tempPath, _filePath);
        }
    }
}
