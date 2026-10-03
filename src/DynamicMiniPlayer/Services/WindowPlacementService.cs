using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace DynamicMiniPlayer.Services;

public sealed class WindowPlacementService : IWindowPlacementService
{
    private const int DwmaWindowCornerPreference = 33;
    private const int RoundPreference = 2;
    private const uint AbmGetState = 0x00000004;
    private const uint AbmGetTaskbarPos = 0x00000005;
    private const uint AbmGetAutoHideBarEx = 0x0000000B;
    private const uint AbsAutoHide = 0x00000001;
    private const uint AbeBottom = 3;
    private const double HorizontalMargin = 12;
    private const double BottomMargin = 12;
    private const double MaximumTaskbarHeight = 96;

    public void PlaceAboveTaskbar(Window window)
    {
        if (!GetCursorPos(out var cursor))
            cursor = new PointNative(0, 0);

        var monitor = MonitorFromPoint(cursor, 2);
        var info = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
        if (monitor == IntPtr.Zero || !GetMonitorInfo(monitor, ref info))
            return;

        var hwnd = new WindowInteropHelper(window).Handle;
        var dpi = hwnd == IntPtr.Zero ? 96u : GetDpiForWindow(hwnd);
        if (dpi == 0) dpi = 96;
        var scale = dpi / 96d;
        var horizontalMargin = (int)Math.Round(HorizontalMargin * scale);
        var bottomMargin = (int)Math.Round(BottomMargin * scale);
        var taskbarHeight = GetAutoHideTaskbarHeight(info, scale);
        var x = info.WorkArea.Right - (int)Math.Round(window.Width * scale) - horizontalMargin;
        var y = info.WorkArea.Bottom - (int)Math.Round(window.Height * scale) - bottomMargin - taskbarHeight;
        window.Left = x / scale;
        window.Top = y / scale;
    }

    private static int GetAutoHideTaskbarHeight(MonitorInfo monitorInfo, double scale)
    {
        var appBar = new AppBarData
        {
            Size = (uint)Marshal.SizeOf<AppBarData>(),
            Edge = AbeBottom,
            Rectangle = monitorInfo.Monitor
        };
        var taskbar = SHAppBarMessage(AbmGetAutoHideBarEx, ref appBar);
        var maximumHeight = (int)Math.Round(MaximumTaskbarHeight * scale);
        if (taskbar != UIntPtr.Zero &&
            GetWindowRect(new IntPtr(unchecked((long)taskbar.ToUInt64())), out var taskbarRectangle))
        {
            var height = taskbarRectangle.Bottom - taskbarRectangle.Top;
            if (height > 0 && height <= maximumHeight)
                return height;
        }

        appBar = new AppBarData { Size = (uint)Marshal.SizeOf<AppBarData>() };
        var state = SHAppBarMessage(AbmGetState, ref appBar).ToUInt64();
        if ((state & AbsAutoHide) == 0)
            return 0;

        appBar = new AppBarData { Size = (uint)Marshal.SizeOf<AppBarData>() };
        if (SHAppBarMessage(AbmGetTaskbarPos, ref appBar) != UIntPtr.Zero)
        {
            if (appBar.Edge != AbeBottom)
                return 0;

            if (appBar.Rectangle.Left < monitorInfo.Monitor.Right &&
                appBar.Rectangle.Right > monitorInfo.Monitor.Left)
            {
                var height = appBar.Rectangle.Bottom - appBar.Rectangle.Top;
                if (height > 0 && height <= maximumHeight)
                    return height;
            }
        }

        return (int)Math.Round(48 * scale);
    }

    public void EnableRoundedCorners(Window window)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd == IntPtr.Zero)
            return;
        var preference = RoundPreference;
        _ = DwmSetWindowAttribute(hwnd, DwmaWindowCornerPreference, ref preference, sizeof(int));
    }

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out PointNative point);

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromPoint(PointNative point, uint flags);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo info);

    [DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(IntPtr hwnd);

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hwnd, out RectNative rectangle);

    [DllImport("shell32.dll")]
    private static extern UIntPtr SHAppBarMessage(uint message, ref AppBarData data);

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct PointNative(int x, int y)
    {
        public readonly int X = x;
        public readonly int Y = y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RectNative
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct AppBarData
    {
        public uint Size;
        public IntPtr Window;
        public uint CallbackMessage;
        public uint Edge;
        public RectNative Rectangle;
        public IntPtr Parameter;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct MonitorInfo
    {
        public int Size;
        public RectNative Monitor;
        public RectNative WorkArea;
        public uint Flags;
    }
}
