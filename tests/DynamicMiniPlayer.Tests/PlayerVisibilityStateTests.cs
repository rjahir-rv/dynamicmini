using DynamicMiniPlayer.Models;
using DynamicMiniPlayer.Services;

namespace DynamicMiniPlayer.Tests;

[TestClass]
public sealed class PlayerVisibilityStateTests
{
    [TestMethod]
    public void ManualHide_SurvivesTimelineUpdatesForSameContent()
    {
        var state = new PlayerVisibilityState();
        var original = Snapshot("Song", TimeSpan.FromSeconds(10), PlayerPlaybackStatus.Playing);
        Assert.IsTrue(state.ShouldShow(original, DisplayMode.Persistent));
        state.RecordHide(manual: true);

        var timelineUpdate = original with { Position = TimeSpan.FromSeconds(30), TimelineUpdatedAt = DateTimeOffset.Now };

        Assert.IsFalse(state.ShouldShow(timelineUpdate, DisplayMode.Persistent));
    }

    [TestMethod]
    public void NewContent_RemainsHiddenUntilUserExplicitlyShowsPlayer()
    {
        var state = new PlayerVisibilityState();
        state.ShouldShow(Snapshot("First", TimeSpan.Zero, PlayerPlaybackStatus.Playing), DisplayMode.Persistent);
        state.RecordHide(manual: true);

        var beforeUserAction = state.ShouldShow(Snapshot("Second", TimeSpan.Zero, PlayerPlaybackStatus.Playing), DisplayMode.Persistent);
        state.AllowShow();
        var afterUserAction = state.ShouldShow(Snapshot("Second", TimeSpan.Zero, PlayerPlaybackStatus.Playing), DisplayMode.Persistent);

        Assert.IsFalse(beforeUserAction);
        Assert.IsTrue(afterUserAction);
    }

    [TestMethod]
    public void StoppedOrFinishedContent_IsInactive()
    {
        Assert.IsTrue(PlayerVisibilityState.IsInactive(Snapshot("Song", TimeSpan.FromSeconds(10), PlayerPlaybackStatus.Stopped)));
        Assert.IsTrue(PlayerVisibilityState.IsInactive(Snapshot("Song", TimeSpan.FromMinutes(3), PlayerPlaybackStatus.Paused)));
        Assert.IsFalse(PlayerVisibilityState.IsInactive(Snapshot("Song", TimeSpan.FromMinutes(1), PlayerPlaybackStatus.Paused)));
    }

    [TestMethod]
    public void DefaultSettings_DoNotShowPlayerAutomatically()
    {
        var state = new PlayerVisibilityState();

        Assert.IsFalse(state.ShouldShow(Snapshot("Song", TimeSpan.Zero, PlayerPlaybackStatus.Playing), new AppSettings().DisplayMode));
    }

    [TestMethod]
    public void ToggleRightAfterDismiss_IsIgnored()
    {
        var state = new PlayerVisibilityState();
        var dismissedAt = DateTimeOffset.Parse("2026-01-01T12:00:00Z");

        Assert.IsFalse(state.ShouldIgnoreToggle(dismissedAt));
        state.RecordDismiss(dismissedAt);

        Assert.IsTrue(state.ShouldIgnoreToggle(dismissedAt.AddMilliseconds(100)));
        Assert.IsFalse(state.ShouldIgnoreToggle(dismissedAt + PlayerVisibilityState.DismissToggleGuard));
    }

    [TestMethod]
    public void Dismiss_KeepsPersistentModeFromReopeningSameSession()
    {
        var state = new PlayerVisibilityState();
        state.RecordDismiss(DateTimeOffset.Now);

        Assert.IsFalse(state.ShouldShow(Snapshot("Song", TimeSpan.Zero, PlayerPlaybackStatus.Playing), DisplayMode.Persistent));
    }

    private static MediaSessionSnapshot Snapshot(string title, TimeSpan position, PlayerPlaybackStatus status) =>
        new("session", "app", "App", title, "Artist", null, status, position, TimeSpan.FromMinutes(3), DateTimeOffset.Now,
            new PlaybackCapabilities(true, true, true, true, true));
}
