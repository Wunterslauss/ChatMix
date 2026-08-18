using System.Collections.Generic;

namespace ChatMix.Models;

public class AppSettings
{
    public List<string> ChatProcessNames { get; set; } = new() { "discord.exe" };
    public int StepPercent { get; set; } = 5;
    public int DuckPercent { get; set; } = 15;
    public bool StartWithWindows { get; set; } = false;

    // Persisted so volume/mute levels survive an app restart.
    public int LastChatVolumePercent { get; set; } = 100;
    public int LastEverythingVolumePercent { get; set; } = 100;
    public bool ChatMuted { get; set; } = false;

    public HotkeySettings Hotkeys { get; set; } = new();
}
