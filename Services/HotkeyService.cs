using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using System.Windows.Input;
using ChatMix.Models;

namespace ChatMix.Services;

/// <summary>
/// Registers system-wide hotkeys via RegisterHotKey/WM_HOTKEY, delivered to a hidden message-only
/// window. This is what lets a Stream Deck's built-in "Hotkey" system action trigger ChatMix without
/// any plugin: the Stream Deck just sends the same keystroke a keyboard would, system-wide.
/// </summary>
public sealed class HotkeyService : IDisposable
{
    private sealed class HotkeyWindow : Form
    {
        public event Action<int>? HotkeyMessage;

        public HotkeyWindow()
        {
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            Location = new System.Drawing.Point(-2000, -2000);
            Size = new System.Drawing.Size(0, 0);
        }

        protected override void SetVisibleCore(bool value) => base.SetVisibleCore(false);

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == NativeMethods.WM_HOTKEY)
                HotkeyMessage?.Invoke(m.WParam.ToInt32());
            base.WndProc(ref m);
        }
    }

    private readonly HotkeyWindow _window;
    private readonly Dictionary<int, HotkeyAction> _idToAction = new();
    private int _nextId = 0xB000;

    public event Action<HotkeyAction>? HotkeyPressed;

    public HotkeyService()
    {
        _window = new HotkeyWindow();
        _ = _window.Handle; // forces Win32 handle creation without ever showing the window
        _window.HotkeyMessage += id =>
        {
            if (_idToAction.TryGetValue(id, out var action))
                HotkeyPressed?.Invoke(action);
        };
    }

    /// <summary>Re-registers every binding, returning the list of actions whose hotkey could not be
    /// registered (typically because another app already owns that combo).</summary>
    public List<HotkeyAction> RegisterAll(HotkeySettings hotkeys)
    {
        UnregisterAll();

        var failed = new List<HotkeyAction>();
        void TryRegister(HotkeyAction action, HotkeyBinding binding)
        {
            if (!Register(action, binding))
                failed.Add(action);
        }

        TryRegister(HotkeyAction.ChatVolumeUp, hotkeys.ChatVolumeUp);
        TryRegister(HotkeyAction.ChatVolumeDown, hotkeys.ChatVolumeDown);
        TryRegister(HotkeyAction.EverythingVolumeUp, hotkeys.EverythingVolumeUp);
        TryRegister(HotkeyAction.EverythingVolumeDown, hotkeys.EverythingVolumeDown);
        TryRegister(HotkeyAction.ToggleMuteChat, hotkeys.ToggleMuteChat);
        TryRegister(HotkeyAction.ToggleDuckChat, hotkeys.ToggleDuckChat);
        return failed;
    }

    private bool Register(HotkeyAction action, HotkeyBinding binding)
    {
        if (string.IsNullOrEmpty(binding.Key) || binding.Key == "None") return true; // intentionally unbound

        if (!Enum.TryParse<Key>(binding.Key, out var key)) return false;
        var vk = (uint)KeyInterop.VirtualKeyFromKey(key);
        if (vk == 0) return false;

        uint mod = NativeMethods.MOD_NOREPEAT;
        if (binding.Ctrl) mod |= NativeMethods.MOD_CONTROL;
        if (binding.Alt) mod |= NativeMethods.MOD_ALT;
        if (binding.Shift) mod |= NativeMethods.MOD_SHIFT;
        if (binding.Win) mod |= NativeMethods.MOD_WIN;

        int id = _nextId++;
        bool ok = NativeMethods.RegisterHotKey(_window.Handle, id, mod, vk);
        if (ok) _idToAction[id] = action;
        return ok;
    }

    public void UnregisterAll()
    {
        foreach (var id in _idToAction.Keys.ToList())
            NativeMethods.UnregisterHotKey(_window.Handle, id);
        _idToAction.Clear();
    }

    public void Dispose()
    {
        UnregisterAll();
        _window.Dispose();
    }
}
