using System.ComponentModel;

namespace AudioPlayer;

internal sealed class TimelineRulerControl : Control
{
    private TimeSpan _duration = TimeSpan.Zero;

    public TimelineRulerControl()
    {
        DoubleBuffered = true;
        BackColor = Color.FromArgb(12, 15, 18);
        ForeColor = Color.FromArgb(242, 247, 250);
        Font = new Font("Consolas", 9.5f, FontStyle.Bold);
        SetStyle(ControlStyles.ResizeRedraw | ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint, true);
    }

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public TimeSpan Duration
    {
        get => _duration;
        set
        {
            var next = value < TimeSpan.Zero ? TimeSpan.Zero : value;
            if (_duration == next)
            {
                return;
            }

            _duration = next;
            Invalidate();
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);

        var g = e.Graphics;
        g.Clear(BackColor);

        if (Width < 20 || Height < 12 || _duration.TotalSeconds <= 0)
        {
            return;
        }

        using var tickPen = new Pen(Color.FromArgb(210, 224, 232), 1.6f);
        using var minorTickPen = new Pen(Color.FromArgb(118, 137, 149), 1f);
        using var textBrush = new SolidBrush(ForeColor);
        using var format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Near };

        var totalSeconds = _duration.TotalSeconds;
        var tickSeconds = ChooseTickSeconds(totalSeconds);
        for (var seconds = 0d; seconds <= totalSeconds + 0.001; seconds += tickSeconds)
        {
            var x = (float)(seconds / totalSeconds * (Width - 1));
            var major = Math.Abs(seconds % 60) < 0.001 || tickSeconds >= 60;
            var tickHeight = major ? 12 : 7;
            g.DrawLine(major ? tickPen : minorTickPen, x, 0, x, tickHeight);

            if (major)
            {
                g.DrawString(FormatRulerTime(TimeSpan.FromSeconds(seconds)), Font, textBrush, new RectangleF(x - 34, tickHeight + 1, 68, Height - tickHeight - 1), format);
            }
        }
    }

    private static double ChooseTickSeconds(double totalSeconds)
    {
        if (totalSeconds <= 120)
        {
            return 10;
        }

        if (totalSeconds <= 600)
        {
            return 30;
        }

        return 60;
    }

    private static string FormatRulerTime(TimeSpan time)
    {
        return time.TotalHours >= 1
            ? $"{(int)time.TotalHours}:{time.Minutes:00}"
            : $"{(int)time.TotalMinutes}:00";
    }
}
