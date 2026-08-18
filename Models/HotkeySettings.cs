namespace ChatMix.Models;

/// <summary>
/// Defaults use F13-F18 - unlabeled keys that don't exist on physical keyboards and are never
/// bound by other apps, which is exactly why they're a popular choice for Stream Deck "Hotkey" actions.
/// </summary>
public class HotkeySettings
{
    public HotkeyBinding ChatVolumeUp { get; set; } = new(false, false, false, false, "F13");
    public HotkeyBinding ChatVolumeDown { get; set; } = new(false, false, false, false, "F14");
    public HotkeyBinding EverythingVolumeUp { get; set; } = new(false, false, false, false, "F15");
    public HotkeyBinding EverythingVolumeDown { get; set; } = new(false, false, false, false, "F16");
    public HotkeyBinding ToggleMuteChat { get; set; } = new(false, false, false, false, "F17");
    public HotkeyBinding ToggleDuckChat { get; set; } = new(false, false, false, false, "F18");
}
