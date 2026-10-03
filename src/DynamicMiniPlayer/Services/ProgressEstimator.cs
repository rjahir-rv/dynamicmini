using DynamicMiniPlayer.Models;

namespace DynamicMiniPlayer.Services;

public static class ProgressEstimator
{
    public static TimeSpan Estimate(MediaSessionSnapshot snapshot, DateTimeOffset now)
    {
        if (!snapshot.IsPlaying || snapshot.Duration <= TimeSpan.Zero)
            return Clamp(snapshot.Position, snapshot.Duration);

        var elapsed = now - snapshot.TimelineUpdatedAt;
        var playbackRate = snapshot.PlaybackRate is > 0 ? snapshot.PlaybackRate.Value : 1;
        var progress = elapsed > TimeSpan.Zero ? elapsed * playbackRate : TimeSpan.Zero;
        return Clamp(snapshot.Position + progress, snapshot.Duration);
    }

    public static TimeSpan Offset(TimeSpan position, TimeSpan delta, TimeSpan duration) =>
        Clamp(position + delta, duration);

    private static TimeSpan Clamp(TimeSpan value, TimeSpan duration)
    {
        if (value < TimeSpan.Zero)
            return TimeSpan.Zero;
        if (duration > TimeSpan.Zero && value > duration)
            return duration;
        return value;
    }
}
