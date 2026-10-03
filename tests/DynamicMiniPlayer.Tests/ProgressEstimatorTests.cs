using DynamicMiniPlayer.Models;
using DynamicMiniPlayer.Services;

namespace DynamicMiniPlayer.Tests;

[TestClass]
public sealed class ProgressEstimatorTests
{
    [TestMethod]
    public void PlayingSession_AdvancesFromLastUpdate()
    {
        var updatedAt = DateTimeOffset.Parse("2026-01-01T12:00:00Z");
        var snapshot = Snapshot(PlayerPlaybackStatus.Playing, TimeSpan.FromSeconds(20), TimeSpan.FromMinutes(3), updatedAt);

        var result = ProgressEstimator.Estimate(snapshot, updatedAt.AddSeconds(5));

        Assert.AreEqual(TimeSpan.FromSeconds(25), result);
    }

    [TestMethod]
    public void PausedSession_DoesNotAdvance()
    {
        var updatedAt = DateTimeOffset.Parse("2026-01-01T12:00:00Z");
        var snapshot = Snapshot(PlayerPlaybackStatus.Paused, TimeSpan.FromSeconds(20), TimeSpan.FromMinutes(3), updatedAt);

        var result = ProgressEstimator.Estimate(snapshot, updatedAt.AddMinutes(1));

        Assert.AreEqual(TimeSpan.FromSeconds(20), result);
    }

    [TestMethod]
    public void OpenedSession_WithPositivePlaybackRate_Advances()
    {
        var updatedAt = DateTimeOffset.Parse("2026-01-01T12:00:00Z");
        var snapshot = Snapshot(PlayerPlaybackStatus.Opened, TimeSpan.FromSeconds(20), TimeSpan.FromMinutes(3), updatedAt, 1);

        var result = ProgressEstimator.Estimate(snapshot, updatedAt.AddSeconds(5));

        Assert.AreEqual(TimeSpan.FromSeconds(25), result);
    }

    [TestMethod]
    public void OpenedSession_WithoutPlaybackRate_DoesNotAdvance()
    {
        var updatedAt = DateTimeOffset.Parse("2026-01-01T12:00:00Z");
        var snapshot = Snapshot(PlayerPlaybackStatus.Opened, TimeSpan.FromSeconds(20), TimeSpan.FromMinutes(3), updatedAt);

        var result = ProgressEstimator.Estimate(snapshot, updatedAt.AddSeconds(5));

        Assert.AreEqual(TimeSpan.FromSeconds(20), result);
    }

    [TestMethod]
    public void PlayingSession_UsesReportedPlaybackRate()
    {
        var updatedAt = DateTimeOffset.Parse("2026-01-01T12:00:00Z");
        var snapshot = Snapshot(PlayerPlaybackStatus.Playing, TimeSpan.FromSeconds(20), TimeSpan.FromMinutes(3), updatedAt, 2);

        var result = ProgressEstimator.Estimate(snapshot, updatedAt.AddSeconds(5));

        Assert.AreEqual(TimeSpan.FromSeconds(30), result);
    }

    [TestMethod]
    public void Position_IsClampedToDuration()
    {
        var updatedAt = DateTimeOffset.Parse("2026-01-01T12:00:00Z");
        var snapshot = Snapshot(PlayerPlaybackStatus.Playing, TimeSpan.FromSeconds(58), TimeSpan.FromMinutes(1), updatedAt);

        var result = ProgressEstimator.Estimate(snapshot, updatedAt.AddSeconds(10));

        Assert.AreEqual(TimeSpan.FromMinutes(1), result);
    }

    [TestMethod]
    public void Offset_MovesPositionWithinDuration()
    {
        var duration = TimeSpan.FromMinutes(3);

        Assert.AreEqual(TimeSpan.FromSeconds(40), ProgressEstimator.Offset(TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(10), duration));
        Assert.AreEqual(TimeSpan.FromSeconds(20), ProgressEstimator.Offset(TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(-10), duration));
    }

    [TestMethod]
    public void Offset_IsClampedToStartAndEnd()
    {
        var duration = TimeSpan.FromMinutes(3);

        Assert.AreEqual(TimeSpan.Zero, ProgressEstimator.Offset(TimeSpan.FromSeconds(4), TimeSpan.FromSeconds(-10), duration));
        Assert.AreEqual(duration, ProgressEstimator.Offset(TimeSpan.FromSeconds(175), TimeSpan.FromSeconds(10), duration));
    }

    private static MediaSessionSnapshot Snapshot(PlayerPlaybackStatus status, TimeSpan position, TimeSpan duration,
        DateTimeOffset updatedAt, double? playbackRate = null) =>
        new("id", "app", "App", "Title", "Artist", null, status, position, duration, updatedAt,
            new PlaybackCapabilities(true, true, true, true, true), playbackRate);
}
