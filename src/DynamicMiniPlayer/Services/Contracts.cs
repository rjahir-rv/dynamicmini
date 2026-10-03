using DynamicMiniPlayer.Models;
using System.Windows;

namespace DynamicMiniPlayer.Services;

public interface IMediaSessionService : IAsyncDisposable
{
    event EventHandler<MediaSessionSnapshot?>? CurrentSessionChanged;
    event EventHandler<IReadOnlyList<MediaSourceOption>>? SessionsChanged;
    MediaSessionSnapshot? Current { get; }
    IReadOnlyList<MediaSourceOption> Sessions { get; }
    Task InitializeAsync(CancellationToken cancellationToken = default);
    Task SelectSessionAsync(string? sessionId);
    Task<bool> TogglePlayPauseAsync();
    Task<bool> SkipPreviousAsync();
    Task<bool> SkipNextAsync();
    Task<bool> SeekAsync(TimeSpan position);
}

public interface ISettingsStore
{
    Task<AppSettings> LoadAsync(CancellationToken cancellationToken = default);
    Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default);
}

public interface IWindowPlacementService
{
    void PlaceAboveTaskbar(Window window);
    void EnableRoundedCorners(Window window);
}
