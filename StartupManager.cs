using System.Diagnostics;
using Microsoft.Win32;

namespace Blink;

/// <summary>
/// Manages the "start with Windows" entry via the per-user Run registry key.
/// Per-user (HKCU) means no admin rights are required.
/// </summary>
public static class StartupManager
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "Blink";

    private static string ExecutablePath =>
        Environment.ProcessPath ?? Process.GetCurrentProcess().MainModule?.FileName ?? "";

    public static bool IsEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath);
            return key?.GetValue(ValueName) is not null;
        }
        catch
        {
            return false;
        }
    }

    public static void SetEnabled(bool enabled)
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath);
            if (key is null)
                return;

            if (enabled)
            {
                var path = ExecutablePath;
                if (!string.IsNullOrEmpty(path))
                    key.SetValue(ValueName, $"\"{path}\"");
            }
            else
            {
                key.DeleteValue(ValueName, throwOnMissingValue: false);
            }
        }
        catch
        {
            // Non-fatal: registry may be locked down. The app still works, it just
            // won't auto-start.
        }
    }
}
