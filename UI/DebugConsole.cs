using System.Text;
using System.Drawing.Drawing2D;

namespace Koli.UI;

public partial class DebugConsole : Form
{
    private readonly RichTextBox _logTextBox;
    private readonly object _lockObject = new();
    
    // Modern Design Colors (matching MainForm premium theme)
    private static readonly Color Background = Color.FromArgb(14, 14, 18);
    private static readonly Color Surface = Color.FromArgb(24, 24, 32);
    private static readonly Color SurfaceHover = Color.FromArgb(36, 36, 46);
    private static readonly Color AccentPrimary = Color.FromArgb(124, 58, 237);
    private static readonly Color AccentHover = Color.FromArgb(139, 92, 246);
    private static readonly Color TextPrimary = Color.FromArgb(248, 250, 252);
    private static readonly Color TextSecondary = Color.FromArgb(163, 163, 178);
    private static readonly Color Border = Color.FromArgb(42, 42, 56);
    private static readonly Color Success = Color.FromArgb(52, 211, 153);
    private static readonly Color Warning = Color.FromArgb(251, 191, 36);
    private static readonly Color Error = Color.FromArgb(248, 113, 113);
    private static readonly Color Info = Color.FromArgb(129, 140, 248);

    public DebugConsole()
    {
        Text = "Debug Console - Koli";
        Size = new Size(900, 600);
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.Sizable;
        WindowState = FormWindowState.Normal;
        BackColor = Background;
        DoubleBuffered = true;

        // Main container with padding
        var mainContainer = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Background,
            Padding = new Padding(16, 16, 16, 0)
        };

        // Header panel with title
        var headerPanel = new Panel
        {
            Dock = DockStyle.Top,
            Height = 48,
            BackColor = Color.Transparent
        };

        var titleLabel = new Label
        {
            Text = "\uE90F  Debug Console",
            Font = new Font("Segoe UI", 14F, FontStyle.Bold),
            ForeColor = TextPrimary,
            AutoSize = true,
            Location = new Point(0, 10)
        };
        headerPanel.Controls.Add(titleLabel);

        // Log panel with rounded corners effect
        var logContainer = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Surface,
            Padding = new Padding(2)
        };

        _logTextBox = new RichTextBox
        {
            Dock = DockStyle.Fill,
            Font = new Font("Cascadia Code", 9.5F, FontStyle.Regular, GraphicsUnit.Point),
            ReadOnly = true,
            WordWrap = false,
            BackColor = Color.FromArgb(10, 10, 14),
            ForeColor = Color.FromArgb(200, 200, 200),
            BorderStyle = BorderStyle.None,
            ScrollBars = RichTextBoxScrollBars.Both
        };

        logContainer.Controls.Add(_logTextBox);

        // Modern button panel
        var buttonPanel = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 56,
            BackColor = Background,
            Padding = new Padding(0, 12, 0, 8)
        };

        var clearButton = CreateModernButton("\uE74D  Clear", AccentPrimary, true);
        clearButton.Location = new Point(16, 12);
        clearButton.Click += (s, e) => Clear();

        var copyButton = CreateModernButton("\uE8C8  Copy", Surface, false);
        copyButton.Location = new Point(136, 12);
        copyButton.Click += (s, e) =>
        {
            if (_logTextBox.SelectedText.Length > 0)
            {
                Clipboard.SetText(_logTextBox.SelectedText);
                ShowCopyFeedback("Selection copied!");
            }
            else if (_logTextBox.Text.Length > 0)
            {
                Clipboard.SetText(_logTextBox.Text);
                ShowCopyFeedback("All logs copied!");
            }
        };

        var exportButton = CreateModernButton("\uE792  Export", Surface, false);
        exportButton.Location = new Point(256, 12);
        exportButton.Click += (s, e) => ExportLogs();

        buttonPanel.Controls.Add(clearButton);
        buttonPanel.Controls.Add(copyButton);
        buttonPanel.Controls.Add(exportButton);

        mainContainer.Controls.Add(logContainer);
        mainContainer.Controls.Add(headerPanel);

        Controls.Add(mainContainer);
        Controls.Add(buttonPanel);
    }

    private Button CreateModernButton(string text, Color backColor, bool isPrimary)
    {
        var button = new Button
        {
            Text = text,
            Size = new Size(110, 32),
            FlatStyle = FlatStyle.Flat,
            ForeColor = isPrimary ? TextPrimary : TextSecondary,
            BackColor = backColor,
            Cursor = Cursors.Hand,
            Font = new Font("Segoe UI", 9F, FontStyle.Regular)
        };
        
        button.FlatAppearance.BorderSize = isPrimary ? 0 : 1;
        button.FlatAppearance.BorderColor = Border;
        button.FlatAppearance.MouseDownBackColor = isPrimary ? Color.FromArgb(0, 103, 192) : SurfaceHover;
        button.FlatAppearance.MouseOverBackColor = isPrimary ? AccentHover : SurfaceHover;
        
        button.MouseEnter += (s, e) => button.ForeColor = TextPrimary;
        button.MouseLeave += (s, e) => button.ForeColor = isPrimary ? TextPrimary : TextSecondary;
        
        return button;
    }

    private void ShowCopyFeedback(string message)
    {
        // Show a brief tooltip-style feedback
        var feedback = new ToolTip
        {
            IsBalloon = false,
            BackColor = Success,
            ForeColor = TextPrimary
        };
        feedback.Show(message, this, Width / 2 - 50, Height - 80, 1500);
    }

    private void ExportLogs()
    {
        using var dialog = new SaveFileDialog
        {
            Filter = "Text files (*.txt)|*.txt|Log files (*.log)|*.log|All files (*.*)|*.*",
            DefaultExt = "txt",
            FileName = $"Koli_Debug_{DateTime.Now:yyyyMMdd_HHmmss}.txt"
        };

        if (dialog.ShowDialog() == DialogResult.OK)
        {
            try
            {
                File.WriteAllText(dialog.FileName, _logTextBox.Text);
                ShowCopyFeedback("Logs exported!");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error exporting logs: {ex.Message}", "Export Error", 
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }

    public void LogRequest(string method, string url, Dictionary<string, string>? headers = null, string? body = null)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] ===== REQUEST =====");
        sb.AppendLine($"{method} {url}");
        sb.AppendLine();

        if (headers != null && headers.Count > 0)
        {
            sb.AppendLine("Headers:");
            foreach (var header in headers)
            {
                // Hide API key for security
                var value = header.Key.ToLower().Contains("api-key") || header.Key.ToLower().Contains("authorization")
                    ? "***HIDDEN***"
                    : header.Value;
                sb.AppendLine($"  {header.Key}: {value}");
            }
            sb.AppendLine();
        }

        if (!string.IsNullOrEmpty(body))
        {
            sb.AppendLine("Body:");
            sb.AppendLine(body);
            sb.AppendLine();
        }

        sb.AppendLine(new string('─', 80));
        sb.AppendLine();

        AppendColoredLog(sb.ToString(), Info);
    }

    public void LogResponse(int statusCode, string? statusMessage, Dictionary<string, string>? headers = null, string? body = null)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] ===== RESPONSE =====");
        sb.AppendLine($"Status: {statusCode} {statusMessage ?? ""}");
        sb.AppendLine();

        if (headers != null && headers.Count > 0)
        {
            sb.AppendLine("Headers:");
            foreach (var header in headers)
            {
                sb.AppendLine($"  {header.Key}: {header.Value}");
            }
            sb.AppendLine();
        }

        if (!string.IsNullOrEmpty(body))
        {
            sb.AppendLine("Body:");
            // Limit the displayed body size (first 2000 characters)
            var displayBody = body.Length > 2000 ? body.Substring(0, 2000) + $"\n... (truncated, {body.Length} characters total)" : body;
            sb.AppendLine(displayBody);
            sb.AppendLine();
        }

        sb.AppendLine(new string('─', 80));
        sb.AppendLine();

        var color = statusCode >= 200 && statusCode < 300 ? Success : (statusCode >= 400 ? Error : Warning);
        AppendColoredLog(sb.ToString(), color);
    }

    public void LogError(string message, Exception? exception = null)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] ✖ ERROR");
        sb.AppendLine($"Message: {message}");
        
        if (exception != null)
        {
            sb.AppendLine($"Exception: {exception.GetType().Name}");
            sb.AppendLine($"Details: {exception.Message}");
            if (exception.StackTrace != null)
            {
                sb.AppendLine("Stack Trace:");
                sb.AppendLine(exception.StackTrace);
            }
        }

        sb.AppendLine(new string('─', 80));
        sb.AppendLine();

        AppendColoredLog(sb.ToString(), Error);
    }

    public void LogInfo(string message)
    {
        var text = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] ℹ INFO: {message}\n";
        AppendColoredLog(text, Success);
    }

    private void AppendColoredLog(string text, Color color)
    {
        if (InvokeRequired)
        {
            Invoke(new Action<string, Color>(AppendColoredLog), text, color);
            return;
        }

        lock (_lockObject)
        {
            _logTextBox.SelectionStart = _logTextBox.TextLength;
            _logTextBox.SelectionLength = 0;
            _logTextBox.SelectionColor = color;
            _logTextBox.AppendText(text);
            _logTextBox.SelectionColor = _logTextBox.ForeColor;
            _logTextBox.ScrollToCaret();
        }
    }

    public void Clear()
    {
        if (InvokeRequired)
        {
            Invoke(new Action(Clear));
            return;
        }

        _logTextBox.Clear();
    }
}

