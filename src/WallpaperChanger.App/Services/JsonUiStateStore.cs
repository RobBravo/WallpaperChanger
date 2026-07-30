using System.IO;
using System.Text.Json;

namespace WallpaperChanger.App.Services;

public sealed class JsonUiStateStore : IUiStateStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented = true };
    private readonly string filePath;

    public JsonUiStateStore(string filePath)
    {
        this.filePath = filePath;
    }

    public async Task<UiState> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(filePath))
        {
            return new UiState(null);
        }

        try
        {
            await using var stream = File.OpenRead(filePath);
            return await JsonSerializer.DeserializeAsync<UiState>(stream, SerializerOptions, cancellationToken) ?? new UiState(null);
        }
        catch (JsonException)
        {
            return new UiState(null);
        }
        catch (IOException)
        {
            return new UiState(null);
        }
    }

    public async Task SaveAsync(UiState state, CancellationToken cancellationToken = default)
    {
        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var tempPath = Path.Combine(directory ?? Path.GetTempPath(), $"{Path.GetFileName(filePath)}.{Guid.NewGuid():N}.tmp");
        await using (var stream = File.Create(tempPath))
        {
            await JsonSerializer.SerializeAsync(stream, state, SerializerOptions, cancellationToken);
        }

        if (File.Exists(filePath))
        {
            File.Replace(tempPath, filePath, null);
        }
        else
        {
            File.Move(tempPath, filePath);
        }
    }
}
