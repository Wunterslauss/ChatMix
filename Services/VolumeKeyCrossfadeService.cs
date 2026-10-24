using System;
using System.Runtime.InteropServices;
using System.Windows.Threading;

namespace ChatMix.Services;

/// <summary>
/// Repurposes the standard system Volume Up/Down keys - what a keyboard's dedicated volume
/// wheel/rocker sends by default (e.g. the Razer DeathStalker V2 Pro's scroll wheel) - into a
/// Chat/Everything crossfade instead of the system volume. Uses a low-level keyboard hook so the
/// key press can be suppressed: without that, Windows would still separately change the system's
/// master volume and show its own OSD alongside whatever ChatMix does.
///
/// Holding Ctrl while scrolling is a pass-through escape hatch: the key isn't intercepted at all,
/// so Windows handles it exactly as it normally would (real system volume + its own OSD) - since
/// enabling this feature otherwise means the wheel can no longer adjust real volume at all.
///
/// The hook callback runs inside the system-wide keyboard input chain, so it must return fast. A
/// single wheel flick fires 4-9 key events ~35-60ms apart, and applying each one means walking
/// every audio session over COM (~28ms), which would keep the UI thread - and keyboard input
/// system-wide - busy for most of the flick. So the hook only records the step; a throttled timer
/// applies the accumulated total in one pass.
/// </summary>
public sealed class VolumeKeyCrossfadeService : IDisposable
{
    /// <summary>Window over which wheel events are merged into one crossfade. Events arrive
    /// ~35-60ms apart, so it has to be well above that for merging to happen at all.</summary>
    private const int CoalesceWindowMs = 100;

    private readonly AudioSessionService _audio;
    private readonly SettingsService _settings;
    private readonly DispatcherTimer _applyTimer;
    private IntPtr _hookHandle = IntPtr.Zero;
    private NativeMethods.LowLevelKeyboardProc? _hookProc; // keep alive - this is a native callback pointer
    private int _pendingSteps; // + = toward Chat, - = toward Everything

    /// <summary>Fired after a crossfade is applied with the resulting (chatPercent, everythingPercent).</summary>
    public event Action<int, int>? BalanceChanged;

    public VolumeKeyCrossfadeService(AudioSessionService audio, SettingsService settings)
    {
        _audio = audio;
        _settings = settings;

        // Created here, on the UI thread, so Tick fires on the same thread that owns the audio
        // service and overlay - no locking needed around _pendingSteps.
        _applyTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(CoalesceWindowMs) };
        _applyTimer.Tick += (_, __) => ApplyPending();
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

        // Disabling the feature discards anything not yet applied.
        _applyTimer.Stop();
        _pendingSteps = 0;
    }

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 && (wParam == (IntPtr)NativeMethods.WM_KEYDOWN || wParam == (IntPtr)NativeMethods.WM_SYSKEYDOWN))
        {
            var data = Marshal.PtrToStructure<NativeMethods.KBDLLHOOKSTRUCT>(lParam);
            bool isVolumeKey = data.vkCode == NativeMethods.VK_VOLUME_DOWN || data.vkCode == NativeMethods.VK_VOLUME_UP;

            if (isVolumeKey && (NativeMethods.GetAsyncKeyState(NativeMethods.VK_CONTROL) & 0x8000) != 0)
            {
                // Escape hatch: let this one press through untouched for real system volume.
                return NativeMethods.CallNextHookEx(_hookHandle, nCode, wParam, lParam);
            }

            if (isVolumeKey)
            {
                QueueStep(data.vkCode == NativeMethods.VK_VOLUME_DOWN ? +1 : -1); // scroll down -> more chat
                return (IntPtr)1; // swallow: don't also change system volume / show its OSD
            }
        }

        return NativeMethods.CallNextHookEx(_hookHandle, nCode, wParam, lParam);
    }

    /// <summary>Records one wheel step and returns immediately; the audio work happens in
    /// <see cref="ApplyPending"/>. Cheap enough to run inside the keyboard hook.</summary>
    internal void QueueStep(int direction)
    {
        _pendingSteps += direction;
        // Throttle, not debounce: the timer is not restarted by later events, so a long continuous
        // scroll still gets feedback every ~100ms instead of only after you stop.
        if (!_applyTimer.IsEnabled) _applyTimer.Start();
    }

    private void ApplyPending()
    {
        _applyTimer.Stop();
        int steps = _pendingSteps;
        _pendingSteps = 0;
        if (steps == 0) return; // e.g. scrolled down and back up within one window

        // This runs from a dispatcher tick, not the hook, so an exception here would reach the
        // app's unhandled-exception path and kill the process - contain it.
        try
        {
            int step = Math.Max(1, _settings.Settings.StepPercent);
            var (chat, everything) = _audio.Crossfade(steps * step);
            BalanceChanged?.Invoke(chat, everything);
        }
        catch
        {
            // Audio device briefly unavailable etc. - the next scroll simply tries again.
        }
    }

    public void Dispose() => Uninstall();
}
