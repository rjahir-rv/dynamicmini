using DynamicMiniPlayer.Models;

namespace DynamicMiniPlayer.Services;

public sealed class PlayerVisibilityState
{
    public static readonly TimeSpan DismissToggleGuard = TimeSpan.FromMilliseconds(400);

    private bool _manuallyHidden;
    private DateTimeOffset? _lastDismissAt;

    public bool ShouldShow(MediaSessionSnapshot snapshot, DisplayMode mode)
    {
        if (IsInactive(snapshot) || mode != DisplayMode.Persistent)
            return false;
        return !_manuallyHidden;
    }

    public void RecordHide(bool manual)
    {
        if (manual)
            _manuallyHidden = true;
    }

    public void AllowShow() => _manuallyHidden = false;

    /// <summary>Records that the player was hidden because it lost focus (click outside).</summary>
    public void RecordDismiss(DateTimeOffset now)
    {
        RecordHide(manual: true);
        _lastDismissAt = now;
    }

    /// <summary>
    /// Clicking the tray icon deactivates the player before the click reaches the tray handler;
    /// a toggle that arrives right after such a dismiss must not reopen it.
    /// </summary>
    public bool ShouldIgnoreToggle(DateTimeOffset now) =>
        _lastDismissAt is { } dismissedAt && now - dismissedAt < DismissToggleGuard;

    public static bool IsInactive(MediaSessionSnapshot? snapshot)
    {
        if (snapshot is null)
            return true;
        if (snapshot.Status is PlayerPlaybackStatus.Closed or PlayerPlaybackStatus.Stopped)
            return true;
        return !snapshot.IsPlaying && snapshot.Duration > TimeSpan.Zero &&
               snapshot.Position >= snapshot.Duration - TimeSpan.FromSeconds(1);
    }
}
