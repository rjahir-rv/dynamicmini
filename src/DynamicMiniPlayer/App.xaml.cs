using DynamicMiniPlayer.Models;
using DynamicMiniPlayer.Services;
using DynamicMiniPlayer.ViewModels;
using System.Windows;
using System.Windows.Threading;

namespace DynamicMiniPlayer;

public partial class App : System.Windows.Application
{
    private SingleInstanceCoordinator? _singleInstance;
    private WindowsMediaSessionService? _mediaService;
    private PlayerViewModel? _viewModel;
    private MainWindow? _playerWindow;
    private SettingsWindow? _settingsWindow;
    private TrayIconService? _trayIcon;
    private DispatcherTimer? _idleTimer;
    private readonly PlayerVisibilityState _visibilityState = new();

    public bool IsExiting { get; private set; }

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        _singleInstance = SingleInstanceCoordinator.Acquire();
        if (!_singleInstance.IsPrimary)
        {
            Shutdown();
            return;
        }

        try
        {
            var settingsStore = new JsonSettingsStore();
            var settings = await settingsStore.LoadAsync();
            StartupService.SetEnabled(settings.StartWithWindows);

            _mediaService = new WindowsMediaSessionService();
            _viewModel = new PlayerViewModel(_mediaService, settingsStore, settings);
            _playerWindow = new MainWindow(_viewModel, new WindowPlacementService());
            _playerWindow.HideRequested += (_, _) => HidePlayerWindow(manual: true);
            _playerWindow.DismissRequested += (_, _) => DismissPlayerWindow();
            MainWindow = _playerWindow;
            _trayIcon = new TrayIconService(_viewModel, TogglePlayerWindow, OpenSettings, ExitApplication);
            _idleTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
            _idleTimer.Tick += OnIdleTimerTick;

            _viewModel.SessionAvailabilityChanged += OnSessionAvailabilityChanged;
            _viewModel.SettingsChanged += OnSettingsChanged;
            _singleInstance.Listen(Dispatcher, TogglePlayerWindow);

            var autoStarted = e.Args.Any(arg => arg.Equals("--autostart", StringComparison.OrdinalIgnoreCase));
            if (!autoStarted)
                _playerWindow.ShowAnchored();

            await _mediaService.InitializeAsync();
        }
        catch (Exception exception)
        {
            MessageBox.Show($"No fue posible iniciar Dynamic Mini Player.\n\n{exception.Message}",
                "Dynamic Mini Player", MessageBoxButton.OK, MessageBoxImage.Error);
            ExitApplication();
        }
    }

    private void OnSessionAvailabilityChanged(object? sender, MediaSessionSnapshot? snapshot)
    {
        if (_playerWindow is null || _viewModel is null)
            return;

        if (PlayerVisibilityState.IsInactive(snapshot))
        {
            _idleTimer?.Stop();
            _idleTimer?.Start();
        }
        else
        {
            _idleTimer?.Stop();
            if (_visibilityState.ShouldShow(snapshot!, _viewModel.DisplayMode))
                _playerWindow.ShowAnchored();
        }
    }

    private void OnSettingsChanged(object? sender, EventArgs e)
    {
        if (_viewModel?.DisplayMode == DisplayMode.Persistent && _viewModel.HasSession == true)
        {
            _visibilityState.AllowShow();
            _playerWindow?.ShowAnchored();
        }
    }

    private void OnIdleTimerTick(object? sender, EventArgs e)
    {
        _idleTimer?.Stop();
        HidePlayerWindow(manual: false);
    }

    private void TogglePlayerWindow()
    {
        if (_playerWindow is null) return;
        if (_playerWindow.IsVisible)
            HidePlayerWindow(manual: true);
        else if (!_visibilityState.ShouldIgnoreToggle(DateTimeOffset.Now))
        {
            _visibilityState.AllowShow();
            _playerWindow.ShowAnchored();
        }
    }

    private void HidePlayerWindow(bool manual)
    {
        _visibilityState.RecordHide(manual);
        _playerWindow?.Hide();
    }

    private void DismissPlayerWindow()
    {
        _visibilityState.RecordDismiss(DateTimeOffset.Now);
        _playerWindow?.Hide();
    }

    private void OpenSettings()
    {
        if (_viewModel is null) return;
        if (_settingsWindow is { IsVisible: true })
        {
            _settingsWindow.Activate();
            return;
        }

        _settingsWindow = new SettingsWindow(_viewModel);
        _settingsWindow.Closed += (_, _) => _settingsWindow = null;
        _settingsWindow.Show();
        _settingsWindow.Activate();
    }

    private async void ExitApplication()
    {
        if (IsExiting) return;
        IsExiting = true;
        _idleTimer?.Stop();
        _trayIcon?.Dispose();
        _viewModel?.Dispose();
        if (_mediaService is not null)
            await _mediaService.DisposeAsync();
        _singleInstance?.Dispose();
        _settingsWindow?.Close();
        _playerWindow?.Close();
        Shutdown();
    }
}
