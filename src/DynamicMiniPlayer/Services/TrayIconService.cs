using DynamicMiniPlayer.Models;
using DynamicMiniPlayer.ViewModels;
using Drawing = System.Drawing;
using Forms = System.Windows.Forms;

namespace DynamicMiniPlayer.Services;

public sealed class TrayIconService : IDisposable
{
    private readonly PlayerViewModel _viewModel;
    private readonly Action _toggleWindow;
    private readonly Action _openSettings;
    private readonly Action _exit;
    private readonly Forms.NotifyIcon _icon;
    private readonly Forms.ContextMenuStrip _menu;
    private readonly Drawing.Icon _appIcon;

    public TrayIconService(PlayerViewModel viewModel, Action toggleWindow, Action openSettings, Action exit)
    {
        _viewModel = viewModel;
        _toggleWindow = toggleWindow;
        _openSettings = openSettings;
        _exit = exit;
        var resource = Application.GetResourceStream(new Uri("pack://application:,,,/Assets/DynamicMiniPlayer.ico"));
        _appIcon = resource is null ? (Drawing.Icon)Drawing.SystemIcons.Application.Clone() : new Drawing.Icon(resource.Stream);
        _menu = new Forms.ContextMenuStrip();
        _menu.Opening += (_, _) => RebuildMenu();
        _icon = new Forms.NotifyIcon
        {
            Text = "Dynamic Mini Player",
            Icon = _appIcon,
            Visible = true,
            ContextMenuStrip = _menu
        };
        _icon.MouseClick += (_, args) =>
        {
            if (args.Button == Forms.MouseButtons.Left)
                _toggleWindow();
        };
    }

    private void RebuildMenu()
    {
        _menu.Items.Clear();
        _menu.Items.Add("Mostrar / ocultar", null, (_, _) => _toggleWindow());
        _menu.Items.Add(new Forms.ToolStripSeparator());

        var mode = new Forms.ToolStripMenuItem("Modo");
        mode.DropDownItems.Add(CreateCheckedItem("Persistente", _viewModel.DisplayMode == DisplayMode.Persistent,
            async () => await _viewModel.SetDisplayModeAsync(DisplayMode.Persistent)));
        mode.DropDownItems.Add(CreateCheckedItem("Bajo demanda", _viewModel.DisplayMode == DisplayMode.OnDemand,
            async () => await _viewModel.SetDisplayModeAsync(DisplayMode.OnDemand)));
        _menu.Items.Add(mode);

        var sources = new Forms.ToolStripMenuItem("Fuente multimedia");
        foreach (var source in _viewModel.Sources)
        {
            var captured = source;
            var selected = _viewModel.SelectedSource?.SessionId == source.SessionId;
            sources.DropDownItems.Add(CreateCheckedItem(source.DisplayName, selected, () => _viewModel.SelectedSource = captured));
        }
        sources.Enabled = _viewModel.Sources.Count > 0;
        _menu.Items.Add(sources);

        _menu.Items.Add(CreateCheckedItem("Ejecutar al iniciar Windows", _viewModel.StartWithWindows,
            async () => await _viewModel.SetStartWithWindowsAsync(!_viewModel.StartWithWindows)));
        _menu.Items.Add("Ajustes", null, (_, _) => _openSettings());
        _menu.Items.Add(new Forms.ToolStripSeparator());
        _menu.Items.Add("Salir", null, (_, _) => _exit());
    }

    private static Forms.ToolStripMenuItem CreateCheckedItem(string text, bool isChecked, Action action)
    {
        var item = new Forms.ToolStripMenuItem(text) { Checked = isChecked };
        item.Click += (_, _) => action();
        return item;
    }

    public void Dispose()
    {
        _icon.Visible = false;
        _icon.Dispose();
        _menu.Dispose();
        _appIcon.Dispose();
    }
}
