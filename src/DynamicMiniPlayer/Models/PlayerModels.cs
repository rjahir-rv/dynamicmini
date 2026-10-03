namespace DynamicMiniPlayer.Models;

public enum PlayerPlaybackStatus
{
    Unknown,
    Closed,
    Opened,
    Changing,
    Stopped,
    Playing,
    Paused
}

public sealed record PlaybackCapabilities(
    bool CanPlay,
    bool CanPause,
    bool CanPrevious,
    bool CanNext,
    bool CanSeek);

public sealed record MediaSessionSnapshot(
    string SessionId,
    string SourceAppId,
    string SourceDisplayName,
    string Title,
    string Artist,
    byte[]? Artwork,
    PlayerPlaybackStatus Status,
    TimeSpan Position,
    TimeSpan Duration,
    DateTimeOffset TimelineUpdatedAt,
    PlaybackCapabilities Capabilities,
    double? PlaybackRate = null)
{
    public bool IsPlaying => Status == PlayerPlaybackStatus.Playing ||
        (Status is PlayerPlaybackStatus.Opened or PlayerPlaybackStatus.Unknown && PlaybackRate is > 0);
}

public sealed record MediaSourceOption(string SessionId, string SourceAppId, string DisplayName);

public enum DisplayMode
{
    Persistent,
    OnDemand
}

public sealed record AppSettings
{
    public const int CurrentVersion = 1;

    public int SettingsVersion { get; init; } = CurrentVersion;
    public DisplayMode DisplayMode { get; init; } = DisplayMode.OnDemand;
    public bool StartWithWindows { get; init; } = true;
    public string Theme { get; init; } = "System";
    public string? PreferredSourceAppId { get; init; }
}
