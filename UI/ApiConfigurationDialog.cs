using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Koli.Config;

namespace Koli.UI;

/// <summary>
/// Self-contained dialog for editing the Azure / OpenAI / on-prem API configuration.
/// Reusable from <see cref="Program"/> at startup (before <see cref="MainForm"/> exists)
/// and from the runtime settings menu.
/// </summary>
internal sealed class ApiConfigurationDialog : Form
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

    // Visual layout constants.
    private const int FormW = 620;
    private const int FormH = 540;
    private const int Pad = 28;
    private const int InnerW = FormW - (Pad * 2);   // 564
    private const int TitleBarH = 52;
    private const int ButtonBarH = 64;

    private readonly AzureOpenAISettings _settings;
    private readonly TextBox _apiKeyBox;
    private readonly TextBox _endpointBox;
    private readonly ComboBox _modelBox;
    private readonly TextBox _providerIdBox;
    private readonly CheckBox _showKeyBox;

    public ApiConfigurationDialog(AzureOpenAISettings settings, bool isStartup)
    {
        _settings = settings;

        Text = "API Configuration";
        Size = new Size(FormW, FormH);
        StartPosition = isStartup ? FormStartPosition.CenterScreen : FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.None;
        MaximizeBox = false;
        MinimizeBox = false;
        BackColor = FluentColors.Background;
        ShowInTaskbar = isStartup;
        DoubleBuffered = true;

        var titleBar = new Panel
        {
            Dock = DockStyle.Top,
            Height = TitleBarH,
            BackColor = FluentColors.Surface
        };

        var titleLabel = new Label
        {
            Text = "API Configuration",
            Font = new Font("Segoe UI", 12F, FontStyle.Bold),
            ForeColor = FluentColors.TextPrimary,
            AutoSize = true,
            Location = new Point(Pad, 16)
        };
        titleBar.Controls.Add(titleLabel);

        titleBar.MouseDown += (_, e) =>
        {
            if (e.Button == MouseButtons.Left)
            {
                ReleaseCapture();
                SendMessage(Handle, WM_NCLBUTTONDOWN, HT_CAPTION, 0);
            }
        };

        var mainPanel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = FluentColors.Background
        };

        var y = 18;

        if (isStartup)
        {
            var introLabel = new Label
            {
                Text = "Configure your transcription API to start using Koli.",
                ForeColor = FluentColors.TextSecondary,
                Font = FluentFonts.Caption,
                Location = new Point(Pad, y),
                Size = new Size(InnerW, 22)
            };
            mainPanel.Controls.Add(introLabel);
            y += 32;
        }

        // Preset row
        mainPanel.Controls.Add(new Label
        {
            Text = "Preset",
            ForeColor = FluentColors.TextSecondary,
            Font = FluentFonts.Caption,
            Location = new Point(Pad, y),
            Size = new Size(InnerW, 20)
        });
        var presetBox = new ComboBox
        {
            Location = new Point(Pad, y + 22),
            Size = new Size(InnerW, 26),
            DropDownStyle = ComboBoxStyle.DropDownList,
            BackColor = FluentColors.Surface,
            ForeColor = FluentColors.TextPrimary,
            FlatStyle = FlatStyle.Flat,
            Font = FluentFonts.Body
        };
        presetBox.Items.AddRange(new object[]
        {
            "Custom / keep current",
            "OpenAI Cloud (api.openai.com)",
            "Ai Nexus / On-premise (custom endpoint)"
        });
        presetBox.SelectedIndex = 0;
        mainPanel.Controls.Add(presetBox);
        y += 60;

        // API Key row
        mainPanel.Controls.Add(new Label
        {
            Text = "API Key",
            ForeColor = FluentColors.TextSecondary,
            Font = FluentFonts.Caption,
            Location = new Point(Pad, y),
            Size = new Size(InnerW, 20)
        });
        _apiKeyBox = new TextBox
        {
            Location = new Point(Pad, y + 22),
            Size = new Size(InnerW, 26),
            Text = _settings.ApiKey ?? string.Empty,
            BackColor = FluentColors.Surface,
            ForeColor = FluentColors.TextPrimary,
            Font = FluentFonts.Body,
            BorderStyle = BorderStyle.FixedSingle,
            UseSystemPasswordChar = true
        };
        mainPanel.Controls.Add(_apiKeyBox);
        _showKeyBox = new CheckBox
        {
            Text = "  Show key",
            Location = new Point(Pad, y + 52),
            Size = new Size(140, 22),
            ForeColor = FluentColors.TextSecondary,
            Font = FluentFonts.Caption,
            Checked = false
        };
        _showKeyBox.CheckedChanged += (_, _) => _apiKeyBox.UseSystemPasswordChar = !_showKeyBox.Checked;
        mainPanel.Controls.Add(_showKeyBox);
        y += 84;

        // Endpoint row
        mainPanel.Controls.Add(new Label
        {
            Text = "Endpoint (leave empty for OpenAI cloud)",
            ForeColor = FluentColors.TextSecondary,
            Font = FluentFonts.Caption,
            Location = new Point(Pad, y),
            Size = new Size(InnerW, 20)
        });
        _endpointBox = new TextBox
        {
            Location = new Point(Pad, y + 22),
            Size = new Size(InnerW, 26),
            Text = _settings.Endpoint ?? string.Empty,
            BackColor = FluentColors.Surface,
            ForeColor = FluentColors.TextPrimary,
            Font = FluentFonts.Body,
            BorderStyle = BorderStyle.FixedSingle
        };
        mainPanel.Controls.Add(_endpointBox);
        y += 60;

        // Model + Provider ID row (two columns)
        const int gap = 24;
        var colW = (InnerW - gap) / 2;
        mainPanel.Controls.Add(new Label
        {
            Text = "Transcription model",
            ForeColor = FluentColors.TextSecondary,
            Font = FluentFonts.Caption,
            Location = new Point(Pad, y),
            Size = new Size(colW, 20)
        });
        _modelBox = new ComboBox
        {
            Location = new Point(Pad, y + 22),
            Size = new Size(colW, 26),
            DropDownStyle = ComboBoxStyle.DropDown,
            BackColor = FluentColors.Surface,
            ForeColor = FluentColors.TextPrimary,
            FlatStyle = FlatStyle.Flat,
            Font = FluentFonts.Body
        };
        _modelBox.Items.AddRange(new object[]
        {
            "gpt-4o-transcribe",
            "whisper-1",
            "gpt-realtime-whisper",
            "gpt-realtime"
        });
        _modelBox.Text = string.IsNullOrWhiteSpace(_settings.Model) ? "gpt-4o-transcribe" : _settings.Model;
        mainPanel.Controls.Add(_modelBox);

        var col2X = Pad + colW + gap;
        mainPanel.Controls.Add(new Label
        {
            Text = "Provider ID (Ai Nexus, optional)",
            ForeColor = FluentColors.TextSecondary,
            Font = FluentFonts.Caption,
            Location = new Point(col2X, y),
            Size = new Size(colW, 20)
        });
        _providerIdBox = new TextBox
        {
            Location = new Point(col2X, y + 22),
            Size = new Size(colW, 26),
            Text = _settings.ProviderId?.ToString() ?? string.Empty,
            BackColor = FluentColors.Surface,
            ForeColor = FluentColors.TextPrimary,
            Font = FluentFonts.Body,
            BorderStyle = BorderStyle.FixedSingle
        };
        mainPanel.Controls.Add(_providerIdBox);

        presetBox.SelectedIndexChanged += (_, _) =>
        {
            switch (presetBox.SelectedIndex)
            {
                case 1: // OpenAI cloud
                    _endpointBox.Text = string.Empty;
                    if (string.IsNullOrWhiteSpace(_modelBox.Text) || _modelBox.Text == "whisper-1")
                    {
                        _modelBox.Text = "gpt-4o-transcribe";
                    }
                    _providerIdBox.Text = string.Empty;
                    break;
                case 2: // Ai Nexus / on-prem
                    if (string.IsNullOrWhiteSpace(_endpointBox.Text)
                        || _endpointBox.Text.Contains("openai.com"))
                    {
                        _endpointBox.Text = "http://localhost:5141/api/AI/queryAudio";
                    }
                    _modelBox.Text = "gpt-4o-transcribe";
                    break;
            }
        };

        // Button bar
        var buttonPanel = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = ButtonBarH,
            BackColor = FluentColors.Background
        };

        var cancelButton = new Button
        {
            Text = isStartup ? "Quit" : "Cancel",
            Size = new Size(100, 36),
            Location = new Point(FormW - Pad - 100, 14),
            FlatStyle = FlatStyle.Flat,
            ForeColor = FluentColors.TextSecondary,
            BackColor = FluentColors.Surface,
            DialogResult = DialogResult.Cancel,
            Font = FluentFonts.ButtonText,
            Cursor = Cursors.Hand
        };
        cancelButton.FlatAppearance.BorderSize = 1;
        cancelButton.FlatAppearance.BorderColor = FluentColors.Border;
        cancelButton.FlatAppearance.MouseDownBackColor = FluentColors.SurfaceHover;
        cancelButton.FlatAppearance.MouseOverBackColor = FluentColors.SurfaceHover;

        var okButton = new Button
        {
            Text = "Save",
            Size = new Size(100, 36),
            Location = new Point(cancelButton.Left - 12 - 100, 14),
            FlatStyle = FlatStyle.Flat,
            ForeColor = FluentColors.TextPrimary,
            BackColor = FluentColors.AccentPrimary,
            DialogResult = DialogResult.None,
            Font = FluentFonts.ButtonText,
            Cursor = Cursors.Hand
        };
        okButton.FlatAppearance.BorderSize = 0;
        okButton.FlatAppearance.MouseDownBackColor = FluentColors.AccentPressed;
        okButton.FlatAppearance.MouseOverBackColor = FluentColors.AccentHover;
        okButton.Click += OnSaveClicked;

        buttonPanel.Controls.Add(cancelButton);
        buttonPanel.Controls.Add(okButton);

        Controls.Add(mainPanel);
        Controls.Add(titleBar);
        Controls.Add(buttonPanel);
        AcceptButton = okButton;
        CancelButton = cancelButton;

        Load += (_, _) => ApplyModernStyle();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        // Subtle violet 1px border so the form edges stay visible even when Mica
        // is unavailable or the window sits over a similarly dark surface.
        using var borderPen = new Pen(Color.FromArgb(110, 124, 58, 237), 1f);
        e.Graphics.DrawRectangle(borderPen, 0, 0, Width - 1, Height - 1);
    }

    private void OnSaveClicked(object? sender, EventArgs e)
    {
        var apiKey = _apiKeyBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            MessageBox.Show(this,
                "The API key is required.",
                "API Configuration",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            _apiKeyBox.Focus();
            return;
        }

        var endpoint = _endpointBox.Text.Trim();
        if (!string.IsNullOrEmpty(endpoint)
            && !endpoint.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            && !endpoint.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            MessageBox.Show(this,
                "The endpoint must start with http:// or https://, or be left empty to use the OpenAI cloud.",
                "API Configuration",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            _endpointBox.Focus();
            return;
        }

        int? providerId = null;
        var providerIdText = _providerIdBox.Text.Trim();
        if (!string.IsNullOrEmpty(providerIdText))
        {
            if (!int.TryParse(providerIdText, out var parsed))
            {
                MessageBox.Show(this,
                    "The Provider ID must be a whole number, or left empty.",
                    "API Configuration",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                _providerIdBox.Focus();
                return;
            }
            providerId = parsed;
        }

        _settings.ApiKey = apiKey;
        _settings.Endpoint = endpoint;
        var model = _modelBox.Text.Trim();
        if (!string.IsNullOrEmpty(model))
        {
            _settings.Model = model;
        }
        _settings.ProviderId = providerId;

        DialogResult = DialogResult.OK;
        Close();
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
                // Mica backdrop is best-effort; ignored on older Windows.
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
