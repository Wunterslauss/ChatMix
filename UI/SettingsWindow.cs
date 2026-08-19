using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ChatMix.Models;
using ChatMix.Services;

namespace ChatMix.UI;

public sealed class SettingsWindow : Window
{
    public event Action? SettingsSaved;

    private readonly SettingsService _settingsService;
    private readonly AppSettings _s;

    private TextBox _processListBox = null!;
    private TextBox _stepBox = null!;
    private TextBox _duckBox = null!;
    private CheckBox _startupCheck = null!;

    private readonly Dictionary<HotkeyAction, HotkeyBinding> _workingHotkeys = new();

    public SettingsWindow(SettingsService settingsService)
    {
        _settingsService = settingsService;
        _s = settingsService.Settings;

        Title = "ChatMix Settings";
        Width = 460;
        Height = 500;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = true;

        foreach (var kvp in GetActionBindingMap())
            _workingHotkeys[kvp.Key] = Clone(kvp.Value);

        var tabs = new TabControl { Margin = new Thickness(10) };
        tabs.Items.Add(BuildChatAppsTab());
        tabs.Items.Add(BuildHotkeysTab());
        tabs.Items.Add(BuildGeneralTab());

        var saveButton = new Button { Content = "Save", Width = 90, Margin = new Thickness(0, 0, 8, 0), IsDefault = true };
        saveButton.Click += (_, __) => Save();
        var cancelButton = new Button { Content = "Cancel", Width = 90, IsCancel = true };

        var buttonPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(10) };
        buttonPanel.Children.Add(saveButton);
        buttonPanel.Children.Add(cancelButton);

        var root = new DockPanel();
        DockPanel.SetDock(buttonPanel, Dock.Bottom);
        root.Children.Add(buttonPanel);
        root.Children.Add(tabs);

        Content = root;

        // The tray menu's "Start with Windows" item can change _s.StartWithWindows while this
        // window is open. Re-sync the checkbox whenever the window regains focus, so Save() below
        // doesn't blindly overwrite that change with a stale snapshot taken at construction time.
        Activated += (_, __) => _startupCheck.IsChecked = _s.StartWithWindows;
    }

    private Dictionary<HotkeyAction, HotkeyBinding> GetActionBindingMap() => new()
    {
        [HotkeyAction.ChatVolumeUp] = _s.Hotkeys.ChatVolumeUp,
        [HotkeyAction.ChatVolumeDown] = _s.Hotkeys.ChatVolumeDown,
        [HotkeyAction.EverythingVolumeUp] = _s.Hotkeys.EverythingVolumeUp,
        [HotkeyAction.EverythingVolumeDown] = _s.Hotkeys.EverythingVolumeDown,
        [HotkeyAction.ToggleMuteChat] = _s.Hotkeys.ToggleMuteChat,
        [HotkeyAction.ToggleDuckChat] = _s.Hotkeys.ToggleDuckChat,
    };

    private static HotkeyBinding Clone(HotkeyBinding b) => new(b.Ctrl, b.Alt, b.Shift, b.Win, b.Key);

    private TabItem BuildChatAppsTab()
    {
        var panel = new StackPanel { Margin = new Thickness(12) };
        panel.Children.Add(new TextBlock
        {
            Text = "One process name per line (e.g. discord.exe). Any active audio session whose "
                 + "process matches this list is treated as \"Chat\"; every other active session is "
                 + "grouped as \"Everything Else\".",
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 10)
        });
        _processListBox = new TextBox
        {
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            Height = 260,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Text = string.Join(Environment.NewLine, _s.ChatProcessNames)
        };
        panel.Children.Add(_processListBox);
        return new TabItem { Header = "Chat Apps", Content = panel };
    }

    private TabItem BuildHotkeysTab()
    {
        var outer = new StackPanel { Margin = new Thickness(12) };

        _stepBox = new TextBox { Text = _s.StepPercent.ToString(), Width = 60, HorizontalAlignment = HorizontalAlignment.Left };
        _duckBox = new TextBox { Text = _s.DuckPercent.ToString(), Width = 60, HorizontalAlignment = HorizontalAlignment.Left };
        outer.Children.Add(LabeledRow("Volume step (%):", _stepBox));
        outer.Children.Add(LabeledRow("Duck volume (%):", _duckBox));

        outer.Children.Add(new Separator { Margin = new Thickness(0, 8, 0, 8) });
        outer.Children.Add(new TextBlock
        {
            Text = "Click a box, then press the key combo to bind (e.g. F13, or Ctrl+Alt+Up). Press Escape to unbind.",
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 8)
        });

        foreach (var action in new[]
                 {
                     HotkeyAction.ChatVolumeUp, HotkeyAction.ChatVolumeDown,
                     HotkeyAction.EverythingVolumeUp, HotkeyAction.EverythingVolumeDown,
                     HotkeyAction.ToggleMuteChat, HotkeyAction.ToggleDuckChat
                 })
        {
            var box = new TextBox
            {
                Width = 160,
                HorizontalAlignment = HorizontalAlignment.Left,
                IsReadOnly = true,
                Focusable = true,
                Text = _workingHotkeys[action].ToString(),
                Tag = action
            };
            box.PreviewKeyDown += HotkeyBox_PreviewKeyDown;
            outer.Children.Add(LabeledRow(action.ToFriendlyName() + ":", box));
        }

        return new TabItem { Header = "Hotkeys", Content = new ScrollViewer { Content = outer, VerticalScrollBarVisibility = ScrollBarVisibility.Auto } };
    }

    private static UIElement LabeledRow(string label, UIElement control)
    {
        var grid = new Grid { Margin = new Thickness(0, 0, 0, 6) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(170) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var text = new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center };
        Grid.SetColumn(text, 0);
        Grid.SetColumn(control, 1);
        grid.Children.Add(text);
        grid.Children.Add(control);
        return grid;
    }

    private void HotkeyBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        e.Handled = true;
        var box = (TextBox)sender;
        var action = (HotkeyAction)box.Tag;
        var key = e.Key == Key.System ? e.SystemKey : e.Key;

        if (key == Key.Escape)
        {
            _workingHotkeys[action] = new HotkeyBinding(false, false, false, false, "None");
            box.Text = "(none)";
            return;
        }

        if (key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt or
            Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin or Key.None)
        {
            return; // modifier alone: keep waiting for the real key
        }

        var binding = new HotkeyBinding(
            Keyboard.Modifiers.HasFlag(ModifierKeys.Control),
            Keyboard.Modifiers.HasFlag(ModifierKeys.Alt),
            Keyboard.Modifiers.HasFlag(ModifierKeys.Shift),
            Keyboard.Modifiers.HasFlag(ModifierKeys.Windows),
            key.ToString());

        _workingHotkeys[action] = binding;
        box.Text = binding.ToString();
    }

    private TabItem BuildGeneralTab()
    {
        var panel = new StackPanel { Margin = new Thickness(12) };
        _startupCheck = new CheckBox { Content = "Start ChatMix with Windows", IsChecked = _s.StartWithWindows, Margin = new Thickness(0, 0, 0, 10) };
        panel.Children.Add(_startupCheck);
        panel.Children.Add(new TextBlock
        {
            Text = "Settings are stored at:\n" + PathHint(),
            TextWrapping = TextWrapping.Wrap,
            Opacity = 0.7,
            Margin = new Thickness(0, 20, 0, 0)
        });
        return new TabItem { Header = "General", Content = panel };
    }

    private static string PathHint() =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ChatMix", "settings.json");

    private void Save()
    {
        var names = _processListBox.Text
            .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(n => n.Trim())
            .Where(n => n.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (names.Count == 0) names.Add("discord.exe");
        _s.ChatProcessNames = names;

        if (int.TryParse(_stepBox.Text, out var step)) _s.StepPercent = Math.Clamp(step, 1, 50);
        if (int.TryParse(_duckBox.Text, out var duck)) _s.DuckPercent = Math.Clamp(duck, 0, 100);

        _s.Hotkeys.ChatVolumeUp = _workingHotkeys[HotkeyAction.ChatVolumeUp];
        _s.Hotkeys.ChatVolumeDown = _workingHotkeys[HotkeyAction.ChatVolumeDown];
        _s.Hotkeys.EverythingVolumeUp = _workingHotkeys[HotkeyAction.EverythingVolumeUp];
        _s.Hotkeys.EverythingVolumeDown = _workingHotkeys[HotkeyAction.EverythingVolumeDown];
        _s.Hotkeys.ToggleMuteChat = _workingHotkeys[HotkeyAction.ToggleMuteChat];
        _s.Hotkeys.ToggleDuckChat = _workingHotkeys[HotkeyAction.ToggleDuckChat];

        _s.StartWithWindows = _startupCheck.IsChecked == true;

        _settingsService.Save();
        SettingsSaved?.Invoke();
        Close();
    }
}
