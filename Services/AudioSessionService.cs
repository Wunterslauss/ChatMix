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
    private readonly Dictionary<uint, string?> _processNameCache = new();
    private readonly Dictionary<uint, (float Volume, bool Mute)> _lastApplied = new();

    public int ChatVolumePercent { get; private set; }
    public int EverythingVolumePercent { get; private set; }
    public bool ChatMuted { get; private set; }
    public bool IsDucked => _preDuckVolume.HasValue;

    /// <summary>Set when a poll tick fails (e.g. default device transiently unavailable); cleared on
    /// the next successful poll. TrayIconManager surfaces this so a degraded state isn't silent.</summary>
    public string? LastPollError { get; private set; }

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
        // A session's chat/everything-else classification can flip, so a cached "already applied
        // this value" entry could wrongly skip re-applying it under its new classification.
        _lastApplied.Clear();
        ApplyToAllSessions();
    }

    /// <summary>Call periodically (e.g. every 1-2s) to pick up new sessions (app relaunched, new game started) and re-sync levels.</summary>
    public void Poll()
    {
        try
        {
            var currentDefaultId = SafeGetDefaultDeviceId();
            // Refresh whenever we can't positively confirm _device is still the current default -
            // including when the default is momentarily unreadable - rather than only on a known
            // ID change, so a disconnected/stale _device doesn't linger silently across ticks.
            if (_device == null || currentDefaultId == null || !string.Equals(currentDefaultId, _device.ID, StringComparison.Ordinal))
            {
                RefreshDevice();
            }

            _device?.AudioSessionManager?.RefreshSessions();
            ApplyToAllSessions();
            LastPollError = null;
        }
        catch (Exception ex)
        {
            // Default device can be transiently unavailable (headset unplugged, etc). Surface it
            // instead of failing silently - TrayIconManager shows LastPollError - and retry next tick.
            LastPollError = ex.Message;
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

                    var pid = GetSessionProcessId(session);
                    var processName = pid.HasValue ? GetProcessName(pid.Value) : null;
                    var vol = session.SimpleAudioVolume;
                    if (vol == null) continue;

                    bool isChat = processName != null && _chatProcessNames.Contains(processName);
                    float targetVolume = (isChat ? ChatVolumePercent : EverythingVolumePercent) / 100f;
                    // Mute is only ever driven for chat sessions - never touch it for "everything
                    // else" so we don't fight a mute the user set by hand in the Windows mixer.
                    bool targetMute = isChat && ChatMuted;

                    // Skip the COM write entirely when it would be a no-op - every poll tick
                    // otherwise rewrites every session's volume/mute regardless of whether anything
                    // changed since the last tick.
                    if (pid.HasValue && _lastApplied.TryGetValue(pid.Value, out var last)
                        && last.Volume == targetVolume && (!isChat || last.Mute == targetMute))
                    {
                        continue;
                    }

                    vol.Volume = targetVolume;
                    if (isChat) vol.Mute = targetMute;
                    if (pid.HasValue) _lastApplied[pid.Value] = (targetVolume, targetMute);
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

    private static uint? GetSessionProcessId(AudioSessionControl session)
    {
        try
        {
            if (session.IsSystemSoundsSession) return null;
            uint pid = session.GetProcessID;
            return pid == 0 ? null : pid;
        }
        catch
        {
            return null;
        }
    }

    private string? GetProcessName(uint pid)
    {
        if (_processNameCache.TryGetValue(pid, out var cached)) return cached;

        string? name;
        try
        {
            using var proc = Process.GetProcessById((int)pid);
            name = (proc.ProcessName + ".exe").ToLowerInvariant();
        }
        catch
        {
            name = null;
        }
        _processNameCache[pid] = name;
        return name;
    }

    public int AdjustChatVolume(int deltaPercent)
    {
        // A manual nudge while ducked means the user took control - drop the pending duck-restore
        // so un-ducking later doesn't silently snap back over this and discard it.
        _preDuckVolume = null;
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
