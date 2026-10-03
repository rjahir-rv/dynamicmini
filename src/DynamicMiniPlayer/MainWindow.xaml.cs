using DynamicMiniPlayer.Services;
using DynamicMiniPlayer.ViewModels;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using ComboBox = System.Windows.Controls.ComboBox;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;

namespace DynamicMiniPlayer;

public partial class MainWindow : Window
{
    private readonly IWindowPlacementService _placement;
    public PlayerViewModel ViewModel { get; }
    public event EventHandler? HideRequested;
    public event EventHandler? DismissRequested;

    public MainWindow(PlayerViewModel viewModel, IWindowPlacementService placement)
    {
        InitializeComponent();
        ViewModel = viewModel;
        _placement = placement;
        DataContext = viewModel;
        SourceInitialized += (_, _) => _placement.EnableRoundedCorners(this);
        Loaded += (_, _) => _placement.PlaceAboveTaskbar(this);
        Deactivated += OnDeactivated;
    }

    public void ShowAnchored()
    {
        var wasVisible = IsVisible;
        Show();
        _placement.PlaceAboveTaskbar(this);
        Activate();
        if (!wasVisible)
            PlayShowAnimation();
    }

    private void OnDeactivated(object? sender, EventArgs e)
    {
        if (IsVisible && !((App)Application.Current).IsExiting)
            DismissRequested?.Invoke(this, EventArgs.Empty);
    }

    private void PlayShowAnimation()
    {
        var duration = TimeSpan.FromMilliseconds(160);
        var easing = new CubicEase { EasingMode = EasingMode.EaseOut };
        RootBorder.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, duration) { EasingFunction = easing });
        RootTranslate.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(8, 0, duration) { EasingFunction = easing });
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            HideRequested?.Invoke(this, EventArgs.Empty);
            e.Handled = true;
            return;
        }

        // Let the source selector keep its own keyboard navigation.
        if (IsWithinComboBox(Keyboard.FocusedElement as DependencyObject))
            return;

        ICommand? command = e.Key switch
        {
            Key.Space => ViewModel.ToggleCommand,
            Key.Left => ViewModel.SkipBackCommand,
            Key.Right => ViewModel.SkipForwardCommand,
            _ => null
        };
        if (command is null)
            return;

        if (command.CanExecute(null))
            command.Execute(null);
        e.Handled = true;
    }

    private static bool IsWithinComboBox(DependencyObject? element)
    {
        for (var current = element; current is not null; current = VisualTreeHelper.GetParent(current))
        {
            if (current is ComboBox or ComboBoxItem)
                return true;
        }

        return false;
    }

    private void HideButton_Click(object sender, RoutedEventArgs e) => HideRequested?.Invoke(this, EventArgs.Empty);

    private void ProgressSlider_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not Slider slider || !slider.IsEnabled)
            return;

        ViewModel.BeginSeek();

        if (IsThumb(e.OriginalSource as DependencyObject))
            return;

        var pointer = e.GetPosition(slider);
        var fraction = slider.ActualWidth <= 0 ? 0 : Math.Clamp(pointer.X / slider.ActualWidth, 0, 1);
        if (slider.IsDirectionReversed)
            fraction = 1 - fraction;

        slider.Value = slider.Minimum + ((slider.Maximum - slider.Minimum) * fraction);
        slider.CaptureMouse();
        e.Handled = true;
    }

    private async void ProgressSlider_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is not Slider slider)
            return;

        var isDirectTrackClick = slider.IsMouseCaptured;
        if (isDirectTrackClick)
            slider.ReleaseMouseCapture();

        e.Handled = isDirectTrackClick;
        await ViewModel.SeekAsync(slider.Value);
    }

    private static bool IsThumb(DependencyObject? element)
    {
        for (var current = element; current is not null; current = VisualTreeHelper.GetParent(current))
        {
            if (current is Thumb)
                return true;
        }

        return false;
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (!((App)Application.Current).IsExiting)
        {
            e.Cancel = true;
            HideRequested?.Invoke(this, EventArgs.Empty);
        }
        base.OnClosing(e);
    }
}
