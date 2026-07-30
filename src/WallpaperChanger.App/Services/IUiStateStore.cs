namespace WallpaperChanger.App.Services;

public sealed record UiState(string? SelectedMonitorId);

public interface IUiStateStore
{
    Task<UiState> LoadAsync(CancellationToken cancellationToken = default);

    Task SaveAsync(UiState state, CancellationToken cancellationToken = default);
}
