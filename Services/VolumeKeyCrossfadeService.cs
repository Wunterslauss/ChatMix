using System;
using System.Runtime.InteropServices;

namespace ChatMix.Services;

/// <summary>
/// Repurposes the standard system Volume Up/Down keys - what a keyboard's dedicated volume
/// wheel/rocker sends by default (e.g. the Razer DeathStalker V2 Pro's scroll wheel) - into a
/// Chat/Everything crossfade instead of the system volume. Uses a low-level keyboard hook so the
/// key press can be suppressed: without that, Windows would still separately change the system's
/// master volume and show its own OSD alongside whatever ChatMix does.
/// </summary>
public sealed class VolumeKeyCrossfadeService : IDisposable
{
    private readonly AudioSessionService _audio;
    private readonly SettingsService _settings;
    private IntPtr _hookHandle = IntPtr.Zero;
    private NativeMethods.LowLevelKeyboardProc? _hookProc; // keep alive - this is a native callback pointer

    /// <summary>Fired after a crossfade step with the resulting (chatPercent, everythingPercent).</summary>
    public event Action<int, int>? BalanceChanged;

    public VolumeKeyCrossfadeService(AudioSessionService audio, SettingsService settings)
    {
        _audio = audio;
        _settings = settings;
    }

    public bool IsEnabled => _hookHandle != IntPtr.Zero;

    public void SetEnabled(bool enabled)
    {
        if (enabled == IsEnabled) return;
        if (enabled) Install(); else Uninstall();
    }

    private void Install()
    {
        _hookProc = HookCallback;
        _hookHandle = NativeMethods.SetWindowsHookEx(
            NativeMethods.WH_KEYBOARD_LL, _hookProc, NativeMethods.GetModuleHandle(null), 0);
        if (_hookHandle == IntPtr.Zero) _hookProc = null; // installation failed - nothing to keep alive
    }

    private void Uninstall()
    {
        if (_hookHandle != IntPtr.Zero)
        {
            NativeMethods.UnhookWindowsHookEx(_hookHandle);
            _hookHandle = IntPtr.Zero;
        }
        _hookProc = null;
    }

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 && (wParam == (IntPtr)NativeMethods.WM_KEYDOWN || wParam == (IntPtr)NativeMethods.WM_SYSKEYDOWN))
        {
            var data = Marshal.PtrToStructure<NativeMethods.KBDLLHOOKSTRUCT>(lParam);
            int step = Math.Max(1, _settings.Settings.StepPercent);

            if (data.vkCode == NativeMethods.VK_VOLUME_DOWN)
            {
                var (chat, everything) = _audio.Crossfade(step); // scroll down -> more chat
                BalanceChanged?.Invoke(chat, everything);
                return (IntPtr)1; // swallow: don't also change system volume / show its OSD
            }
            if (data.vkCode == NativeMethods.VK_VOLUME_UP)
            {
                var (chat, everything) = _audio.Crossfade(-step); // scroll up -> more everything
                BalanceChanged?.Invoke(chat, everything);
                return (IntPtr)1;
            }
        }

        return NativeMethods.CallNextHookEx(_hookHandle, nCode, wParam, lParam);
    }

    public void Dispose() => Uninstall();
}
