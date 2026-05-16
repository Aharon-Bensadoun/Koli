using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Koli.Config;

namespace Koli.UI;

/// <summary>
/// About / developer information dialog (version, author, contact).
/// </summary>
internal sealed class AboutDialog : Form
{
    [DllImport("dwmapi.dll", CharSet = CharSet.Unicode, PreserveSig = false)]
    private static extern void DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int pvAttribute, int cbAttribute);

    [DllImport("user32.dll")]
    private static extern int SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam);

    [DllImport("user32.dll")]
    private static extern bool ReleaseCapture();

    private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
    private const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
    private const int DWMWA_SYSTEMBACKDROP_TYPE = 38;
    private const int DWMWCP_ROUND = 2;
    private const int DWMSBT_TABBEDWINDOW = 4;
    private const int WM_NCLBUTTONDOWN = 0xA1;
    private const int HT_CAPTION = 0x2;

    private const int FormW = 440;
    private const int FormH = 420;
    private const int Pad = 28;
    private const int TitleBarH = 52;
    private const int ButtonBarH = 64;

    public AboutDialog()
    {
        Text = "About Koli";
        Size = new Size(FormW, FormH);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.None;
        MaximizeBox = false;
        MinimizeBox = false;
        BackColor = FluentColors.Background;
        ShowInTaskbar = false;
        DoubleBuffered = true;

        var titleBar = new Panel
        {
            Dock = DockStyle.Top,
            Height = TitleBarH,
            BackColor = FluentColors.Surface
        };

        var titleLabel = new Label
        {
            Text = "About Koli",
            Font = new Font("Segoe UI", 12F, FontStyle.Bold),
            ForeColor = FluentColors.TextPrimary,
            AutoSize = true,
            Location = new Point(Pad, 16)
        };
        titleBar.Controls.Add(titleLabel);
        titleBar.MouseDown += OnTitleBarMouseDown;

        var mainPanel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = FluentColors.Background,
            AutoScroll = true
        };

        var y = 12;

        var appIcon = LoadAppIcon();
        if (appIcon != null)
        {
            var iconBox = new PictureBox
            {
                Image = appIcon.ToBitmap(),
                SizeMode = PictureBoxSizeMode.Zoom,
                Size = new Size(48, 48),
                Location = new Point((FormW - 48) / 2, y),
                BackColor = Color.Transparent
            };
            mainPanel.Controls.Add(iconBox);
            y += 58;
        }

        mainPanel.Controls.Add(CreateCenteredLabel(AppInfo.ProductName, FluentFonts.EmptyStateTitle, FluentColors.TextPrimary, y));
        y += 32;

        mainPanel.Controls.Add(CreateCenteredLabel($"Version {AppInfo.Version}", FluentFonts.Subtitle, FluentColors.AccentGlow, y));
        y += 28;

        var description = WrapText(AppInfo.Description, 52);
        var descLabel = new Label
        {
            Text = description,
            Font = FluentFonts.Body,
            ForeColor = FluentColors.TextSecondary,
            Location = new Point(Pad, y),
            Size = new Size(FormW - Pad * 2, MeasureTextHeight(description, FluentFonts.Body, FormW - Pad * 2)),
            TextAlign = ContentAlignment.TopCenter
        };
        mainPanel.Controls.Add(descLabel);
        y += descLabel.Height + 20;

        mainPanel.Controls.Add(CreateSectionLabel("Developer", Pad, y));
        y += 24;

        var devLabel = new Label
        {
            Text = AppInfo.DeveloperName,
            Font = FluentFonts.BodyLarge,
            ForeColor = FluentColors.TextPrimary,
            Location = new Point(Pad, y),
            Size = new Size(FormW - Pad * 2, 22),
            TextAlign = ContentAlignment.MiddleCenter
        };
        mainPanel.Controls.Add(devLabel);
        y += 30;

        mainPanel.Controls.Add(CreateSectionLabel("Contact", Pad, y));
        y += 24;

        var emailLink = CreateLinkLabel(AppInfo.ContactEmail, $"mailto:{AppInfo.ContactEmail}", Pad, y);
        mainPanel.Controls.Add(emailLink);
        y += 26;

        var repoLink = CreateLinkLabel("GitHub — Report issues & source", AppInfo.RepositoryUrl, Pad, y);
        mainPanel.Controls.Add(repoLink);
        y += 32;

        var copyrightLabel = new Label
        {
            Text = AppInfo.Copyright,
            Font = FluentFonts.CaptionSmall,
            ForeColor = FluentColors.TextMuted,
            Location = new Point(Pad, y),
            Size = new Size(FormW - Pad * 2, 36),
            TextAlign = ContentAlignment.TopCenter
        };
        mainPanel.Controls.Add(copyrightLabel);

        var buttonPanel = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = ButtonBarH,
            BackColor = FluentColors.Background
        };

        var closeButton = new Button
        {
            Text = "Close",
            Size = new Size(100, 36),
            Location = new Point(FormW - Pad - 100, 14),
            FlatStyle = FlatStyle.Flat,
            ForeColor = FluentColors.TextPrimary,
            BackColor = FluentColors.AccentPrimary,
            DialogResult = DialogResult.OK,
            Font = FluentFonts.ButtonText,
            Cursor = Cursors.Hand
        };
        closeButton.FlatAppearance.BorderSize = 0;
        closeButton.FlatAppearance.MouseDownBackColor = FluentColors.AccentPressed;
        closeButton.FlatAppearance.MouseOverBackColor = FluentColors.AccentHover;
        buttonPanel.Controls.Add(closeButton);

        Controls.Add(mainPanel);
        Controls.Add(titleBar);
        Controls.Add(buttonPanel);
        AcceptButton = closeButton;
        CancelButton = closeButton;

        Load += (_, _) => ApplyModernStyle();
    }

    private static Label CreateCenteredLabel(string text, Font font, Color color, int y) =>
        new()
        {
            Text = text,
            Font = font,
            ForeColor = color,
            Location = new Point(Pad, y),
            Size = new Size(FormW - Pad * 2, 26),
            TextAlign = ContentAlignment.MiddleCenter
        };

    private static Label CreateSectionLabel(string text, int x, int y) =>
        new()
        {
            Text = text,
            Font = FluentFonts.SectionLabel,
            ForeColor = FluentColors.TextTertiary,
            Location = new Point(x, y),
            AutoSize = true
        };

    private static LinkLabel CreateLinkLabel(string text, string url, int x, int y)
    {
        var link = new LinkLabel
        {
            Text = text,
            Font = FluentFonts.Body,
            LinkColor = FluentColors.AccentGlow,
            ActiveLinkColor = FluentColors.AccentHover,
            VisitedLinkColor = FluentColors.AccentSecondary,
            Location = new Point(x, y),
            Size = new Size(FormW - x * 2, 22),
            TextAlign = ContentAlignment.MiddleCenter,
            Tag = url
        };
        link.LinkClicked += (_, _) => OpenUrl((string)link.Tag!);
        return link;
    }

    private static void OpenUrl(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Could not open link:\n{ex.Message}", "About Koli",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private static Icon? LoadAppIcon()
    {
        foreach (var name in new[] { "Koli.Resources.Koli.ico", "Resources.Koli.ico", "Koli.ico" })
        {
            using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(name);
            if (stream != null)
            {
                return new Icon(stream);
            }
        }

        return null;
    }

    private static string WrapText(string text, int maxCharsPerLine)
    {
        if (string.IsNullOrWhiteSpace(text) || text.Length <= maxCharsPerLine)
        {
            return text;
        }

        var words = text.Split(' ');
        var lines = new List<string>();
        var current = "";
        foreach (var word in words)
        {
            var candidate = string.IsNullOrEmpty(current) ? word : $"{current} {word}";
            if (candidate.Length > maxCharsPerLine && !string.IsNullOrEmpty(current))
            {
                lines.Add(current);
                current = word;
            }
            else
            {
                current = candidate;
            }
        }

        if (!string.IsNullOrEmpty(current))
        {
            lines.Add(current);
        }

        return string.Join(Environment.NewLine, lines);
    }

    private static int MeasureTextHeight(string text, Font font, int width)
    {
        using var g = Graphics.FromHwnd(IntPtr.Zero);
        var size = g.MeasureString(text, font, width);
        return (int)Math.Ceiling(size.Height) + 4;
    }

    private void OnTitleBarMouseDown(object? sender, MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left)
        {
            ReleaseCapture();
            SendMessage(Handle, WM_NCLBUTTONDOWN, HT_CAPTION, 0);
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        using var borderPen = new Pen(Color.FromArgb(110, 124, 58, 237), 1f);
        e.Graphics.DrawRectangle(borderPen, 0, 0, Width - 1, Height - 1);
    }

    private void ApplyModernStyle()
    {
        try
        {
            var darkMode = 1;
            DwmSetWindowAttribute(Handle, DWMWA_USE_IMMERSIVE_DARK_MODE, ref darkMode, sizeof(int));

            var cornerPreference = DWMWCP_ROUND;
            DwmSetWindowAttribute(Handle, DWMWA_WINDOW_CORNER_PREFERENCE, ref cornerPreference, sizeof(int));

            try
            {
                var backdropType = DWMSBT_TABBEDWINDOW;
                DwmSetWindowAttribute(Handle, DWMWA_SYSTEMBACKDROP_TYPE, ref backdropType, sizeof(int));
            }
            catch
            {
                // Mica backdrop is best-effort.
            }
        }
        catch
        {
            var path = new GraphicsPath();
            const int radius = 14;
            path.AddArc(0, 0, radius * 2, radius * 2, 180, 90);
            path.AddArc(Width - radius * 2, 0, radius * 2, radius * 2, 270, 90);
            path.AddArc(Width - radius * 2, Height - radius * 2, radius * 2, radius * 2, 0, 90);
            path.AddArc(0, Height - radius * 2, radius * 2, radius * 2, 90, 90);
            path.CloseFigure();
            Region = new Region(path);
        }
    }
}
