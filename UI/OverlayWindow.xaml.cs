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
        TitleText.Text = title;
        percent = Math.Clamp(percent, 0, 100);
        PercentText.Text = muted ? "Muted" : $"{percent}%";
        FillBar.Width = FillTrack.Width * (percent / 100.0);

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
