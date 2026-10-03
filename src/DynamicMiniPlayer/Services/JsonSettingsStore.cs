using DynamicMiniPlayer.Models;
using System.Text.Json;

namespace DynamicMiniPlayer.Services;

public sealed class JsonSettingsStore : ISettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private readonly string _settingsPath;

    public JsonSettingsStore(string? settingsPath = null)
    {
        _settingsPath = settingsPath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "DynamicMiniPlayer",
            "settings.json");
    }

    public async Task<AppSettings> LoadAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            if (!File.Exists(_settingsPath))
                return new AppSettings();

            await using var stream = File.OpenRead(_settingsPath);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            var settings = document.Deserialize<AppSettings>(JsonOptions) ?? new AppSettings();
            return Migrate(settings, ReadVersion(document.RootElement));
        }
        catch (JsonException)
        {
            return new AppSettings();
        }
        catch (IOException)
        {
            return new AppSettings();
        }
    }

    private static int ReadVersion(JsonElement root) =>
        root.ValueKind == JsonValueKind.Object &&
        root.TryGetProperty(nameof(AppSettings.SettingsVersion), out var value) &&
        value.TryGetInt32(out var version) ? version : 0;

    private static AppSettings Migrate(AppSettings settings, int version)
    {
        // v1: automatic display stopped being the default; older files that stored it are reset to on-demand.
        if (version < 1)
            settings = settings with { DisplayMode = DisplayMode.OnDemand };
        return settings with { SettingsVersion = AppSettings.CurrentVersion };
    }

    public async Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default)
    {
        var directory = Path.GetDirectoryName(_settingsPath)!;
        Directory.CreateDirectory(directory);
        var temporaryPath = _settingsPath + ".tmp";

        await using (var stream = File.Create(temporaryPath))
            await JsonSerializer.SerializeAsync(stream, settings, JsonOptions, cancellationToken);

        File.Move(temporaryPath, _settingsPath, true);
    }
}
