using System;
using System.Windows.Forms;
using ChatMix.Services;

namespace ChatMix.UI;

public sealed class TrayIconManager : IDisposable
{
    private readonly NotifyIcon _notifyIcon;
    private readonly ContextMenuStrip _menu;
    private readonly ToolStripMenuItem _chatVolumeItem;
    private readonly ToolStripMenuItem _everythingVolumeItem;
    private readonly ToolStripMenuItem _startupItem;
    private readonly AudioSessionService _audio;
    private readonly SettingsService _settings;

    public TrayIconManager(AudioSessionService audio, SettingsService settings, Action openSettings, Action exit)
    {
        _audio = audio;
        _settings = settings;

        _chatVolumeItem = new ToolStripMenuItem { Enabled = false };
        _everythingVolumeItem = new ToolStripMenuItem { Enabled = false };

        _startupItem = new ToolStripMenuItem("Start with Windows")
        {
            CheckOnClick = true,
            Checked = settings.Settings.StartWithWindows
        };
        _startupItem.Click += (_, __) =>
        {
            settings.Settings.StartWithWindows = _startupItem.Checked;
            StartupService.SetStartWithWindows(_startupItem.Checked);
            settings.Save();
        };

        var settingsItem = new ToolStripMenuItem("Settings...");
        settingsItem.Click += (_, __) => openSettings();

        var exitItem = new ToolStripMenuItem("Exit");
        exitItem.Click += (_, __) => exit();

        _menu = new ContextMenuStrip();
        _menu.Items.Add(_chatVolumeItem);
        _menu.Items.Add(_everythingVolumeItem);
        _menu.Items.Add(new ToolStripSeparator());
        _menu.Items.Add(settingsItem);
        _menu.Items.Add(_startupItem);
        _menu.Items.Add(new ToolStripSeparator());
        _menu.Items.Add(exitItem);
        _menu.Opening += (_, __) => RefreshLabels();

        _notifyIcon = new NotifyIcon
        {
            Icon = System.Drawing.Icon.ExtractAssociatedIcon(Environment.ProcessPath ?? "") ?? System.Drawing.SystemIcons.Application,
            Visible = true,
            Text = "ChatMix",
            ContextMenuStrip = _menu
        };
        _notifyIcon.DoubleClick += (_, __) => openSettings();

        audio.StateChanged += RefreshLabels;
        RefreshLabels();
    }

    private void RefreshLabels()
    {
        _chatVolumeItem.Text = $"Chat: {_audio.ChatVolumePercent}%{(_audio.ChatMuted ? " (Muted)" : "")}";
        _everythingVolumeItem.Text = $"Everything Else: {_audio.EverythingVolumePercent}%{(_audio.EverythingMuted ? " (Muted)" : "")}";

        // Re-sync from the live setting every time the menu is about to be shown, so a change made
        // via the Settings window (which doesn't know about this control) isn't left stale here.
        _startupItem.Checked = _settings.Settings.StartWithWindows;

        var degraded = _audio.LastPollError != null ? " - audio device issue" : "";
        var tooltip = $"ChatMix - Chat {_audio.ChatVolumePercent}%{(_audio.ChatMuted ? " (Muted)" : "")} | Rest {_audio.EverythingVolumePercent}%{(_audio.EverythingMuted ? " (Muted)" : "")}{degraded}";
        _notifyIcon.Text = tooltip.Length > 63 ? tooltip.Substring(0, 63) : tooltip;
    }

    public void ShowBalloon(string title, string text)
    {
        _notifyIcon.BalloonTipTitle = title;
        _notifyIcon.BalloonTipText = text;
        _notifyIcon.BalloonTipIcon = ToolTipIcon.Warning;
        _notifyIcon.ShowBalloonTip(4000);
    }

    public void Dispose()
    {
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        _menu.Dispose();
    }
}
