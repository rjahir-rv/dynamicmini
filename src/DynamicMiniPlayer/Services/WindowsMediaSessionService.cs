using DynamicMiniPlayer.Models;
using System.Runtime.CompilerServices;
using Windows.Media.Control;
using Windows.Storage.Streams;

namespace DynamicMiniPlayer.Services;

public sealed class WindowsMediaSessionService : IMediaSessionService
{
    private readonly SemaphoreSlim _refreshLock = new(1, 1);
    private GlobalSystemMediaTransportControlsSessionManager? _manager;
    private GlobalSystemMediaTransportControlsSession? _activeSession;
    private string? _selectedSessionId;
    private List<SessionItem> _sessionItems = [];
    private readonly CancellationTokenSource _pollCts = new();
    private bool _disposed;

    public event EventHandler<MediaSessionSnapshot?>? CurrentSessionChanged;
    public event EventHandler<IReadOnlyList<MediaSourceOption>>? SessionsChanged;

    public MediaSessionSnapshot? Current { get; private set; }
    public IReadOnlyList<MediaSourceOption> Sessions { get; private set; } = [];

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _manager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
        _manager.CurrentSessionChanged += OnManagerChanged;
        _manager.SessionsChanged += OnSessionsChanged;
        await RefreshAsync(cancellationToken);
        _ = PollPlaybackStatusAsync(_pollCts.Token);
    }

    // Right after launch Windows can report a stale status (e.g. Paused) for sessions that are
    // already playing and may not raise PlaybackInfoChanged afterwards, so re-sync when it drifts.
    private async Task PollPlaybackStatusAsync(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
        try
        {
            while (await timer.WaitForNextTickAsync(cancellationToken))
            {
                var session = _activeSession;
                if (session is null || Current is null)
                    continue;

                PlayerPlaybackStatus status;
                try { status = ConvertStatus(session.GetPlaybackInfo()?.PlaybackStatus); }
                catch { continue; }

                if (status != Current.Status)
                    await SafeRefreshCurrentAsync();
            }
        }
        catch (OperationCanceledException) { }
    }

    public async Task SelectSessionAsync(string? sessionId)
    {
        _selectedSessionId = sessionId;
        await RefreshAsync();
    }

    public Task<bool> TogglePlayPauseAsync() => ExecuteAsync(session => session.TryTogglePlayPauseAsync().AsTask());
    public Task<bool> SkipPreviousAsync() => ExecuteAsync(session => session.TrySkipPreviousAsync().AsTask());
    public Task<bool> SkipNextAsync() => ExecuteAsync(session => session.TrySkipNextAsync().AsTask());
    public Task<bool> SeekAsync(TimeSpan position) =>
        ExecuteAsync(session => session.TryChangePlaybackPositionAsync(Math.Max(0, position.Ticks)).AsTask());

    private async Task<bool> ExecuteAsync(Func<GlobalSystemMediaTransportControlsSession, Task<bool>> command)
    {
        var session = _activeSession;
        if (session is null)
            return false;

        try
        {
            var succeeded = await command(session);
            if (succeeded)
                await RefreshCurrentAsync();
            return succeeded;
        }
        catch
        {
            return false;
        }
    }

    private async void OnManagerChanged(GlobalSystemMediaTransportControlsSessionManager sender, CurrentSessionChangedEventArgs args) =>
        await SafeRefreshAsync();

    private async void OnSessionsChanged(GlobalSystemMediaTransportControlsSessionManager sender, SessionsChangedEventArgs args) =>
        await SafeRefreshAsync();

    private async void OnMediaPropertiesChanged(GlobalSystemMediaTransportControlsSession sender, MediaPropertiesChangedEventArgs args) =>
        await SafeRefreshCurrentAsync();

    private async void OnPlaybackInfoChanged(GlobalSystemMediaTransportControlsSession sender, PlaybackInfoChangedEventArgs args) =>
        await SafeRefreshCurrentAsync();

    private async void OnTimelinePropertiesChanged(GlobalSystemMediaTransportControlsSession sender, TimelinePropertiesChangedEventArgs args) =>
        await SafeRefreshCurrentAsync();

    private async Task SafeRefreshAsync()
    {
        try { await RefreshAsync(); } catch { }
    }

    private async Task SafeRefreshCurrentAsync()
    {
        try { await RefreshCurrentAsync(); } catch { }
    }

    private async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        if (_manager is null || _disposed)
            return;

        await _refreshLock.WaitAsync(cancellationToken);
        try
        {
            var sessions = _manager.GetSessions().ToList();
            var items = new List<SessionItem>(sessions.Count);
            foreach (var session in sessions)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var id = CreateSessionId(session);
                var label = await GetSessionLabelAsync(session);
                items.Add(new SessionItem(id, session.SourceAppUserModelId, label, session));
            }

            _sessionItems = items;
            Sessions = items.Select(item => new MediaSourceOption(item.Id, item.SourceAppId, item.Label)).ToArray();
            SessionsChanged?.Invoke(this, Sessions);

            var selected = items.FirstOrDefault(item => item.Id == _selectedSessionId)?.Session;
            selected ??= _manager.GetCurrentSession();
            AttachActiveSession(selected);
            await RefreshCurrentCoreAsync();
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    private async Task RefreshCurrentAsync()
    {
        if (_disposed)
            return;

        await _refreshLock.WaitAsync();
        try { await RefreshCurrentCoreAsync(); }
        finally { _refreshLock.Release(); }
    }

    private async Task RefreshCurrentCoreAsync()
    {
        var session = _activeSession;
        if (session is null)
        {
            Current = null;
            CurrentSessionChanged?.Invoke(this, null);
            return;
        }

        try
        {
            var properties = await session.TryGetMediaPropertiesAsync();
            var playback = session.GetPlaybackInfo();
            var timeline = session.GetTimelineProperties();
            var controls = playback?.Controls;
            var item = _sessionItems.FirstOrDefault(value => ReferenceEquals(value.Session, session));
            var duration = timeline.EndTime > timeline.StartTime ? timeline.EndTime - timeline.StartTime : TimeSpan.Zero;
            var position = timeline.Position - timeline.StartTime;

            Current = new MediaSessionSnapshot(
                item?.Id ?? CreateSessionId(session),
                session.SourceAppUserModelId,
                item?.Label ?? FriendlySourceName(session.SourceAppUserModelId),
                string.IsNullOrWhiteSpace(properties?.Title) ? "Contenido multimedia" : properties.Title,
                properties?.Artist ?? string.Empty,
                properties?.Thumbnail is null ? null : await ReadThumbnailAsync(properties.Thumbnail),
                ConvertStatus(playback?.PlaybackStatus),
                position,
                duration,
                timeline.LastUpdatedTime,
                new PlaybackCapabilities(
                    controls?.IsPlayEnabled == true || controls?.IsPlayPauseToggleEnabled == true,
                    controls?.IsPauseEnabled == true || controls?.IsPlayPauseToggleEnabled == true,
                    controls?.IsPreviousEnabled == true,
                    controls?.IsNextEnabled == true,
                    controls?.IsPlaybackPositionEnabled == true),
                playback?.PlaybackRate);

            CurrentSessionChanged?.Invoke(this, Current);
        }
        catch
        {
            Current = null;
            CurrentSessionChanged?.Invoke(this, null);
        }
    }

    private void AttachActiveSession(GlobalSystemMediaTransportControlsSession? session)
    {
        if (ReferenceEquals(_activeSession, session))
            return;

        if (_activeSession is not null)
        {
            _activeSession.MediaPropertiesChanged -= OnMediaPropertiesChanged;
            _activeSession.PlaybackInfoChanged -= OnPlaybackInfoChanged;
            _activeSession.TimelinePropertiesChanged -= OnTimelinePropertiesChanged;
        }

        _activeSession = session;
        if (_activeSession is not null)
        {
            _activeSession.MediaPropertiesChanged += OnMediaPropertiesChanged;
            _activeSession.PlaybackInfoChanged += OnPlaybackInfoChanged;
            _activeSession.TimelinePropertiesChanged += OnTimelinePropertiesChanged;
        }
    }

    private static string CreateSessionId(GlobalSystemMediaTransportControlsSession session) =>
        $"{session.SourceAppUserModelId}|{RuntimeHelpers.GetHashCode(session):X8}";

    private static async Task<string> GetSessionLabelAsync(GlobalSystemMediaTransportControlsSession session)
    {
        try
        {
            var media = await session.TryGetMediaPropertiesAsync();
            var source = FriendlySourceName(session.SourceAppUserModelId);
            return string.IsNullOrWhiteSpace(media?.Title) ? source : $"{source} — {media.Title}";
        }
        catch
        {
            return FriendlySourceName(session.SourceAppUserModelId);
        }
    }

    private static string FriendlySourceName(string appId)
    {
        var lower = appId.ToLowerInvariant();
        if (lower.Contains("chrome")) return "Google Chrome";
        if (lower.Contains("msedge")) return "Microsoft Edge";
        if (lower.Contains("firefox")) return "Firefox";
        if (lower.Contains("spotify")) return "Spotify";
        var fileName = Path.GetFileNameWithoutExtension(appId);
        return string.IsNullOrWhiteSpace(fileName) ? appId : fileName;
    }

    private static PlayerPlaybackStatus ConvertStatus(GlobalSystemMediaTransportControlsSessionPlaybackStatus? status) => status switch
    {
        GlobalSystemMediaTransportControlsSessionPlaybackStatus.Closed => PlayerPlaybackStatus.Closed,
        GlobalSystemMediaTransportControlsSessionPlaybackStatus.Opened => PlayerPlaybackStatus.Opened,
        GlobalSystemMediaTransportControlsSessionPlaybackStatus.Changing => PlayerPlaybackStatus.Changing,
        GlobalSystemMediaTransportControlsSessionPlaybackStatus.Stopped => PlayerPlaybackStatus.Stopped,
        GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing => PlayerPlaybackStatus.Playing,
        GlobalSystemMediaTransportControlsSessionPlaybackStatus.Paused => PlayerPlaybackStatus.Paused,
        _ => PlayerPlaybackStatus.Unknown
    };

    private static async Task<byte[]?> ReadThumbnailAsync(IRandomAccessStreamReference reference)
    {
        try
        {
            using var stream = await reference.OpenReadAsync();
            if (stream.Size == 0 || stream.Size > 10 * 1024 * 1024)
                return null;
            using var reader = new DataReader(stream.GetInputStreamAt(0));
            await reader.LoadAsync((uint)stream.Size);
            var bytes = new byte[checked((int)stream.Size)];
            reader.ReadBytes(bytes);
            return bytes;
        }
        catch
        {
            return null;
        }
    }

    public ValueTask DisposeAsync()
    {
        if (_disposed)
            return ValueTask.CompletedTask;
        _disposed = true;
        _pollCts.Cancel();

        AttachActiveSession(null);
        if (_manager is not null)
        {
            _manager.CurrentSessionChanged -= OnManagerChanged;
            _manager.SessionsChanged -= OnSessionsChanged;
        }
        _pollCts.Dispose();
        _refreshLock.Dispose();
        return ValueTask.CompletedTask;
    }

    private sealed record SessionItem(
        string Id,
        string SourceAppId,
        string Label,
        GlobalSystemMediaTransportControlsSession Session);
}
