using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Koli.Platform;

/// <summary>
/// Custom top-right toast (fade in/out) matching the original WinForms Koli design.
/// </summary>
public sealed class CustomToastForm : Form
{
    private readonly System.Windows.Forms.Timer _timer;
    private readonly System.Windows.Forms.Timer _fadeTimer;
    private float _opacity;
    private bool _fadingOut;

    private const int DwmwaWindowCornerPreference = 33;
    private const int DwmwcpRound = 2;
    private const int ToastWidth = 340;
    private const int MinHeight = 96;
    private const int MaxHeight = 380;
    private const int MessageAreaWidth = 260;
    private const int TopPadding = 46;
    private const int BottomPadding = 12;

    [DllImport("dwmapi.dll", CharSet = CharSet.Unicode, PreserveSig = false)]
    private static extern void DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int pvAttribute, int cbAttribute);

    public CustomToastForm(Icon icon, string title, string message, int displayDurationMs = 3000)
    {
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        TopMost = true;
        BackColor = Color.FromArgb(22, 22, 30);
        Opacity = 0;

        Width = ToastWidth;
        var messageFont = new Font("Segoe UI", 9.5F, FontStyle.Regular);
        var messageHeight = MeasureMessageHeight(message, messageFont, MessageAreaWidth);
        Height = Math.Clamp(TopPadding + messageHeight + BottomPadding, MinHeight, MaxHeight);

        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);

        var primaryScreen = Screen.PrimaryScreen;
        if (primaryScreen != null)
        {
            var wa = primaryScreen.WorkingArea;
            Location = new Point(wa.Right - Width - 16, wa.Top + 16);
        }

        var pictureBox = new PictureBox
        {
            Image = icon.ToBitmap(),
            SizeMode = PictureBoxSizeMode.Zoom,
            Size = new Size(36, 36),
            Location = new Point(16, 28),
            BackColor = Color.Transparent
        };

        var titleLabel = new Label
        {
            Text = title,
            AutoSize = false,
            Location = new Point(64, 20),
            Size = new Size(Width - 80, 26),
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 11F, FontStyle.Bold),
            BackColor = Color.Transparent
        };

        var messageLabel = new Label
        {
            Text = message,
            AutoSize = false,
            Location = new Point(64, TopPadding),
            Size = new Size(MessageAreaWidth, Height - TopPadding - BottomPadding),
            ForeColor = Color.FromArgb(163, 163, 178),
            Font = messageFont,
            BackColor = Color.Transparent,
            AutoEllipsis = false
        };

        var accentBar = new Panel
        {
            Size = new Size(3, Height - 24),
            Location = new Point(0, 12),
            BackColor = Color.FromArgb(124, 58, 237)
        };

        Controls.Add(accentBar);
        Controls.Add(pictureBox);
        Controls.Add(titleLabel);
        Controls.Add(messageLabel);

        _fadeTimer = new System.Windows.Forms.Timer { Interval = 16 };
        _fadeTimer.Tick += (_, _) =>
        {
            if (!_fadingOut)
            {
                _opacity += 0.12f;
                if (_opacity >= 0.96f)
                {
                    _opacity = 0.96f;
                    _fadeTimer.Stop();
                }
            }
            else
            {
                _opacity -= 0.08f;
                if (_opacity <= 0)
                {
                    _fadeTimer.Stop();
                    Close();
                    return;
                }
            }

            Opacity = _opacity;
        };
        _fadeTimer.Start();

        _timer = new System.Windows.Forms.Timer { Interval = Math.Max(1000, displayDurationMs) };
        _timer.Tick += (_, _) =>
        {
            _timer.Stop();
            _fadingOut = true;
            _fadeTimer.Start();
        };
        _timer.Start();

        Click += (_, _) => StartFadeOut();
        foreach (Control c in Controls)
            c.Click += (_, _) => StartFadeOut();

        Shown += (_, _) => ApplyRoundedCorners();
    }

    private void ApplyRoundedCorners()
    {
        try
        {
            var preference = DwmwcpRound;
            DwmSetWindowAttribute(Handle, DwmwaWindowCornerPreference, ref preference, sizeof(int));
        }
        catch
        {
            var path = new GraphicsPath();
            const int radius = 12;
            path.AddArc(0, 0, radius * 2, radius * 2, 180, 90);
            path.AddArc(Width - radius * 2, 0, radius * 2, radius * 2, 270, 90);
            path.AddArc(Width - radius * 2, Height - radius * 2, radius * 2, radius * 2, 0, 90);
            path.AddArc(0, Height - radius * 2, radius * 2, radius * 2, 90, 90);
            path.CloseFigure();
            Region = new Region(path);
        }
    }

    private void StartFadeOut()
    {
        _timer.Stop();
        _fadingOut = true;
        if (!_fadeTimer.Enabled)
            _fadeTimer.Start();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        using var pen = new Pen(Color.FromArgb(42, 42, 56), 1);
        e.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _timer.Dispose();
            _fadeTimer.Dispose();
        }

        base.Dispose(disposing);
    }

    private static int MeasureMessageHeight(string message, Font font, int maxWidth)
    {
        if (string.IsNullOrEmpty(message))
            return 34;

        var flags = TextFormatFlags.WordBreak | TextFormatFlags.TextBoxControl | TextFormatFlags.NoPadding;
        var size = TextRenderer.MeasureText(message, font, new Size(maxWidth, int.MaxValue), flags);
        return Math.Max(34, Math.Min(size.Height, MaxHeight - TopPadding - BottomPadding));
    }
}
