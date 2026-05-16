using System.ComponentModel;
using System.Drawing.Drawing2D;

namespace AudioPlayer;

internal sealed class WaveformControl : Control
{
    private float[] _peaks = [];
    private string _emptyText = "Open an audio file";
    private double _progress;
    private bool _dragging;

    public WaveformControl()
    {
        DoubleBuffered = true;
        SetStyle(ControlStyles.ResizeRedraw | ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint, true);
        BackColor = Color.FromArgb(18, 21, 24);
        ForeColor = Color.FromArgb(116, 215, 255);
        Cursor = Cursors.Hand;
    }

    public event EventHandler<double>? SeekRequested;

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public double Progress
    {
        get => _progress;
        set
        {
            var next = Math.Clamp(value, 0, 1);
            if (Math.Abs(_progress - next) < 0.0001)
            {
                return;
            }

            _progress = next;
            Invalidate();
        }
    }

    public void SetPeaks(float[] peaks, bool resetProgress = true)
    {
        _peaks = peaks;
        _emptyText = "Open an audio file";
        if (resetProgress)
        {
            _progress = 0;
        }

        Invalidate();
    }

    public void Clear(string emptyText = "Open an audio file")
    {
        _peaks = [];
        _emptyText = emptyText;
        _progress = 0;
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);

        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(BackColor);

        var bounds = ClientRectangle;
        if (bounds.Width <= 2 || bounds.Height <= 2)
        {
            return;
        }

        using var borderPen = new Pen(Color.FromArgb(58, 67, 74));
        using var centerPen = new Pen(Color.FromArgb(45, 52, 58));
        using var playedPen = new Pen(Color.FromArgb(83, 212, 138), 1.4f);
        using var remainingPen = new Pen(ForeColor, 1.2f);
        using var scrubPen = new Pen(Color.White, 2f);

        var centerY = bounds.Top + bounds.Height / 2f;
        g.DrawLine(centerPen, bounds.Left + 1, centerY, bounds.Right - 1, centerY);

        if (_peaks.Length == 0)
        {
            DrawEmptyState(g, bounds);
            g.DrawRectangle(borderPen, bounds.Left, bounds.Top, bounds.Width - 1, bounds.Height - 1);
            return;
        }

        var playedX = bounds.Left + (float)(_progress * Math.Max(1, bounds.Width - 1));
        var maxAmplitude = bounds.Height * 0.44f;
        var xScale = (float)_peaks.Length / bounds.Width;

        for (var x = 0; x < bounds.Width; x++)
        {
            var sampleIndex = Math.Min(_peaks.Length - 1, (int)(x * xScale));
            var amplitude = Math.Max(1f, _peaks[sampleIndex] * maxAmplitude);
            var actualX = bounds.Left + x;
            var pen = actualX <= playedX ? playedPen : remainingPen;
            g.DrawLine(pen, actualX, centerY - amplitude, actualX, centerY + amplitude);
        }

        g.DrawLine(scrubPen, playedX, bounds.Top + 1, playedX, bounds.Bottom - 2);
        g.DrawRectangle(borderPen, bounds.Left, bounds.Top, bounds.Width - 1, bounds.Height - 1);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button != MouseButtons.Left)
        {
            return;
        }

        _dragging = true;
        Capture = true;
        SeekFromMouse(e.X);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (_dragging)
        {
            SeekFromMouse(e.X);
        }
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (e.Button != MouseButtons.Left)
        {
            return;
        }

        _dragging = false;
        Capture = false;
        SeekFromMouse(e.X);
    }

    private void SeekFromMouse(int x)
    {
        if (ClientSize.Width <= 0)
        {
            return;
        }

        var value = Math.Clamp((double)x / ClientSize.Width, 0, 1);
        _progress = value;
        Invalidate();
        SeekRequested?.Invoke(this, value);
    }

    private void DrawEmptyState(Graphics g, Rectangle bounds)
    {
        using var textBrush = new SolidBrush(Color.FromArgb(139, 151, 160));
        using var format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
        g.DrawString(_emptyText, SystemFonts.MessageBoxFont ?? Control.DefaultFont, textBrush, bounds, format);
    }
}
