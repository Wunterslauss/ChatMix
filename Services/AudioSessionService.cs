using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using NAudio.CoreAudioApi;
using NAudio.CoreAudioApi.Interfaces;

namespace ChatMix.Services;

/// <summary>
/// Drives real Windows audio sessions on the default playback device via NAudio's CoreAudioApi
/// (WASAPI session volume control) - no virtual cable, no Voicemeeter. All members are expected
/// to be called from a single (UI) thread; the caller is responsible for driving <see cref="Poll"/>
/// periodically (e.g. from a DispatcherTimer) so newly created sessions get picked up live.
/// </summary>
public sealed class AudioSessionService : IDisposable
{
    private readonly MMDeviceEnumerator _enumerator = new();
    private MMDevice? _device;
    private List<string> _chatProcessNames;

    public int ChatVolumePercent { get; private set; }
    public int EverythingVolumePercent { get; private set; }
    public bool ChatMuted { get; private set; }
    public bool IsDucked => _preDuckVolume.HasValue;

    private int? _preDuckVolume;

    /// <summary>Fired after a hotkey-driven change to volume/mute/duck state (not fired by the background poll).</summary>
    public event Action? StateChanged;

    public AudioSessionService(List<string> chatProcessNames, int initialChatVolume, int initialEverythingVolume, bool initialChatMuted)
    {
        _chatProcessNames = Normalize(chatProcessNames);
        ChatVolumePercent = Math.Clamp(initialChatVolume, 0, 100);
        EverythingVolumePercent = Math.Clamp(initialEverythingVolume, 0, 100);
        ChatMuted = initialChatMuted;

        RefreshDevice();
        ApplyToAllSessions();
    }

    private static List<string> Normalize(IEnumerable<string> names) =>
        names.Select(n => n.Trim().ToLowerInvariant()).Where(n => n.Length > 0).ToList();

    public void UpdateChatProcessNames(List<string> names)
    {
        _chatProcessNames = Normalize(names);
        ApplyToAllSessions();
    }

    /// <summary>Call periodically (e.g. every 1-2s) to pick up new sessions (app relaunched, new game started) and re-sync levels.</summary>
    public void Poll()
    {
        try
        {
            var currentDefaultId = SafeGetDefaultDeviceId();
            if (_device == null || (currentDefaultId != null && !string.Equals(currentDefaultId, _device.ID, StringComparison.Ordinal)))
            {
                RefreshDevice();
            }

            _device?.AudioSessionManager?.RefreshSessions();
            ApplyToAllSessions();
        }
        catch
        {
            // Default device can be transiently unavailable (headset unplugged, etc). Just retry next tick.
        }
    }

    private void RefreshDevice()
    {
        try
        {
            _device?.Dispose();
            _device = _enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
        }
        catch
        {
            _device = null;
        }
    }

    private string? SafeGetDefaultDeviceId()
    {
        try
        {
            using var d = _enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
            return d.ID;
        }
        catch
        {
            return null;
        }
    }

    private void ApplyToAllSessions()
    {
        if (_device == null) return;

        try
        {
            var sessions = _device.AudioSessionManager?.Sessions;
            if (sessions == null) return;

            for (int i = 0; i < sessions.Count; i++)
            {
                try
                {
                    var session = sessions[i];
                    if (session.State == AudioSessionState.AudioSessionStateExpired) continue;

                    var processName = GetProcessName(session);
                    var vol = session.SimpleAudioVolume;
                    if (vol == null) continue;

                    bool isChat = processName != null && _chatProcessNames.Contains(processName);
                    if (isChat)
                    {
                        vol.Volume = ChatVolumePercent / 100f;
                        vol.Mute = ChatMuted;
                    }
                    else
                    {
                        vol.Volume = EverythingVolumePercent / 100f;
                        // Deliberately don't touch Mute here: don't fight a mute the user set by hand
                        // in the Windows volume mixer for some unrelated app.
                    }
                }
                catch
                {
                    // Session may have expired between enumeration and access; skip it.
                }
            }
        }
        catch
        {
            // Sessions collection briefly unavailable; next poll/hotkey press will retry.
        }
    }

    private static string? GetProcessName(AudioSessionControl session)
    {
        try
        {
            if (session.IsSystemSoundsSession) return null;
            uint pid = session.GetProcessID;
            if (pid == 0) return null;
            using var proc = Process.GetProcessById((int)pid);
            return (proc.ProcessName + ".exe").ToLowerInvariant();
        }
        catch
        {
            return null;
        }
    }

    public int AdjustChatVolume(int deltaPercent)
    {
        ChatVolumePercent = Math.Clamp(ChatVolumePercent + deltaPercent, 0, 100);
        if (deltaPercent > 0 && ChatVolumePercent > 0) ChatMuted = false;
        ApplyToAllSessions();
        StateChanged?.Invoke();
        return ChatVolumePercent;
    }

    public int AdjustEverythingVolume(int deltaPercent)
    {
        EverythingVolumePercent = Math.Clamp(EverythingVolumePercent + deltaPercent, 0, 100);
        ApplyToAllSessions();
        StateChanged?.Invoke();
        return EverythingVolumePercent;
    }

    public bool ToggleMuteChat()
    {
        ChatMuted = !ChatMuted;
        ApplyToAllSessions();
        StateChanged?.Invoke();
        return ChatMuted;
    }

    public int ToggleDuckChat(int duckPercent)
    {
        if (_preDuckVolume.HasValue)
        {
            ChatVolumePercent = _preDuckVolume.Value;
            _preDuckVolume = null;
        }
        else
        {
            _preDuckVolume = ChatVolumePercent;
            ChatVolumePercent = Math.Clamp(duckPercent, 0, 100);
        }

        ApplyToAllSessions();
        StateChanged?.Invoke();
        return ChatVolumePercent;
    }

    public void Dispose()
    {
        _device?.Dispose();
        _enumerator?.Dispose();
    }
}
