using System;
using System.Diagnostics;
using Microsoft.Win32;

namespace ChatMix.Services;

/// <summary>Adds/removes a per-user "Run at login" entry. No admin rights required (HKCU only).</summary>
public static class StartupService
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "ChatMix";

    public static void SetStartWithWindows(bool enabled)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true);
            if (key == null) return;

            if (enabled)
            {
                var exePath = Environment.ProcessPath ?? Process.GetCurrentProcess().MainModule?.FileName;
                if (!string.IsNullOrEmpty(exePath))
                    key.SetValue(ValueName, $"\"{exePath}\"");
            }
            else
            {
                if (key.GetValue(ValueName) != null)
                    key.DeleteValue(ValueName, throwOnMissingValue: false);
            }
        }
        catch
        {
            // Registry access can be blocked by Group Policy or AV/EDR software. Don't crash the
            // app over a "Start with Windows" toggle - just leave the setting unapplied.
        }
    }
}
