using System;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Threading;
using ChatMix.Models;
using ChatMix.Services;
using ChatMix.UI;

namespace ChatMix;

public partial class App : Application
{
    private const string SingleInstanceMutexName = "ChatMix-SingleInstance-8F2E1B7A-4B3E-4B2B-9C7B-6E9F0B1A2C3D";

    private Mutex? _mutex;
    private bool _ownsMutex;

    private SettingsService _settingsService = null!;
    private AudioSessionService _audioService = null!;
    private HotkeyService _hotkeyService = null!;
    private TrayIconManager _trayIconManager = null!;
    private OverlayWindow _overlay = null!;
    private DispatcherTimer _pollTimer = null!;
    private SettingsWindow? _settingsWindow;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _mutex = new Mutex(true, SingleInstanceMutexName, out var createdNew);
        _ownsMutex = createdNew;
        if (!createdNew)
        {
            MessageBox.Show("ChatMix is already running (check your system tray).", "ChatMix", MessageBoxButton.OK, MessageBoxImage.Information);
            Shutdown();
            return;
        }

        _settingsService = new SettingsService();
        var s = _settingsService.Settings;

        _audioService = new AudioSessionService(s.ChatProcessNames, s.LastChatVolumePercent, s.LastEverythingVolumePercent, s.ChatMuted);
        _audioService.StateChanged += PersistAudioState;

        _pollTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1.5) };
        _pollTimer.Tick += (_, __) => _audioService.Poll();
        _pollTimer.Start();

        _hotkeyService = new HotkeyService();
        _hotkeyService.HotkeyPressed += OnHotkeyPressed;
        var failed = _hotkeyService.RegisterAll(s.Hotkeys);

        _overlay = new OverlayWindow();

        _trayIconManager = new TrayIconManager(_audioService, _settingsService, OpenSettings, () => Shutdown());

        if (failed.Count > 0)
        {
            var names = string.Join(", ", failed.Select(a => a.ToFriendlyName()));
            _trayIconManager.ShowBalloon("ChatMix", $"Could not register hotkey(s) for: {names} (probably already in use by another app). Change them in Settings.");
        }
    }

    private void PersistAudioState()
    {
        var s = _settingsService.Settings;
        s.LastChatVolumePercent = _audioService.ChatVolumePercent;
        s.LastEverythingVolumePercent = _audioService.EverythingVolumePercent;
        s.ChatMuted = _audioService.ChatMuted;
        _settingsService.Save();
    }

    private void OnHotkeyPressed(HotkeyAction action)
    {
        var step = _settingsService.Settings.StepPercent;
        switch (action)
        {
            case HotkeyAction.ChatVolumeUp:
                ShowOverlay("Chat Volume", _audioService.AdjustChatVolume(step), _audioService.ChatMuted);
                break;
            case HotkeyAction.ChatVolumeDown:
                ShowOverlay("Chat Volume", _audioService.AdjustChatVolume(-step), _audioService.ChatMuted);
                break;
            case HotkeyAction.EverythingVolumeUp:
                ShowOverlay("Everything Else Volume", _audioService.AdjustEverythingVolume(step), false);
                break;
            case HotkeyAction.EverythingVolumeDown:
                ShowOverlay("Everything Else Volume", _audioService.AdjustEverythingVolume(-step), false);
                break;
            case HotkeyAction.ToggleMuteChat:
                var muted = _audioService.ToggleMuteChat();
                ShowOverlay("Chat Volume", _audioService.ChatVolumePercent, muted);
                break;
            case HotkeyAction.ToggleDuckChat:
                var vol = _audioService.ToggleDuckChat(_settingsService.Settings.DuckPercent);
                ShowOverlay(_audioService.IsDucked ? "Chat Ducked" : "Chat Restored", vol, _audioService.ChatMuted);
                break;
        }
    }

    private void ShowOverlay(string title, int percent, bool muted) => _overlay.ShowVolume(title, percent, muted);

    private void OpenSettings()
    {
        if (_settingsWindow != null)
        {
            _settingsWindow.Activate();
            return;
        }

        _settingsWindow = new SettingsWindow(_settingsService);
        _settingsWindow.SettingsSaved += ApplySettingsChanges;
        _settingsWindow.Closed += (_, __) => _settingsWindow = null;
        _settingsWindow.Show();
        _settingsWindow.Activate();
    }

    private void ApplySettingsChanges()
    {
        var s = _settingsService.Settings;
        _audioService.UpdateChatProcessNames(s.ChatProcessNames);

        var failed = _hotkeyService.RegisterAll(s.Hotkeys);
        if (failed.Count > 0)
        {
            var names = string.Join(", ", failed.Select(a => a.ToFriendlyName()));
            MessageBox.Show($"Could not register hotkey(s) for: {names}.\nThey may already be in use by another application.", "ChatMix", MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        StartupService.SetStartWithWindows(s.StartWithWindows);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _pollTimer?.Stop();
        _hotkeyService?.Dispose();
        _audioService?.Dispose();
        _trayIconManager?.Dispose();

        if (_ownsMutex) _mutex?.ReleaseMutex();
        _mutex?.Dispose();

        base.OnExit(e);
    }
}
