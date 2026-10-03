using DynamicMiniPlayer.Models;
using DynamicMiniPlayer.ViewModels;
using System.ComponentModel;
using System.Windows;

namespace DynamicMiniPlayer;

public partial class SettingsWindow : Window, INotifyPropertyChanged
{
    private readonly PlayerViewModel _viewModel;

    public SettingsWindow(PlayerViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = this;
    }

    public bool IsPersistent => _viewModel.DisplayMode == DisplayMode.Persistent;
    public bool StartWithWindows => _viewModel.StartWithWindows;
    public event PropertyChangedEventHandler? PropertyChanged;

    private async void PersistentCheckBox_Click(object sender, RoutedEventArgs e)
    {
        await _viewModel.SetDisplayModeAsync(PersistentCheckBox.IsChecked == true ? DisplayMode.Persistent : DisplayMode.OnDemand);
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsPersistent)));
    }

    private async void StartupCheckBox_Click(object sender, RoutedEventArgs e)
    {
        await _viewModel.SetStartWithWindowsAsync(!_viewModel.StartWithWindows);
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(StartWithWindows)));
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();
}
