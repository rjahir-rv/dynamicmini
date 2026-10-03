using Microsoft.Win32;
using System.Diagnostics;
using System.Reflection;

namespace DynamicMiniPlayer.Services;

public static class StartupService
{
    private const string RegistryPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "DynamicMiniPlayer";

    public static void SetEnabled(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RegistryPath);
        if (!enabled)
        {
            key.DeleteValue(ValueName, false);
            return;
        }

        key.SetValue(ValueName, BuildCommand());
    }

    private static string BuildCommand()
    {
        var processPath = Environment.ProcessPath ?? Process.GetCurrentProcess().MainModule?.FileName ?? "DynamicMiniPlayer.exe";
        var assemblyPath = Assembly.GetExecutingAssembly().Location;
        return Path.GetFileNameWithoutExtension(processPath).Equals("dotnet", StringComparison.OrdinalIgnoreCase)
            ? $"\"{processPath}\" \"{assemblyPath}\" --autostart"
            : $"\"{processPath}\" --autostart";
    }
}
