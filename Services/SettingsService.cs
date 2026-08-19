using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using ChatMix.Models;

namespace ChatMix.Services;

public class SettingsService
{
    private readonly string _path;

    public AppSettings Settings { get; private set; }

    public SettingsService()
    {
        var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ChatMix");
        Directory.CreateDirectory(dir);
        _path = Path.Combine(dir, "settings.json");
        Settings = Load();
    }

    private AppSettings Load()
    {
        try
        {
            if (File.Exists(_path))
            {
                var json = File.ReadAllText(_path);
                var loaded = JsonSerializer.Deserialize<AppSettings>(json);
                if (loaded != null) return Sanitize(loaded);
            }
        }
        catch
        {
            // Corrupt or unreadable config: fall back to defaults rather than crashing at startup.
        }
        return new AppSettings();
    }

    /// <summary>settings.json is a plain user-editable file - repair anything a hand-edit (or a
    /// truncated write) could leave null/out-of-range instead of letting it NRE deep inside
    /// startup, or silently disable hotkeys/chat detection with no error shown.</summary>
    private static AppSettings Sanitize(AppSettings s)
    {
        var names = (s.ChatProcessNames ?? new())
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .ToList();
        s.ChatProcessNames = names.Count > 0 ? names : new() { "discord.exe" };

        s.StepPercent = Math.Clamp(s.StepPercent <= 0 ? 5 : s.StepPercent, 1, 50);
        s.DuckPercent = Math.Clamp(s.DuckPercent, 0, 100);

        s.Hotkeys ??= new();
        s.Hotkeys.ChatVolumeUp ??= new(false, false, false, false, "None");
        s.Hotkeys.ChatVolumeDown ??= new(false, false, false, false, "None");
        s.Hotkeys.EverythingVolumeUp ??= new(false, false, false, false, "None");
        s.Hotkeys.EverythingVolumeDown ??= new(false, false, false, false, "None");
        s.Hotkeys.ToggleMuteChat ??= new(false, false, false, false, "None");
        s.Hotkeys.ToggleDuckChat ??= new(false, false, false, false, "None");
        s.Hotkeys.ToggleMuteEverything ??= new(false, false, false, false, "None");
        s.Hotkeys.ToggleDuckEverything ??= new(false, false, false, false, "None");
        s.Hotkeys.ChatVolumeUp.Key ??= "None";
        s.Hotkeys.ChatVolumeDown.Key ??= "None";
        s.Hotkeys.EverythingVolumeUp.Key ??= "None";
        s.Hotkeys.EverythingVolumeDown.Key ??= "None";
        s.Hotkeys.ToggleMuteChat.Key ??= "None";
        s.Hotkeys.ToggleDuckChat.Key ??= "None";
        s.Hotkeys.ToggleMuteEverything.Key ??= "None";
        s.Hotkeys.ToggleDuckEverything.Key ??= "None";

        return s;
    }

    public void Save()
    {
        try
        {
            var json = JsonSerializer.Serialize(Settings, new JsonSerializerOptions { WriteIndented = true });
            // Write to a temp file and swap it in, so a crash/power-loss mid-write can never leave
            // settings.json truncated - Load() would otherwise treat that as corrupt and reset
            // every setting (hotkeys, chat process list, everything) back to defaults.
            var tempPath = _path + ".tmp";
            File.WriteAllText(tempPath, json);
            File.Move(tempPath, _path, overwrite: true);
        }
        catch
        {
            // Transient I/O failure (locked by AV/cloud-sync, disk full, etc). Don't crash the app
            // over a settings write - the in-memory state is unaffected and the next successful
            // save will persist it.
        }
    }
}
