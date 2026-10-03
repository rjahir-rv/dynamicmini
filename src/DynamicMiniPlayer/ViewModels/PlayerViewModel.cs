using DynamicMiniPlayer.Models;
using DynamicMiniPlayer.Services;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace DynamicMiniPlayer.ViewModels;

public sealed class PlayerViewModel : ObservableObject, IDisposable
{
    private readonly IMediaSessionService _media;
    private readonly ISettingsStore _settingsStore;
    private readonly DispatcherTimer _progressTimer;
    private AppSettings _settings;
    private MediaSessionSnapshot? _snapshot;
    private MediaSourceOption? _selectedSource;
    private ImageSource? _artwork;
    private double _positionSeconds;
    private bool _ignoreSelection;
    private bool _isSeeking;

    public PlayerViewModel(IMediaSessionService media, ISettingsStore settingsStore, AppSettings settings)
    {
        _media = media;
        _settingsStore = settingsStore;
        _settings = settings;
        Sources.Add(AutomaticSource);

        PreviousCommand = new AsyncRelayCommand(() => _media.SkipPreviousAsync(), () => CanPrevious);
        ToggleCommand = new AsyncRelayCommand(() => _media.TogglePlayPauseAsync(), () => CanToggle);
        NextCommand = new AsyncRelayCommand(() => _media.SkipNextAsync(), () => CanNext);
        SkipBackCommand = new AsyncRelayCommand(() => SkipAsync(-SkipInterval), () => CanSeek);
        SkipForwardCommand = new AsyncRelayCommand(() => SkipAsync(SkipInterval), () => CanSeek);

        _media.CurrentSessionChanged += OnCurrentSessionChanged;
        _media.SessionsChanged += OnSessionsChanged;
        _progressTimer = new DispatcherTimer(TimeSpan.FromMilliseconds(500), DispatcherPriority.Background, OnProgressTick, Application.Current.Dispatcher);
        _progressTimer.Start();
    }

    public event EventHandler<MediaSessionSnapshot?>? SessionAvailabilityChanged;
    public event EventHandler? SettingsChanged;

    public ObservableCollection<MediaSourceOption> Sources { get; } = [];
    public static MediaSourceOption AutomaticSource { get; } = new(string.Empty, string.Empty, "Automático (Windows)");

    public AsyncRelayCommand PreviousCommand { get; }
    public AsyncRelayCommand ToggleCommand { get; }
    public AsyncRelayCommand NextCommand { get; }
    public AsyncRelayCommand SkipBackCommand { get; }
    public AsyncRelayCommand SkipForwardCommand { get; }
    public static TimeSpan SkipInterval { get; } = TimeSpan.FromSeconds(10);

    public string Title => _snapshot?.Title ?? "Nada reproduciéndose";
    public string Artist => _snapshot is null ? "Abre música o video en tu navegador" :
        string.IsNullOrWhiteSpace(_snapshot.Artist) ? _snapshot.SourceDisplayName : _snapshot.Artist;
    public string PlayPauseGlyph => _snapshot?.IsPlaying == true ? "" : "";
    public string PlayPauseLabel => _snapshot?.IsPlaying == true ? "Pausar" : "Reproducir";
    public bool HasSession => _snapshot is not null;
    public bool CanPrevious => _snapshot?.Capabilities.CanPrevious == true;
    public bool CanNext => _snapshot?.Capabilities.CanNext == true;
    public bool CanToggle => _snapshot is not null && (_snapshot.IsPlaying ? _snapshot.Capabilities.CanPause : _snapshot.Capabilities.CanPlay);
    public bool CanSeek => _snapshot?.Capabilities.CanSeek == true && DurationSeconds > 0;
    public double DurationSeconds => Math.Max(0, _snapshot?.Duration.TotalSeconds ?? 0);
    public string PositionText => FormatTime(TimeSpan.FromSeconds(PositionSeconds));
    public string DurationText => FormatTime(_snapshot?.Duration ?? TimeSpan.Zero);
    public ImageSource? Artwork { get => _artwork; private set => SetProperty(ref _artwork, value); }
    public DisplayMode DisplayMode => _settings.DisplayMode;
    public bool StartWithWindows => _settings.StartWithWindows;

    public double PositionSeconds
    {
        get => _positionSeconds;
        set
        {
            if (SetProperty(ref _positionSeconds, Math.Clamp(value, 0, Math.Max(0, DurationSeconds))))
                OnPropertyChanged(nameof(PositionText));
        }
    }

    public MediaSourceOption? SelectedSource
    {
        get => _selectedSource;
        set
        {
            if (!SetProperty(ref _selectedSource, value) || _ignoreSelection)
                return;
            _ = SelectSourceAsync(value);
        }
    }

    public void BeginSeek() => _isSeeking = true;

    public async Task SeekAsync(double seconds)
    {
        if (!CanSeek)
        {
            _isSeeking = false;
            return;
        }

        var position = Math.Clamp(seconds, 0, DurationSeconds);
        PositionSeconds = position;

        try
        {
            var succeeded = await _media.SeekAsync(TimeSpan.FromSeconds(position));
            if (succeeded && _snapshot is not null)
            {
                _snapshot = _snapshot with
                {
                    Position = TimeSpan.FromSeconds(position),
                    TimelineUpdatedAt = DateTimeOffset.Now
                };
            }
            else if (_snapshot is not null)
            {
                PositionSeconds = ProgressEstimator.Estimate(_snapshot, DateTimeOffset.Now).TotalSeconds;
            }
        }
        finally
        {
            _isSeeking = false;
        }
    }

    private Task SkipAsync(TimeSpan delta)
    {
        BeginSeek();
        var target = ProgressEstimator.Offset(TimeSpan.FromSeconds(PositionSeconds), delta, TimeSpan.FromSeconds(DurationSeconds));
        return SeekAsync(target.TotalSeconds);
    }

    public async Task SetDisplayModeAsync(DisplayMode mode)
    {
        if (_settings.DisplayMode == mode) return;
        _settings = _settings with { DisplayMode = mode };
        OnPropertyChanged(nameof(DisplayMode));
        await SaveSettingsAsync();
        SettingsChanged?.Invoke(this, EventArgs.Empty);
    }

    public async Task SetStartWithWindowsAsync(bool enabled)
    {
        if (_settings.StartWithWindows == enabled) return;
        StartupService.SetEnabled(enabled);
        _settings = _settings with { StartWithWindows = enabled };
        OnPropertyChanged(nameof(StartWithWindows));
        await SaveSettingsAsync();
        SettingsChanged?.Invoke(this, EventArgs.Empty);
    }

    private async Task SelectSourceAsync(MediaSourceOption? source)
    {
        var sourceAppId = string.IsNullOrEmpty(source?.SessionId) ? null : source.SourceAppId;
        _settings = _settings with { PreferredSourceAppId = sourceAppId };
        await SaveSettingsAsync();
        await _media.SelectSessionAsync(string.IsNullOrEmpty(source?.SessionId) ? null : source.SessionId);
        SettingsChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnCurrentSessionChanged(object? sender, MediaSessionSnapshot? snapshot) => RunOnUi(() =>
    {
        _snapshot = snapshot;
        Artwork = ToImage(snapshot?.Artwork);
        if (!_isSeeking)
            PositionSeconds = ProgressEstimator.Estimate(snapshot ?? EmptySnapshot, DateTimeOffset.Now).TotalSeconds;
        NotifyPlayerProperties();
        SessionAvailabilityChanged?.Invoke(this, snapshot);
    });

    private void OnSessionsChanged(object? sender, IReadOnlyList<MediaSourceOption> sessions) => RunOnUi(() =>
    {
        _ignoreSelection = true;
        try
        {
            Sources.Clear();
            Sources.Add(AutomaticSource);
            foreach (var session in sessions)
                Sources.Add(session);

            SelectedSource = _settings.PreferredSourceAppId is { Length: > 0 } preferred
                ? Sources.FirstOrDefault(source => source.SourceAppId == preferred) ?? AutomaticSource
                : AutomaticSource;
        }
        finally { _ignoreSelection = false; }
    });

    private void OnProgressTick(object? sender, EventArgs e)
    {
        if (!_isSeeking && _snapshot is not null)
            PositionSeconds = ProgressEstimator.Estimate(_snapshot, DateTimeOffset.Now).TotalSeconds;
    }

    private void NotifyPlayerProperties()
    {
        foreach (var property in new[] { nameof(Title), nameof(Artist), nameof(PlayPauseGlyph), nameof(PlayPauseLabel), nameof(HasSession), nameof(CanPrevious), nameof(CanNext), nameof(CanToggle), nameof(CanSeek), nameof(DurationSeconds), nameof(DurationText) })
            OnPropertyChanged(property);
        PreviousCommand.RaiseCanExecuteChanged();
        ToggleCommand.RaiseCanExecuteChanged();
        NextCommand.RaiseCanExecuteChanged();
        SkipBackCommand.RaiseCanExecuteChanged();
        SkipForwardCommand.RaiseCanExecuteChanged();
    }

    private async Task SaveSettingsAsync()
    {
        try { await _settingsStore.SaveAsync(_settings); } catch { }
    }

    private static void RunOnUi(Action action)
    {
        var dispatcher = Application.Current.Dispatcher;
        if (dispatcher.CheckAccess()) action(); else dispatcher.BeginInvoke(action);
    }

    private static ImageSource? ToImage(byte[]? bytes)
    {
        if (bytes is null || bytes.Length == 0) return null;
        try
        {
            using var stream = new MemoryStream(bytes);
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.StreamSource = stream;
            image.EndInit();
            image.Freeze();
            return image;
        }
        catch { return null; }
    }

    private static string FormatTime(TimeSpan value) => value.TotalHours >= 1
        ? value.ToString(@"h\:mm\:ss", CultureInfo.InvariantCulture)
        : value.ToString(@"m\:ss", CultureInfo.InvariantCulture);

    public void Dispose()
    {
        _progressTimer.Stop();
        _media.CurrentSessionChanged -= OnCurrentSessionChanged;
        _media.SessionsChanged -= OnSessionsChanged;
    }

    private static MediaSessionSnapshot EmptySnapshot { get; } = new("", "", "", "", "", null,
        PlayerPlaybackStatus.Unknown, TimeSpan.Zero, TimeSpan.Zero, DateTimeOffset.Now,
        new PlaybackCapabilities(false, false, false, false, false));
}
