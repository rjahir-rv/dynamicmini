using DynamicMiniPlayer.Models;
using DynamicMiniPlayer.Services;

namespace DynamicMiniPlayer.Tests;

[TestClass]
public sealed class JsonSettingsStoreTests
{
    private string _directory = null!;
    private string _path = null!;

    [TestInitialize]
    public void Initialize()
    {
        _directory = Path.Combine(Path.GetTempPath(), "DynamicMiniPlayer.Tests", Guid.NewGuid().ToString("N"));
        _path = Path.Combine(_directory, "settings.json");
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_directory))
            Directory.Delete(_directory, true);
    }

    [TestMethod]
    public async Task MissingFile_ReturnsProductDefaults()
    {
        var settings = await new JsonSettingsStore(_path).LoadAsync();

        Assert.AreEqual(DisplayMode.OnDemand, settings.DisplayMode);
        Assert.IsTrue(settings.StartWithWindows);
    }

    [TestMethod]
    public async Task LegacyFileWithPersistentMode_IsMigratedToOnDemand()
    {
        Directory.CreateDirectory(_directory);
        await File.WriteAllTextAsync(_path, """{ "DisplayMode": 0, "StartWithWindows": false, "PreferredSourceAppId": "spotify.exe" }""");

        var settings = await new JsonSettingsStore(_path).LoadAsync();

        Assert.AreEqual(DisplayMode.OnDemand, settings.DisplayMode);
        Assert.AreEqual(AppSettings.CurrentVersion, settings.SettingsVersion);
        Assert.IsFalse(settings.StartWithWindows);
        Assert.AreEqual("spotify.exe", settings.PreferredSourceAppId);
    }

    [TestMethod]
    public async Task PersistentModeChosenAfterMigration_IsPreserved()
    {
        var store = new JsonSettingsStore(_path);
        await store.SaveAsync(new AppSettings { DisplayMode = DisplayMode.Persistent });

        var settings = await store.LoadAsync();

        Assert.AreEqual(DisplayMode.Persistent, settings.DisplayMode);
    }

    [TestMethod]
    public async Task CorruptFile_ReturnsProductDefaults()
    {
        Directory.CreateDirectory(_directory);
        await File.WriteAllTextAsync(_path, "{ definitely-not-json");

        var settings = await new JsonSettingsStore(_path).LoadAsync();

        Assert.AreEqual(new AppSettings(), settings);
    }

    [TestMethod]
    public async Task SaveAndLoad_RoundTripsPreferences()
    {
        var store = new JsonSettingsStore(_path);
        var expected = new AppSettings
        {
            DisplayMode = DisplayMode.OnDemand,
            StartWithWindows = false,
            PreferredSourceAppId = "spotify.exe"
        };

        await store.SaveAsync(expected);
        var actual = await store.LoadAsync();

        Assert.AreEqual(expected, actual);
    }
}
