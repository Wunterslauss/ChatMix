namespace ChatMix.Models;

public enum HotkeyAction
{
    ChatVolumeUp,
    ChatVolumeDown,
    EverythingVolumeUp,
    EverythingVolumeDown,
    ToggleMuteChat,
    ToggleDuckChat,
    ToggleMuteEverything,
    ToggleDuckEverything
}

public static class HotkeyActionExtensions
{
    /// <summary>Single source of truth for display text - was previously duplicated in App.xaml.cs
    /// and UI/SettingsWindow.cs, which could drift out of sync.</summary>
    public static string ToFriendlyName(this HotkeyAction action) => action switch
    {
        HotkeyAction.ChatVolumeUp => "Chat Volume Up",
        HotkeyAction.ChatVolumeDown => "Chat Volume Down",
        HotkeyAction.EverythingVolumeUp => "Everything Else Volume Up",
        HotkeyAction.EverythingVolumeDown => "Everything Else Volume Down",
        HotkeyAction.ToggleMuteChat => "Toggle Mute Chat",
        HotkeyAction.ToggleDuckChat => "Toggle Duck Chat",
        HotkeyAction.ToggleMuteEverything => "Toggle Mute Everything Else",
        HotkeyAction.ToggleDuckEverything => "Toggle Duck Everything Else",
        _ => action.ToString()
    };
}
