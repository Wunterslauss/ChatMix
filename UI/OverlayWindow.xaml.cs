using System;
using System.Windows;
using System.Windows.Threading;

namespace ChatMix.UI;

public partial class OverlayWindow : Window
{
    private readonly DispatcherTimer _hideTimer;

    public OverlayWindow()
    {
        InitializeComponent();

        // Kept alive and invisible (Opacity 0) at all times; a hotkey press just fades it in and
        // starts the auto-hide timer. Avoids repeated Show()/Hide() window-handle churn.
        Opacity = 0;
        Show();

        _hideTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1500) };
        _hideTimer.Tick += (_, __) =>
        {
            _hideTimer.Stop();
            Opacity = 0;
        };
    }

    public void ShowVolume(string title, int percent, bool muted)
    {
        percent = Math.Clamp(percent, 0, 100);
        Present(title, muted ? "Muted" : $"{percent}%", percent);
    }

    /// <summary>Crossfade feedback: shows both values, with the fill bar tracking the chat side.</summary>
    public void ShowBalance(int chatPercent, int everythingPercent)
    {
        chatPercent = Math.Clamp(chatPercent, 0, 100);
        everythingPercent = Math.Clamp(everythingPercent, 0, 100);
        Present("Chat / Everything", $"{chatPercent}% / {everythingPercent}%", chatPercent);
    }

    private void Present(string title, string valueText, int fillPercent)
    {
        TitleText.Text = title;
        PercentText.Text = valueText;
        FillBar.Width = FillTrack.Width * (fillPercent / 100.0);

        PositionWindow();
        Opacity = 1;
        _hideTimer.Stop();
        _hideTimer.Start();
    }

    private void PositionWindow()
    {
        var area = SystemParameters.WorkArea;
        Left = area.Left + (area.Width - ActualWidth) / 2;
        Top = area.Top + area.Height - ActualHeight - 80;
    }
}
