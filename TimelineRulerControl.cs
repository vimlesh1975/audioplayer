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
        Font = new Font("Consolas", 9f, FontStyle.Bold);
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

        if (Width < 20 || Height < 18 || _duration.TotalSeconds <= 0)
        {
            return;
        }

        using var tickPen = new Pen(Color.FromArgb(210, 224, 232), 1.6f);
        using var minorTickPen = new Pen(Color.FromArgb(118, 137, 149), 1f);
        using var textBrush = new SolidBrush(ForeColor);
        using var centerFormat = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Near };
        using var leftFormat = new StringFormat { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Near };
        using var rightFormat = new StringFormat { Alignment = StringAlignment.Far, LineAlignment = StringAlignment.Near };

        var totalSeconds = _duration.TotalSeconds;
        var tickSeconds = ChooseTickSeconds(totalSeconds);
        for (var seconds = 0d; seconds <= totalSeconds + 0.001; seconds += tickSeconds)
        {
            var x = (float)(seconds / totalSeconds * (Width - 1));
            var major = Math.Abs(seconds % 60) < 0.001 || tickSeconds >= 60;
            var tickHeight = major ? 11 : 6;
            g.DrawLine(major ? tickPen : minorTickPen, x, 0, x, tickHeight);

            if (major)
            {
                var labelBounds = new RectangleF(x - 34, tickHeight + 3, 68, Height - tickHeight - 4);
                if (labelBounds.Left > 48 && labelBounds.Right < Width - 48)
                {
                    g.DrawString(FormatRulerTime(TimeSpan.FromSeconds(seconds)), Font, textBrush, labelBounds, centerFormat);
                }
            }
        }

        var labelTop = 14f;
        var labelHeight = Math.Max(12f, Height - labelTop - 1);
        g.DrawString(FormatRulerTime(TimeSpan.Zero), Font, textBrush, new RectangleF(0, labelTop, 84, labelHeight), leftFormat);
        g.DrawString(FormatRulerTime(_duration), Font, textBrush, new RectangleF(Width - 104, labelTop, 104, labelHeight), rightFormat);
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
            ? $"{(int)time.TotalHours}:{time.Minutes:00}:{time.Seconds:00}"
            : $"{(int)time.TotalMinutes:00}:{time.Seconds:00}";
    }
}
