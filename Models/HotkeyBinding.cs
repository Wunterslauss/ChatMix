using System.Collections.Generic;

namespace ChatMix.Models;

/// <summary>A key combo. Key is the string name of a System.Windows.Input.Key (e.g. "F13", "Up"), or "None" if unbound.</summary>
public class HotkeyBinding
{
    public bool Ctrl { get; set; }
    public bool Alt { get; set; }
    public bool Shift { get; set; }
    public bool Win { get; set; }
    public string Key { get; set; } = "None";

    public HotkeyBinding() { }

    public HotkeyBinding(bool ctrl, bool alt, bool shift, bool win, string key)
    {
        Ctrl = ctrl;
        Alt = alt;
        Shift = shift;
        Win = win;
        Key = key;
    }

    public override string ToString()
    {
        if (string.IsNullOrEmpty(Key) || Key == "None") return "(none)";
        var parts = new List<string>();
        if (Ctrl) parts.Add("Ctrl");
        if (Alt) parts.Add("Alt");
        if (Shift) parts.Add("Shift");
        if (Win) parts.Add("Win");
        parts.Add(Key);
        return string.Join("+", parts);
    }
}
