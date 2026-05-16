using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Koli.Config;
using Koli.Services;

namespace Koli.UI;

// Modern Design Color Palette - Workspace Glass/Mica Premium
internal static class FluentColors
{
    // Backgrounds - Deep base under Mica
    public static readonly Color Background = Color.FromArgb(14, 14, 18);
    public static readonly Color Surface = Color.FromArgb(24, 24, 32);
    public static readonly Color SurfaceHover = Color.FromArgb(36, 36, 46);
    public static readonly Color SurfaceElevated = Color.FromArgb(32, 32, 42);

    // Workspace layout zones
    public static readonly Color SidebarBg = Color.FromArgb(220, 16, 16, 22);
    public static readonly Color HeaderBg = Color.FromArgb(180, 18, 18, 24);
    public static readonly Color MainContentBg = Color.FromArgb(140, 14, 14, 20);
    public static readonly Color DockBg = Color.FromArgb(220, 16, 16, 22);

    // Card surfaces
    public static readonly Color CardBg = Color.FromArgb(200, 28, 28, 38);
    public static readonly Color CardBgHover = Color.FromArgb(220, 36, 36, 48);
    public static readonly Color CardBorder = Color.FromArgb(60, 124, 58, 237);
    public static readonly Color CardBorderSoft = Color.FromArgb(40, 255, 255, 255);

    // Nav rail items
    public static readonly Color NavItemHover = Color.FromArgb(40, 124, 58, 237);
    public static readonly Color NavItemActive = Color.FromArgb(70, 124, 58, 237);
    public static readonly Color NavIndicator = Color.FromArgb(167, 139, 250);

    // Accents - Violet/Indigo gradient feel
    public static readonly Color AccentPrimary = Color.FromArgb(124, 58, 237);    // Violet-600
    public static readonly Color AccentHover = Color.FromArgb(139, 92, 246);      // Violet-500
    public static readonly Color AccentPressed = Color.FromArgb(109, 40, 217);    // Violet-700
    public static readonly Color AccentSecondary = Color.FromArgb(99, 102, 241);  // Indigo-500
    public static readonly Color AccentGlow = Color.FromArgb(167, 139, 250);      // Violet-400

    // Recording - Vibrant coral/red
    public static readonly Color RecordingPrimary = Color.FromArgb(239, 68, 68);
    public static readonly Color RecordingSecondary = Color.FromArgb(248, 113, 113);
    public static readonly Color RecordingGlow = Color.FromArgb(252, 165, 165);

    // Text - Clear hierarchy
    public static readonly Color TextPrimary = Color.FromArgb(248, 250, 252);
    public static readonly Color TextSecondary = Color.FromArgb(163, 163, 178);
    public static readonly Color TextTertiary = Color.FromArgb(113, 113, 128);
    public static readonly Color TextMuted = Color.FromArgb(85, 85, 100);

    // Status - Modern vibrant tones
    public static readonly Color Success = Color.FromArgb(52, 211, 153);          // Emerald-400
    public static readonly Color Warning = Color.FromArgb(251, 191, 36);          // Amber-400
    public static readonly Color Error = Color.FromArgb(248, 113, 113);           // Red-400

    // Borders - Subtle glass edges
    public static readonly Color Border = Color.FromArgb(42, 42, 56);
    public static readonly Color BorderLight = Color.FromArgb(55, 55, 72);
    public static readonly Color BorderGlow = Color.FromArgb(124, 58, 237, 40);
    public static readonly Color BorderDivider = Color.FromArgb(50, 124, 58, 237);
}

// Modern Font System - Segoe UI Variable for premium feel
internal static class FluentFonts
{
    public static readonly Font Title = new Font("Segoe UI", 13F, FontStyle.Bold);
    public static readonly Font BrandTitle = new Font("Segoe UI Semibold", 11.5F, FontStyle.Bold);
    public static readonly Font BrandSubtitle = new Font("Segoe UI", 8.5F, FontStyle.Regular);
    public static readonly Font Subtitle = new Font("Segoe UI Semibold", 10F, FontStyle.Regular);
    public static readonly Font Body = new Font("Segoe UI", 10F, FontStyle.Regular);
    public static readonly Font BodyLarge = new Font("Segoe UI", 11.5F, FontStyle.Regular);
    public static readonly Font Caption = new Font("Segoe UI", 9F, FontStyle.Regular);
    public static readonly Font CaptionSmall = new Font("Segoe UI", 8F, FontStyle.Regular);
    public static readonly Font ButtonText = new Font("Segoe UI Semibold", 9.5F, FontStyle.Regular);
    public static readonly Font Icons = new Font("Segoe Fluent Icons", 10F, FontStyle.Regular);
    public static readonly Font IconsLarge = new Font("Segoe Fluent Icons", 12F, FontStyle.Regular);
    public static readonly Font IconsXL = new Font("Segoe Fluent Icons", 16F, FontStyle.Regular);
    public static readonly Font NavIcon = new Font("Segoe Fluent Icons", 14F, FontStyle.Regular);
    public static readonly Font StatusBadge = new Font("Segoe UI Semibold", 9F, FontStyle.Regular);
    public static readonly Font SectionLabel = new Font("Segoe UI Semibold", 9F, FontStyle.Bold);
    public static readonly Font CardTitle = new Font("Segoe UI Semibold", 10.5F, FontStyle.Bold);
    public static readonly Font Timer = new Font("Cascadia Mono", 14F, FontStyle.Bold);
    public static readonly Font EmptyStateTitle = new Font("Segoe UI Semibold", 15F, FontStyle.Regular);
    public static readonly Font EmptyStateBody = new Font("Segoe UI", 10.5F, FontStyle.Regular);
}

// Custom ToolStripRenderer for Fluent Design menu styling
internal class FluentMenuRenderer : ToolStripProfessionalRenderer
{
    public FluentMenuRenderer() : base(new FluentMenuColorTable()) { }
    
    protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
    {
        if (e.Item.Selected)
        {
            using (var brush = new SolidBrush(FluentColors.SurfaceHover))
            {
                e.Graphics.FillRectangle(brush, 0, 0, e.Item.Width, e.Item.Height);
            }
        }
        else
        {
            using (var brush = new SolidBrush(FluentColors.Surface))
            {
                e.Graphics.FillRectangle(brush, 0, 0, e.Item.Width, e.Item.Height);
            }
        }
    }
    
    protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
    {
        e.TextColor = e.Item.Selected ? FluentColors.TextPrimary : FluentColors.TextPrimary;
        base.OnRenderItemText(e);
    }
}

internal class FluentMenuColorTable : ProfessionalColorTable
{
    public override Color MenuItemSelected => FluentColors.SurfaceHover;
    public override Color MenuItemBorder => FluentColors.Border;
    public override Color MenuBorder => FluentColors.Border;
    public override Color ToolStripBorder => FluentColors.Border;
    public override Color SeparatorDark => FluentColors.Border;
    public override Color SeparatorLight => FluentColors.BorderLight;
    public override Color ImageMarginGradientBegin => FluentColors.Surface;
    public override Color ImageMarginGradientMiddle => FluentColors.Surface;
    public override Color ImageMarginGradientEnd => FluentColors.Surface;
}

// Animation Helper for smooth transitions
internal static class AnimationHelper
{
    public static async Task AnimateColorAsync(
        Control control,
        Color from,
        Color to,
        int durationMs,
        Action<Color> setter,
        CancellationToken cancellationToken = default)
    {
        var steps = Math.Max(1, durationMs / 16); // ~60fps
        for (int i = 0; i <= steps; i++)
        {
            if (cancellationToken.IsCancellationRequested)
                break;
                
            var progress = (float)i / steps;
            var r = (int)(from.R + (to.R - from.R) * progress);
            var g = (int)(from.G + (to.G - from.G) * progress);
            var b = (int)(from.B + (to.B - from.B) * progress);
            var a = (int)(from.A + (to.A - from.A) * progress);
            
            if (control.InvokeRequired)
            {
                control.BeginInvoke(new Action(() => setter(Color.FromArgb(a, r, g, b))));
            }
            else
            {
                setter(Color.FromArgb(a, r, g, b));
            }
            
            await Task.Delay(16, cancellationToken);
        }
    }
    
    public static Color InterpolateColor(Color from, Color to, float progress)
    {
        progress = Math.Max(0, Math.Min(1, progress));
        var r = (int)(from.R + (to.R - from.R) * progress);
        var g = (int)(from.G + (to.G - from.G) * progress);
        var b = (int)(from.B + (to.B - from.B) * progress);
        var a = (int)(from.A + (to.A - from.A) * progress);
        return Color.FromArgb(a, r, g, b);
    }
}

// Message filter to intercept WM_INPUTLANGCHANGE before it reaches child controls
internal sealed class InputLanguageMessageFilter : IMessageFilter
{
    private readonly MainForm _mainForm;
    private const int WM_INPUTLANGCHANGE = 0x0051;

    public InputLanguageMessageFilter(MainForm mainForm)
    {
        _mainForm = mainForm;
    }

    public bool PreFilterMessage(ref Message m)
    {
        if (m.Msg == WM_INPUTLANGCHANGE)
        {
            // Handle the message in the main form
            _mainForm.HandleInputLanguageChange(m.LParam);
            // Return true to indicate the message has been handled and should not be processed further
            return true;
        }
        return false;
    }
}

public partial class MainForm : Form
{
    private readonly AppSettings _settings;
    private readonly SecureSettingsStore _secureStore;
    private readonly string _configPath;
    private NotifyIcon? _notifyIcon;
    private Button _startStopButton = null!;
    private Button _closeButton = null!;
    private Button _minimizeButton = null!;
    private Button _languageButton = null!;
    private Button _infoButton = null!;
    private Button _settingsButton = null!;
    private ContextMenuStrip _settingsMenu = null!;
    private ToolStripMenuItem _toggleRewriteMenuItem = null!;
    private Button _rewriteToggleButton = null!; // Hidden, for state management
    private ToolTip _rewriteTooltip = null!;
    private Label _statusLabel = null!;
    private Panel _wavePanel = null!;

    // Workspace layout containers
    private Panel _sidebarPanel = null!;
    private Panel _headerPanel = null!;
    private Panel _mainContentPanel = null!;
    private Panel _dockPanel = null!;

    // Header content
    private Label _titleLabel = null!;
    private Panel _logoBadge = null!;

    // Sidebar nav items
    private Button _navHome = null!;
    private Button _navHistory = null!;
    private Button _navMeeting = null!;
    private Button _navDebug = null!;

    // Main content (transcript live + history)
    private Panel _transcriptCard = null!;
    private Label _transcriptCardTitle = null!;
    private Label _transcriptCardBadge = null!;
    private Label _transcriptLabel = null!;
    private Panel _emptyStatePanel = null!;

    // Dock
    private Label _timerLabel = null!;
    private DateTime _recordingStartTime = DateTime.MinValue;
    private System.Windows.Forms.Timer? _timerTickTimer;

    // History (persistent)
    private Panel _historyPanel = null!;
    private FlowLayoutPanel _historyList = null!;
    private Label _historyEmpty = null!;
    private readonly List<TranscriptHistoryEntry> _history = new();
    private const int MaxHistoryEntries = 100;
    private string _historyPath = string.Empty;

    // Embedded debug + meeting hosts
    private Panel _debugHost = null!;
    private Panel _meetingHost = null!;
    private MeetingForm? _embeddedMeetingForm;

    private enum MainView { Home, History, Debug, Meeting }
    private MainView _currentView = MainView.Home;

    private sealed class TranscriptHistoryEntry
    {
        public DateTime Timestamp { get; init; }
        public string Language { get; init; } = "";
        public string Text { get; init; } = "";
    }
    private AudioCaptureService? _audioCapture;
    private SpeechToTextService? _speechToText;
    private SpeechToTextService? _dictationRealtimeStt;
    private Task? _dictationRealtimeTask;
    private bool _dictationUsedRealtime;
    /// <summary>
    /// Number of characters already typed live in the active window for each Realtime utterance
    /// (keyed by item_id). Used to dedupe between accumulated deltas and the final transcript so
    /// we never type the same character twice (Whisper Realtime sends only <c>completed</c>; other
    /// Realtime models send deltas + a final).
    /// </summary>
    private readonly Dictionary<string, int> _realtimeTypedLengthByItem = new(StringComparer.Ordinal);
    /// <summary>True once the live realtime session has typed something in the active window. Used
    /// to skip the final all-at-once paste so we don't duplicate text.</summary>
    private bool _realtimeTypedAnything;
    private CancellationTokenSource? _cancellationTokenSource;
    private bool _isRecording;
    private bool _isPaused;
    private DebugConsole? _debugConsole;
    private float _currentAudioLevel = 0f;
    private System.Windows.Forms.Timer? _waveTimer;
    private System.Windows.Forms.Timer? _languageMonitorTimer;
    private readonly Queue<float> _audioLevelHistory = new Queue<float>();
    private readonly Stopwatch _waveStopwatch = Stopwatch.StartNew();
    private float _waveEnergy = 0f;
    private readonly WaveLayer[] _waveLayers =
    {
        new WaveLayer(
            frequency: 1.55f,
            speed: 0.55f,
            detailFrequency: 3.2f,
            detailAmplitude: 0.35f,
            amplitudeMultiplier: 1.0f,
            verticalShift: -0.05f,
            smoothness: 0.55f,
            outlineWidth: 1.6f,
            fillAlpha: 185,
            glowAlpha: 90,
            outlineAlpha: 220,
            fillColor: Color.FromArgb(124, 58, 237),   // Violet primary
            glowColor: Color.FromArgb(167, 139, 250),   // Violet glow
            outlineColor: Color.FromArgb(196, 181, 253)),// Violet light
        new WaveLayer(
            frequency: 1.1f,
            speed: 0.32f,
            detailFrequency: 2.2f,
            detailAmplitude: 0.28f,
            amplitudeMultiplier: 0.75f,
            verticalShift: 0.08f,
            smoothness: 0.6f,
            outlineWidth: 1.1f,
            fillAlpha: 150,
            glowAlpha: 50,
            outlineAlpha: 170,
            fillColor: Color.FromArgb(99, 102, 241),    // Indigo
            glowColor: Color.FromArgb(129, 140, 248),   // Indigo light
            outlineColor: Color.FromArgb(165, 180, 252)),// Indigo lighter
        new WaveLayer(
            frequency: 2.3f,
            speed: 0.85f,
            detailFrequency: 5.1f,
            detailAmplitude: 0.22f,
            amplitudeMultiplier: 0.5f,
            verticalShift: -0.2f,
            smoothness: 0.5f,
            outlineWidth: 1f,
            fillAlpha: 120,
            glowAlpha: 0,
            outlineAlpha: 120,
            fillColor: Color.FromArgb(79, 70, 229),     // Indigo deep
            glowColor: Color.FromArgb(139, 92, 246),    // Violet medium
            outlineColor: Color.FromArgb(196, 181, 253)) // Violet pale
    };
    private const int MaxHistorySize = 360;
    private StringBuilder _accumulatedTranscription = new StringBuilder();
    private InputLanguageMessageFilter? _messageFilter;
    private CursorOverlay? _cursorOverlay;
    private IntPtr _targetWindowForTyping = IntPtr.Zero; // Window that had focus when recording started
    
    // DWM APIs for modern window effects
    [System.Runtime.InteropServices.DllImport("dwmapi.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode, PreserveSig = false)]
    private static extern void DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int pvAttribute, int cbAttribute);
    
    [System.Runtime.InteropServices.DllImport("dwmapi.dll")]
    private static extern int DwmExtendFrameIntoClientArea(IntPtr hWnd, ref MARGINS pMarInset);
    
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    private struct MARGINS
    {
        public int Left, Right, Top, Bottom;
    }
    
    private const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
    private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
    private const int DWMWA_SYSTEMBACKDROP_TYPE = 38;
    private const int DWMWCP_ROUND = 2;
    private const int DWMSBT_MAINWINDOW = 2;  // Mica
    private const int DWMSBT_TABBEDWINDOW = 4; // Mica Alt

    public MainForm(AppSettings settings, SecureSettingsStore secureStore, string configPath)
    {
        _settings = settings;
        _secureStore = secureStore;
        _configPath = configPath;
        _historyPath = Path.Combine(
            Path.GetDirectoryName(configPath) ?? AppDomain.CurrentDomain.BaseDirectory,
            "history.json");
        LoadHistory();
        InitializeComponent();
        InitializeSystemTray();
    }

    private void LoadHistory()
    {
        try
        {
            if (!File.Exists(_historyPath)) return;
            var json = File.ReadAllText(_historyPath);
            if (string.IsNullOrWhiteSpace(json)) return;
            var loaded = System.Text.Json.JsonSerializer.Deserialize<List<TranscriptHistoryEntry>>(json);
            if (loaded != null)
            {
                _history.Clear();
                _history.AddRange(loaded);
                // Cap at MaxHistoryEntries (in case file is bloated)
                while (_history.Count > MaxHistoryEntries) _history.RemoveAt(_history.Count - 1);
            }
        }
        catch
        {
            // Ignore corrupted history file - the user will start fresh.
        }
    }

    private void SaveHistory()
    {
        try
        {
            var dir = Path.GetDirectoryName(_historyPath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            var json = System.Text.Json.JsonSerializer.Serialize(_history,
                new System.Text.Json.JsonSerializerOptions { WriteIndented = false });
            File.WriteAllText(_historyPath, json);
        }
        catch
        {
            // Ignore I/O errors - history is best-effort.
        }
    }

    private static Icon? LoadEmbeddedIcon()
    {
        try
        {
            var assembly = Assembly.GetExecutingAssembly();
            // Try different possible resource names
            var possibleNames = new[]
            {
                "Koli.Resources.Koli.ico",
                "Resources.Koli.ico",
                "Koli.ico"
            };

            foreach (var resourceName in possibleNames)
            {
                using (var stream = assembly.GetManifestResourceStream(resourceName))
                {
                    if (stream != null)
                    {
                        return new Icon(stream);
                    }
                }
            }
        }
        catch
        {
            // Ignore errors
        }
        return null;
    }

    // Workspace layout constants - compact workspace
    private const int WindowWidth = 720;
    private const int WindowHeight = 480;
    private const int SidebarWidth = 56;
    private const int HeaderHeight = 48;
    private const int DockHeight = 78;
    private const int RecordButtonSize = 58;

    private void InitializeComponent()
    {
        Text = "Koli";
        var icon = LoadEmbeddedIcon();
        if (icon != null)
        {
            Icon = icon;
        }

        Size = new Size(WindowWidth, WindowHeight);
        MinimumSize = new Size(820, 540);
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.None;
        MaximizeBox = true;
        MinimizeBox = true;
        DoubleBuffered = true;
        BackColor = FluentColors.Background;
        KeyPreview = true;

        ApplyModernWindowStyle();
        ApplyRoundedFormRegion();

        // Initialize controls
        InitializeSettingsMenu();
        BuildRecordButton();
        BuildLanguageChip();
        BuildHiddenRewriteButton();
        BuildWindowControls();
        BuildHeaderPanel();
        BuildSidebarPanel();
        BuildDockPanel();
        BuildMainContentPanel();

        // Add containers in z-order (bottom dock first, sidebar/main fill remainder)
        Controls.Add(_mainContentPanel); // Fill - added first
        Controls.Add(_dockPanel);        // Bottom
        Controls.Add(_sidebarPanel);     // Left
        Controls.Add(_headerPanel);      // Top
        Controls.Add(_rewriteToggleButton); // Hidden

        _rewriteTooltip = new ToolTip();
        UpdateRewriteButtonState();

        // Resize + close handlers
        Resize += MainForm_Resize;
        FormClosing += MainForm_FormClosing;
        KeyDown += MainForm_KeyDown;

        // Global hotkeys (F9, F7, F6)
        RegisterHotKey(Handle, HOTKEY_ID, MOD_NONE, VK_F9);
        RegisterHotKey(Handle, HOTKEY_ID_F7, MOD_NONE, VK_F7);
        RegisterHotKey(Handle, HOTKEY_ID_F6, MOD_NONE, VK_F6);

        _messageFilter = new InputLanguageMessageFilter(this);
        Application.AddMessageFilter(_messageFilter);

        // Initialize language monitoring
        if (_settings.AzureOpenAI.LanguageMode == "Auto")
        {
            UpdateLanguageFromKeyboard();
            _languageMonitorTimer = new System.Windows.Forms.Timer { Interval = 500 };
            _languageMonitorTimer.Tick += (_, _) => UpdateLanguageFromKeyboard();
            _languageMonitorTimer.Start();
        }
        else
        {
            _settings.AzureOpenAI.Language = _settings.AzureOpenAI.ManualLanguage;
            _languageMonitorTimer = new System.Windows.Forms.Timer { Interval = 500 };
            _languageMonitorTimer.Tick += (_, _) => UpdateLanguageFromKeyboard();
        }

        // Recording timer (updates dock timer label)
        _timerTickTimer = new System.Windows.Forms.Timer { Interval = 250 };
        _timerTickTimer.Tick += (_, _) => UpdateTimerLabel();

        // Force an initial layout pass so anchored right-cluster controls land in the right place
        // even before the OS fires the first Resize event.
        MainForm_Resize(this, EventArgs.Empty);
    }

    private void ApplyRoundedFormRegion()
    {
        var path = new GraphicsPath();
        path.AddArc(0, 0, 24, 24, 180, 90);
        path.AddArc(Width - 24, 0, 24, 24, 270, 90);
        path.AddArc(Width - 24, Height - 24, 24, 24, 0, 90);
        path.AddArc(0, Height - 24, 24, 24, 90, 90);
        path.CloseAllFigures();
        Region?.Dispose();
        Region = new Region(path);
    }

    // -----------------------------------------------------------------------
    // HEADER PANEL  - drag region, logo+title (left), language chip + window controls (right)
    // -----------------------------------------------------------------------
    private void BuildHeaderPanel()
    {
        _headerPanel = new Panel
        {
            Dock = DockStyle.Top,
            Height = HeaderHeight,
            BackColor = Color.Transparent
        };
        _headerPanel.Paint += (s, e) =>
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            // Subtle violet tint band
            using (var brush = new LinearGradientBrush(
                new Rectangle(0, 0, _headerPanel.Width, HeaderHeight),
                Color.FromArgb(28, 124, 58, 237),
                Color.FromArgb(0, 124, 58, 237),
                LinearGradientMode.Vertical))
            {
                g.FillRectangle(brush, 0, 0, _headerPanel.Width, HeaderHeight);
            }

            // 1px hairline divider at bottom
            using (var pen = new Pen(Color.FromArgb(50, 124, 58, 237), 1f))
            {
                g.DrawLine(pen, 0, HeaderHeight - 1, _headerPanel.Width, HeaderHeight - 1);
            }
        };

        // Drag the form via the header
        _headerPanel.MouseDown += (s, e) =>
        {
            if (e.Button == MouseButtons.Left)
            {
                ReleaseCapture();
                SendMessage(Handle, WM_NCLBUTTONDOWN, HT_CAPTION, 0);
            }
        };

        // Logo badge (small violet rounded square with mic glyph) - vertically centered in 48px header
        _logoBadge = new Panel
        {
            Size = new Size(28, 28),
            Location = new Point(14, (HeaderHeight - 28) / 2),
            BackColor = Color.Transparent
        };
        _logoBadge.Paint += (s, e) =>
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            var rect = new Rectangle(0, 0, 28, 28);
            using (var path = GetRoundedRectPath(rect, 8))
            using (var brush = new LinearGradientBrush(rect,
                FluentColors.AccentHover,
                FluentColors.AccentSecondary,
                LinearGradientMode.ForwardDiagonal))
            {
                g.FillPath(brush, path);
            }

            using (var glowPen = new Pen(Color.FromArgb(120, 167, 139, 250), 1f))
            using (var glowPath = GetRoundedRectPath(rect, 8))
            {
                g.DrawPath(glowPen, glowPath);
            }

            // Mic glyph - precisely centered (NoPadding neutralizes Segoe Fluent bearings)
            DrawCenteredGlyph(g, "\uE720", FluentFonts.Icons, Color.White, rect);
        };

        // Title text (single-line, vertically centered)
        _titleLabel = new Label
        {
            Text = "Koli",
            Font = FluentFonts.BrandTitle,
            ForeColor = FluentColors.TextPrimary,
            BackColor = Color.Transparent,
            Location = new Point(50, (HeaderHeight - 20) / 2),
            AutoSize = true
        };
        Label? subtitle = null; // subtitle removed \u2014 header is now 48px tall

        // Header right cluster (lang chip + settings + minimize + close)
        BuildHeaderRightCluster();

        _headerPanel.Controls.Add(_logoBadge);
        _headerPanel.Controls.Add(_titleLabel);
        if (subtitle != null) _headerPanel.Controls.Add(subtitle);
        _headerPanel.Controls.Add(_languageButton);
        _headerPanel.Controls.Add(_infoButton);
        _headerPanel.Controls.Add(_settingsButton);
        _headerPanel.Controls.Add(_minimizeButton);
        _headerPanel.Controls.Add(_closeButton);

        ApplyRoundedCorners(_infoButton, 8);
        ApplyRoundedCorners(_settingsButton, 8);
        ApplyRoundedCorners(_closeButton, 8);
        ApplyRoundedCorners(_minimizeButton, 8);
        ApplyRoundedCorners(_languageButton, 15);
    }

    private void BuildHeaderRightCluster()
    {
        // Positions are computed in MainForm_Resize against the actual header width.
        // We don't set Anchor here because it would lock in a wrong "distance from right"
        // computed against the panel's default (un-docked) width.
        _languageButton.Location = new Point(WindowWidth - 236, 9);
        _infoButton.Location = new Point(WindowWidth - 158, 8);
        _settingsButton.Location = new Point(WindowWidth - 122, 8);
        _minimizeButton.Location = new Point(WindowWidth - 86, 8);
        _closeButton.Location = new Point(WindowWidth - 46, 8);
    }

    // -----------------------------------------------------------------------
    // SIDEBAR RAIL - vertical nav, icon-only, violet active indicator
    // -----------------------------------------------------------------------
    private void BuildSidebarPanel()
    {
        _sidebarPanel = new Panel
        {
            Dock = DockStyle.Left,
            Width = SidebarWidth,
            BackColor = Color.Transparent
        };
        _sidebarPanel.Paint += (s, e) =>
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            // Glass surface
            using (var brush = new SolidBrush(FluentColors.SidebarBg))
            {
                g.FillRectangle(brush, 0, 0, _sidebarPanel.Width, _sidebarPanel.Height);
            }

            // Right hairline
            using (var pen = new Pen(Color.FromArgb(40, 124, 58, 237), 1f))
            {
                g.DrawLine(pen, _sidebarPanel.Width - 1, 0, _sidebarPanel.Width - 1, _sidebarPanel.Height);
            }
        };

        const int navIconSize = 40;
        var navYStart = 14; // sidebar local coords; top of sidebar = right below header
        var navStride = 48;

        _navHome = CreateNavItem("\uE80F", "Home / Live", navYStart, navIconSize, true);
        _navHistory = CreateNavItem("\uE81C", "Recent transcripts", navYStart + navStride, navIconSize, false);
        _navMeeting = CreateNavItem("\uE8BC", "Meeting mode", navYStart + navStride * 2, navIconSize, false);
        _navDebug = CreateNavItem("\uE9F5", "Debug console", navYStart + navStride * 3, navIconSize, false);

        _navHome.Click += (s, e) => { SetActiveNav(_navHome); SwitchMainView(MainView.Home); };
        _navHistory.Click += (s, e) => { SetActiveNav(_navHistory); SwitchMainView(MainView.History); };
        _navMeeting.Click += (s, e) => { SetActiveNav(_navMeeting); SwitchMainView(MainView.Meeting); };
        _navDebug.Click += (s, e) => { SetActiveNav(_navDebug); SwitchMainView(MainView.Debug); };

        _sidebarPanel.Controls.Add(_navHome);
        _sidebarPanel.Controls.Add(_navHistory);
        _sidebarPanel.Controls.Add(_navMeeting);
        _sidebarPanel.Controls.Add(_navDebug);
    }

    private Button CreateNavItem(string glyph, string tooltip, int y, int size, bool active)
    {
        var btn = new Button
        {
            Text = string.Empty, // Glyph painted via MakeIconButton
            Font = FluentFonts.NavIcon,
            Size = new Size(size, size),
            Location = new Point((SidebarWidth - size) / 2, y),
            FlatStyle = FlatStyle.Flat,
            ForeColor = active ? FluentColors.NavIndicator : FluentColors.TextSecondary,
            BackColor = active ? FluentColors.NavItemActive : Color.Transparent,
            Cursor = Cursors.Hand,
            TabStop = false,
            Tag = active ? "active" : "inactive",
            TextAlign = ContentAlignment.MiddleCenter,
            Padding = Padding.Empty,
            Margin = Padding.Empty,
            AutoSize = false
        };
        btn.FlatAppearance.BorderSize = 0;
        btn.FlatAppearance.MouseOverBackColor = active ? FluentColors.NavItemActive : FluentColors.NavItemHover;
        btn.FlatAppearance.MouseDownBackColor = FluentColors.NavItemActive;
        ApplyRoundedCorners(btn, 10);
        MakeIconButton(btn, glyph, FluentFonts.NavIcon);

        var tip = new ToolTip();
        tip.SetToolTip(btn, tooltip);

        btn.MouseEnter += (s, e) =>
        {
            if (btn.Tag as string != "active")
            {
                btn.ForeColor = FluentColors.TextPrimary;
                btn.Invalidate();
            }
        };
        btn.MouseLeave += (s, e) =>
        {
            if (btn.Tag as string != "active")
            {
                btn.ForeColor = FluentColors.TextSecondary;
                btn.Invalidate();
            }
        };
        return btn;
    }

    private void SetActiveNav(Button active)
    {
        foreach (var b in new[] { _navHome, _navHistory, _navMeeting, _navDebug })
        {
            var isActive = b == active;
            b.Tag = isActive ? "active" : "inactive";
            b.ForeColor = isActive ? FluentColors.NavIndicator : FluentColors.TextSecondary;
            b.BackColor = isActive ? FluentColors.NavItemActive : Color.Transparent;
            b.FlatAppearance.MouseOverBackColor = isActive ? FluentColors.NavItemActive : FluentColors.NavItemHover;
            b.Invalidate();
        }
    }

    private void ShowHistoryMessage()
    {
        // Placeholder: history view will live in main content. For now just refocus Home.
        SetActiveNav(_navHome);
    }

    // -----------------------------------------------------------------------
    // DOCK PANEL - record button + waveform + timer + status
    // -----------------------------------------------------------------------
    private void BuildDockPanel()
    {
        _dockPanel = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = DockHeight,
            BackColor = Color.Transparent
        };
        _dockPanel.Paint += (s, e) =>
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (var brush = new SolidBrush(FluentColors.DockBg))
                g.FillRectangle(brush, 0, 0, _dockPanel.Width, _dockPanel.Height);
            using (var pen = new Pen(Color.FromArgb(50, 124, 58, 237), 1f))
                g.DrawLine(pen, 0, 0, _dockPanel.Width, 0);
        };

        // Position the record button on the left of the dock
        _startStopButton.Size = new Size(RecordButtonSize, RecordButtonSize);
        _startStopButton.Location = new Point(20, (DockHeight - RecordButtonSize) / 2);
        _startStopButton.Anchor = AnchorStyles.Left | AnchorStyles.Top;

        // Wave panel - centered, fills the middle
        _wavePanel.Dock = DockStyle.None;
        _wavePanel.Visible = true; // always visible (idle baseline)
        _wavePanel.Height = 56;
        _wavePanel.Location = new Point(110, (DockHeight - 56) / 2);
        _wavePanel.Size = new Size(_dockPanel.Width - 110 - 220, 56);
        _wavePanel.Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top;

        // Timer label (right-of-wave)
        _timerLabel = new Label
        {
            Text = "00:00",
            Font = FluentFonts.Timer,
            ForeColor = FluentColors.TextSecondary,
            BackColor = Color.Transparent,
            AutoSize = false,
            TextAlign = ContentAlignment.MiddleRight,
            Size = new Size(86, 30),
            Anchor = AnchorStyles.Right | AnchorStyles.Top
        };

        // Status pill (right edge)
        _statusLabel.Dock = DockStyle.None;
        _statusLabel.Text = "Ready";
        _statusLabel.Font = FluentFonts.StatusBadge;
        _statusLabel.ForeColor = FluentColors.AccentGlow;
        _statusLabel.BackColor = Color.Transparent;
        _statusLabel.TextAlign = ContentAlignment.MiddleCenter;
        _statusLabel.AutoSize = false;
        _statusLabel.Size = new Size(108, 26);
        _statusLabel.Anchor = AnchorStyles.Right | AnchorStyles.Top;

        // Right-align timer + status
        _timerLabel.Location = new Point(_dockPanel.Width - 200, (DockHeight - 30) / 2 - 8);
        _statusLabel.Location = new Point(_dockPanel.Width - 124, (DockHeight - 26) / 2 + 14);

        _dockPanel.Controls.Add(_startStopButton);
        _dockPanel.Controls.Add(_wavePanel);
        _dockPanel.Controls.Add(_timerLabel);
        _dockPanel.Controls.Add(_statusLabel);
    }

    // -----------------------------------------------------------------------
    // MAIN CONTENT - transcript card + empty state
    // -----------------------------------------------------------------------
    private void BuildMainContentPanel()
    {
        _mainContentPanel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.Transparent,
            Padding = new Padding(24, 18, 24, 18)
        };

        BuildTranscriptCard();
        BuildEmptyState();
        BuildHistoryPanel();
        BuildEmbedHosts();

        _mainContentPanel.Controls.Add(_meetingHost);
        _mainContentPanel.Controls.Add(_debugHost);
        _mainContentPanel.Controls.Add(_historyPanel);
        _mainContentPanel.Controls.Add(_transcriptCard);
        _mainContentPanel.Controls.Add(_emptyStatePanel);

        // Show empty state by default
        _transcriptCard.Visible = false;
        _historyPanel.Visible = false;
        _debugHost.Visible = false;
        _meetingHost.Visible = false;
        _emptyStatePanel.Visible = true;
    }

    private void BuildEmbedHosts()
    {
        _debugHost = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.Transparent,
            Padding = new Padding(0)
        };
        _meetingHost = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.Transparent,
            Padding = new Padding(0)
        };
    }

    private void BuildHistoryPanel()
    {
        _historyPanel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.Transparent,
            Padding = new Padding(0, 0, 0, 0)
        };

        var header = new Label
        {
            Text = "Recent transcripts",
            Font = FluentFonts.CardTitle,
            ForeColor = FluentColors.TextPrimary,
            BackColor = Color.Transparent,
            Dock = DockStyle.Top,
            Height = 32,
            Padding = new Padding(4, 6, 0, 0)
        };

        _historyList = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoScroll = true,
            BackColor = Color.Transparent,
            Padding = new Padding(0, 4, 4, 4)
        };

        _historyEmpty = new Label
        {
            Text = "No transcripts yet.\nStart a recording with F9 or the microphone — it will appear here.",
            Font = FluentFonts.EmptyStateBody,
            ForeColor = FluentColors.TextSecondary,
            BackColor = Color.Transparent,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter
        };

        _historyPanel.Controls.Add(_historyList);
        _historyPanel.Controls.Add(_historyEmpty);
        _historyPanel.Controls.Add(header);
        RefreshHistoryPanel();
    }

    private void RefreshHistoryPanel()
    {
        if (_historyList == null) return;
        _historyList.SuspendLayout();
        _historyList.Controls.Clear();

        if (_history.Count == 0)
        {
            _historyEmpty.Visible = true;
            _historyList.Visible = false;
        }
        else
        {
            _historyEmpty.Visible = false;
            _historyList.Visible = true;
            foreach (var entry in _history)
            {
                _historyList.Controls.Add(BuildHistoryCard(entry));
            }
        }
        _historyList.ResumeLayout();
    }

    private Panel BuildHistoryCard(TranscriptHistoryEntry entry)
    {
        var card = new Panel
        {
            Width = _historyList.ClientSize.Width - 24,
            Height = 88,
            Margin = new Padding(0, 0, 0, 8),
            BackColor = Color.Transparent,
            Cursor = Cursors.Hand,
            Tag = entry
        };
        card.Paint += (s, e) =>
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var rect = new Rectangle(0, 0, card.Width - 1, card.Height - 1);
            using (var path = GetRoundedRectPath(rect, 10))
            using (var brush = new SolidBrush(FluentColors.CardBg))
            {
                g.FillPath(brush, path);
            }
            using (var path = GetRoundedRectPath(rect, 10))
            using (var pen = new Pen(FluentColors.CardBorder, 1f))
            {
                g.DrawPath(pen, path);
            }
        };

        var when = new Label
        {
            Text = entry.Timestamp.ToLocalTime().ToString("HH:mm"),
            Font = FluentFonts.SectionLabel,
            ForeColor = FluentColors.AccentGlow,
            BackColor = Color.Transparent,
            AutoSize = true,
            Location = new Point(14, 10)
        };

        var lang = new Label
        {
            Text = (entry.Language ?? "").ToUpperInvariant(),
            Font = FluentFonts.CaptionSmall,
            ForeColor = FluentColors.TextSecondary,
            BackColor = Color.Transparent,
            AutoSize = true,
            Location = new Point(60, 12)
        };

        var preview = new Label
        {
            Text = entry.Text.Length > 200 ? entry.Text[..200] + "…" : entry.Text,
            Font = FluentFonts.Body,
            ForeColor = FluentColors.TextPrimary,
            BackColor = Color.Transparent,
            AutoSize = false,
            Size = new Size(card.Width - 28, 50),
            Location = new Point(14, 32)
        };

        // Click to copy
        var copyHandler = new EventHandler((s, e) =>
        {
            try
            {
                Clipboard.SetText(entry.Text);
                ShowToastNotification("Copied", "Transcript copied to clipboard", ToolTipIcon.Info);
            }
            catch { }
        });
        card.Click += copyHandler;
        preview.Click += copyHandler;
        when.Click += copyHandler;
        lang.Click += copyHandler;

        card.Controls.Add(when);
        card.Controls.Add(lang);
        card.Controls.Add(preview);
        return card;
    }

    private void PushHistoryEntry(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return;
        _history.Insert(0, new TranscriptHistoryEntry
        {
            Timestamp = DateTime.UtcNow,
            Language = _settings.AzureOpenAI.Language ?? "",
            Text = text.Trim()
        });
        if (_history.Count > MaxHistoryEntries)
            _history.RemoveAt(_history.Count - 1);
        RefreshHistoryPanel();
        SaveHistory();
    }

    private void SwitchMainView(MainView view)
    {
        _currentView = view;
        if (_mainContentPanel == null) return;

        // Hide everything first
        _transcriptCard.Visible = false;
        _emptyStatePanel.Visible = false;
        _historyPanel.Visible = false;
        _debugHost.Visible = false;
        _meetingHost.Visible = false;

        switch (view)
        {
            case MainView.History:
                _historyPanel.Visible = true;
                break;

            case MainView.Debug:
                EnsureDebugEmbedded();
                _debugHost.Visible = true;
                break;

            case MainView.Meeting:
                EnsureMeetingEmbedded();
                _meetingHost.Visible = true;
                break;

            case MainView.Home:
            default:
                if (_isRecording || _accumulatedTranscription.Length > 0)
                    _transcriptCard.Visible = true;
                else
                    _emptyStatePanel.Visible = true;
                break;
        }
    }

    private void EnsureDebugEmbedded()
    {
        if (_debugConsole != null && !_debugConsole.IsDisposed)
        {
            if (_debugConsole.Parent != _debugHost)
            {
                _debugConsole.Parent = _debugHost;
            }
            return;
        }

        _debugConsole = new DebugConsole();
        _debugConsole.TopLevel = false;
        _debugConsole.FormBorderStyle = FormBorderStyle.None;
        _debugConsole.Dock = DockStyle.Fill;
        _debugConsole.ControlBox = false;
        _debugHost.Controls.Add(_debugConsole);
        _debugConsole.Show();

        // Connect logging streams (matching old DebugButton_Click behavior)
        if (_speechToText != null)
        {
            _speechToText.RequestLogging += OnRequestLogging;
            _speechToText.ResponseLogging += OnResponseLogging;
            _speechToText.ErrorLogging += OnErrorLogging;
        }
    }

    private async void EnsureMeetingEmbedded()
    {
        if (_isRecording)
        {
            MessageBox.Show(this, "Please stop dictation before starting a meeting.", "Dictation Active",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            SetActiveNav(_navHome);
            SwitchMainView(MainView.Home);
            return;
        }

        if (_embeddedMeetingForm != null && !_embeddedMeetingForm.IsDisposed)
        {
            if (_embeddedMeetingForm.Parent != _meetingHost)
                _embeddedMeetingForm.Parent = _meetingHost;
            return;
        }

        try
        {
            var apiKey = await _secureStore.ResolveApiKeyAsync(_settings.AzureOpenAI.ApiKey, CancellationToken.None);
            _embeddedMeetingForm = new MeetingForm(_settings, apiKey);
            _embeddedMeetingForm.TopLevel = false;
            _embeddedMeetingForm.FormBorderStyle = FormBorderStyle.None;
            _embeddedMeetingForm.Dock = DockStyle.Fill;
            _embeddedMeetingForm.ControlBox = false;
            _meetingHost.Controls.Add(_embeddedMeetingForm);
            _embeddedMeetingForm.Show();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Failed to open meeting mode: {ex.Message}", "Error",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
            SetActiveNav(_navHome);
            SwitchMainView(MainView.Home);
        }
    }

    private void BuildTranscriptCard()
    {
        _transcriptCard = new Panel
        {
            BackColor = Color.Transparent,
            Dock = DockStyle.Fill,
            Padding = new Padding(24, 20, 24, 20)
        };
        _transcriptCard.Paint += (s, e) =>
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var rect = new Rectangle(0, 0, _transcriptCard.Width - 1, _transcriptCard.Height - 1);
            using (var path = GetRoundedRectPath(rect, 14))
            using (var brush = new SolidBrush(FluentColors.CardBg))
            {
                g.FillPath(brush, path);
            }
            using (var path = GetRoundedRectPath(rect, 14))
            using (var pen = new Pen(FluentColors.CardBorder, 1f))
            {
                g.DrawPath(pen, path);
            }
            // Inner top highlight for glass feel
            using (var path = GetRoundedRectPath(new Rectangle(1, 1, rect.Width - 2, rect.Height - 2), 13))
            using (var pen = new Pen(FluentColors.CardBorderSoft, 1f))
            {
                g.DrawPath(pen, path);
            }
        };

        // Card header row (title + live badge)
        _transcriptCardTitle = new Label
        {
            Text = "Live transcript",
            Font = FluentFonts.CardTitle,
            ForeColor = FluentColors.TextPrimary,
            BackColor = Color.Transparent,
            AutoSize = true,
            Location = new Point(24, 18)
        };

        _transcriptCardBadge = new Label
        {
            Text = "  IDLE  ",
            Font = FluentFonts.CaptionSmall,
            ForeColor = FluentColors.TextTertiary,
            BackColor = Color.Transparent,
            AutoSize = false,
            Size = new Size(64, 22),
            TextAlign = ContentAlignment.MiddleCenter,
            Anchor = AnchorStyles.Top | AnchorStyles.Right
        };
        _transcriptCardBadge.Paint += (s, e) =>
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var rect = new Rectangle(0, 0, _transcriptCardBadge.Width - 1, _transcriptCardBadge.Height - 1);
            var bg = _isRecording ? Color.FromArgb(60, 239, 68, 68) : Color.FromArgb(40, 124, 58, 237);
            using (var path = GetRoundedRectPath(rect, 10))
            using (var brush = new SolidBrush(bg))
            {
                g.FillPath(brush, path);
            }
        };

        // Transcript text body
        _transcriptLabel = new Label
        {
            Text = "",
            Font = FluentFonts.BodyLarge,
            ForeColor = FluentColors.TextPrimary,
            BackColor = Color.Transparent,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.TopLeft,
            Padding = new Padding(0, 48, 0, 0),
            AutoEllipsis = false,
            UseCompatibleTextRendering = false
        };

        _transcriptCard.Controls.Add(_transcriptLabel);
        _transcriptCard.Controls.Add(_transcriptCardTitle);
        _transcriptCard.Controls.Add(_transcriptCardBadge);
        _transcriptCard.Resize += (s, e) =>
        {
            _transcriptCardBadge.Location = new Point(_transcriptCard.Width - _transcriptCardBadge.Width - 24, 18);
        };
    }

    private void BuildEmptyState()
    {
        _emptyStatePanel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.Transparent
        };

        // Custom-painted panel instead of Label, so we can apply NoPadding centering.
        var iconFont = new Font("Segoe Fluent Icons", 38F, FontStyle.Regular);
        var iconLabel = new Panel
        {
            BackColor = Color.Transparent,
            Dock = DockStyle.Top,
            Height = 80
        };
        iconLabel.Paint += (s, e) =>
        {
            DrawCenteredGlyph(e.Graphics, "\uE720", iconFont, FluentColors.AccentGlow,
                new Rectangle(0, 0, iconLabel.Width, iconLabel.Height));
        };

        var title = new Label
        {
            Text = "Ready to transcribe",
            Font = FluentFonts.EmptyStateTitle,
            ForeColor = FluentColors.TextPrimary,
            BackColor = Color.Transparent,
            AutoSize = false,
            TextAlign = ContentAlignment.MiddleCenter,
            Dock = DockStyle.Top,
            Height = 28
        };

        var body = new Label
        {
            Text = "Press F9 or click the microphone to start.\nText is typed live into the active window.",
            Font = FluentFonts.EmptyStateBody,
            ForeColor = FluentColors.TextSecondary,
            BackColor = Color.Transparent,
            AutoSize = false,
            TextAlign = ContentAlignment.TopCenter,
            Dock = DockStyle.Top,
            Height = 38
        };

        // Shortcuts row
        var shortcutsRow = new Panel
        {
            Dock = DockStyle.Top,
            Height = 44,
            BackColor = Color.Transparent
        };
        shortcutsRow.Paint += (s, e) =>
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            DrawShortcutChip(g, shortcutsRow.Width / 2 - 155, 8, "F9", "Start/Stop");
            DrawShortcutChip(g, shortcutsRow.Width / 2 - 28, 8, "F6", "Pause");
            DrawShortcutChip(g, shortcutsRow.Width / 2 + 88, 8, "F7", "Cancel");
        };

        _emptyStatePanel.Controls.Add(shortcutsRow);
        _emptyStatePanel.Controls.Add(body);
        _emptyStatePanel.Controls.Add(title);
        _emptyStatePanel.Controls.Add(iconLabel);
    }

    private static void DrawShortcutChip(Graphics g, int x, int y, string key, string label)
    {
        var keyRect = new Rectangle(x, y, 36, 28);
        using (var path = GetRoundedRectPath(keyRect, 7))
        using (var brush = new SolidBrush(Color.FromArgb(160, 28, 28, 38)))
        using (var pen = new Pen(Color.FromArgb(80, 124, 58, 237), 1f))
        {
            g.FillPath(brush, path);
            g.DrawPath(pen, path);
        }
        TextRenderer.DrawText(g, key, FluentFonts.SectionLabel, keyRect,
            FluentColors.AccentGlow, Color.Transparent,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        TextRenderer.DrawText(g, label, FluentFonts.CaptionSmall,
            new Rectangle(x + 40, y, 100, 28),
            FluentColors.TextSecondary, Color.Transparent,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
    }

    // -----------------------------------------------------------------------
    // RECORD BUTTON  (now responsive to its size, scales paint)
    // -----------------------------------------------------------------------
    private void BuildRecordButton()
    {
        _startStopButton = new NoFocusCueButton
        {
            Size = new Size(RecordButtonSize, RecordButtonSize),
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.Transparent,
            Cursor = Cursors.Hand,
            Text = "",
            TabStop = false
        };
        _startStopButton.Click += StartStopButton_Click;
        _startStopButton.FlatAppearance.BorderSize = 0;
        _startStopButton.FlatAppearance.MouseDownBackColor = Color.Transparent;
        _startStopButton.FlatAppearance.MouseOverBackColor = Color.Transparent;

        bool isHovered = false;
        _startStopButton.MouseEnter += (s, e) => { isHovered = true; _startStopButton.Invalidate(); };
        _startStopButton.MouseLeave += (s, e) => { isHovered = false; _startStopButton.Invalidate(); };

        _startStopButton.Paint += (s, e) =>
        {
            var g = e.Graphics;
            g.SetClip(new Rectangle(0, 0, _startStopButton.Width, _startStopButton.Height));
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;

            var buttonSize = _startStopButton.Width;
            var center = buttonSize / 2;
            var outerRadius = buttonSize / 2 - 6;
            var glowSteps = Math.Max(8, buttonSize / 10);

            if (_isRecording)
            {
                var glowIntensity = isHovered ? 50 : 32;
                for (int i = glowSteps; i > 0; i--)
                {
                    var alpha = (int)(glowIntensity * (1 - (float)i / glowSteps));
                    using var glowBrush = new SolidBrush(Color.FromArgb(alpha, FluentColors.RecordingPrimary));
                    var glowRect = new Rectangle(center - outerRadius - i, center - outerRadius - i,
                        (outerRadius + i) * 2, (outerRadius + i) * 2);
                    g.FillEllipse(glowBrush, glowRect);
                }

                using (var ringPen = new Pen(Color.FromArgb(80, FluentColors.RecordingPrimary), 2f))
                {
                    var ringRect = new Rectangle(center - outerRadius - 2, center - outerRadius - 2,
                        (outerRadius + 2) * 2, (outerRadius + 2) * 2);
                    g.DrawEllipse(ringPen, ringRect);
                }

                var mainRect = new Rectangle(center - outerRadius, center - outerRadius, outerRadius * 2, outerRadius * 2);
                using (var grad = new LinearGradientBrush(mainRect,
                    Color.FromArgb(38, 38, 46), Color.FromArgb(22, 22, 28), LinearGradientMode.Vertical))
                {
                    g.FillEllipse(grad, mainRect);
                }

                using (var ringPen = new Pen(Color.FromArgb(60, 255, 255, 255), 1f))
                {
                    g.DrawEllipse(ringPen, mainRect);
                }

                var stopSize = isHovered ? (int)(outerRadius * 0.55f) : (int)(outerRadius * 0.5f);
                var stopRadius = Math.Max(4, stopSize / 5);
                var stopRect = new Rectangle(center - stopSize / 2, center - stopSize / 2, stopSize, stopSize);
                using (var stopBrush = new LinearGradientBrush(stopRect,
                    FluentColors.RecordingPrimary, Color.FromArgb(220, 50, 50), LinearGradientMode.Vertical))
                using (var path = GetRoundedRectPath(stopRect, stopRadius))
                {
                    g.FillPath(stopBrush, path);
                }
            }
            else
            {
                var glowIntensity = isHovered ? 36 : 14;
                for (int i = glowSteps; i > 0; i--)
                {
                    var alpha = (int)(glowIntensity * (1 - (float)i / glowSteps));
                    using var glowBrush = new SolidBrush(Color.FromArgb(alpha, FluentColors.AccentPrimary));
                    var glowRect = new Rectangle(center - outerRadius - i, center - outerRadius - i,
                        (outerRadius + i) * 2, (outerRadius + i) * 2);
                    g.FillEllipse(glowBrush, glowRect);
                }

                var ringColor = isHovered
                    ? Color.FromArgb(120, FluentColors.AccentPrimary)
                    : Color.FromArgb(40, FluentColors.AccentPrimary);
                using (var ringPen = new Pen(ringColor, 2f))
                {
                    var ringRect = new Rectangle(center - outerRadius - 2, center - outerRadius - 2,
                        (outerRadius + 2) * 2, (outerRadius + 2) * 2);
                    g.DrawEllipse(ringPen, ringRect);
                }

                var mainRect = new Rectangle(center - outerRadius, center - outerRadius, outerRadius * 2, outerRadius * 2);
                var gradientStart = isHovered ? Color.FromArgb(250, 80, 90) : FluentColors.RecordingPrimary;
                var gradientEnd = isHovered ? Color.FromArgb(220, 45, 55) : Color.FromArgb(190, 40, 50);
                using (var grad = new LinearGradientBrush(mainRect, gradientStart, gradientEnd, LinearGradientMode.Vertical))
                {
                    g.FillEllipse(grad, mainRect);
                }

                // Top highlight (glass)
                var highlightRect = new Rectangle(center - outerRadius + (outerRadius / 4),
                    center - outerRadius + (outerRadius / 7),
                    (outerRadius - outerRadius / 4) * 2,
                    outerRadius - outerRadius / 4);
                using (var hlBrush = new LinearGradientBrush(highlightRect,
                    Color.FromArgb(90, 255, 255, 255),
                    Color.FromArgb(0, 255, 255, 255),
                    LinearGradientMode.Vertical))
                {
                    g.FillEllipse(hlBrush, highlightRect);
                }

                // Mic glyph (Segoe Fluent Icons - Microphone) - precisely centered
                var glyphFontSize = Math.Max(14f, outerRadius * 0.85f);
                using var glyphFont = new Font("Segoe Fluent Icons", glyphFontSize, FontStyle.Regular);
                var glyphRect = new Rectangle(0, 0, buttonSize, buttonSize);
                DrawCenteredGlyph(g, "\uE720", glyphFont, Color.White, glyphRect);
            }
        };
    }

    // -----------------------------------------------------------------------
    // LANGUAGE CHIP, WINDOW CONTROLS, HIDDEN REWRITE
    // -----------------------------------------------------------------------
    private void BuildLanguageChip()
    {
        _languageButton = new Button
        {
            Text = GetLanguageButtonText(),
            Size = new Size(72, 30),
            FlatStyle = FlatStyle.Flat,
            ForeColor = FluentColors.AccentGlow,
            BackColor = FluentColors.Surface,
            Cursor = Cursors.Hand,
            TabStop = false,
            Font = FluentFonts.CaptionSmall
        };
        _languageButton.FlatAppearance.BorderSize = 1;
        _languageButton.FlatAppearance.BorderColor = FluentColors.Border;
        _languageButton.FlatAppearance.MouseDownBackColor = FluentColors.SurfaceHover;
        _languageButton.FlatAppearance.MouseOverBackColor = FluentColors.SurfaceElevated;
        _languageButton.Click += LanguageButton_Click;
        _languageButton.MouseEnter += (s, e) =>
        {
            _languageButton.ForeColor = FluentColors.TextPrimary;
            _languageButton.FlatAppearance.BorderColor = FluentColors.AccentPrimary;
        };
        _languageButton.MouseLeave += (s, e) =>
        {
            _languageButton.ForeColor = FluentColors.AccentGlow;
            _languageButton.FlatAppearance.BorderColor = FluentColors.Border;
        };
    }

    private void BuildWindowControls()
    {
        _infoButton = new Button
        {
            Text = string.Empty,
            Size = new Size(32, 32),
            FlatStyle = FlatStyle.Flat,
            ForeColor = FluentColors.TextPrimary,
            BackColor = Color.FromArgb(120, 36, 36, 48),
            Cursor = Cursors.Hand,
            TabStop = false,
            Font = FluentFonts.Icons,
            TextAlign = ContentAlignment.MiddleCenter,
            Padding = Padding.Empty,
            Margin = Padding.Empty,
            AutoSize = false
        };
        _infoButton.FlatAppearance.BorderSize = 1;
        _infoButton.FlatAppearance.BorderColor = FluentColors.Border;
        _infoButton.FlatAppearance.MouseDownBackColor = FluentColors.SurfaceHover;
        _infoButton.FlatAppearance.MouseOverBackColor = FluentColors.SurfaceElevated;
        _infoButton.Click += InfoButton_Click;
        _infoButton.MouseEnter += (s, e) => { _infoButton.ForeColor = FluentColors.AccentGlow; _infoButton.FlatAppearance.BorderColor = FluentColors.AccentPrimary; _infoButton.Invalidate(); };
        _infoButton.MouseLeave += (s, e) => { _infoButton.ForeColor = FluentColors.TextPrimary; _infoButton.FlatAppearance.BorderColor = FluentColors.Border; _infoButton.Invalidate(); };
        MakeIconButton(_infoButton, "\uE946", FluentFonts.Icons);

        var infoTip = new ToolTip();
        infoTip.SetToolTip(_infoButton, "About Koli (version, developer, contact)");

        _settingsButton = new Button
        {
            Text = string.Empty, // Glyph painted via MakeIconButton
            Size = new Size(32, 32),
            FlatStyle = FlatStyle.Flat,
            ForeColor = FluentColors.TextPrimary,
            BackColor = Color.FromArgb(120, 36, 36, 48),
            Cursor = Cursors.Hand,
            TabStop = false,
            Font = FluentFonts.Icons,
            TextAlign = ContentAlignment.MiddleCenter,
            Padding = Padding.Empty,
            Margin = Padding.Empty,
            AutoSize = false
        };
        _settingsButton.FlatAppearance.BorderSize = 1;
        _settingsButton.FlatAppearance.BorderColor = FluentColors.Border;
        _settingsButton.FlatAppearance.MouseDownBackColor = FluentColors.SurfaceHover;
        _settingsButton.FlatAppearance.MouseOverBackColor = FluentColors.SurfaceElevated;
        _settingsButton.Click += SettingsButton_Click;
        _settingsButton.MouseEnter += (s, e) => { _settingsButton.ForeColor = FluentColors.AccentGlow; _settingsButton.FlatAppearance.BorderColor = FluentColors.AccentPrimary; _settingsButton.Invalidate(); };
        _settingsButton.MouseLeave += (s, e) => { _settingsButton.ForeColor = FluentColors.TextPrimary; _settingsButton.FlatAppearance.BorderColor = FluentColors.Border; _settingsButton.Invalidate(); };
        MakeIconButton(_settingsButton, "", FluentFonts.Icons);

        var tip = new ToolTip();
        tip.SetToolTip(_settingsButton, "Settings (transcription model, rewrite, translation, typing)");

        _minimizeButton = new Button
        {
            Text = string.Empty,
            Size = new Size(32, 32),
            FlatStyle = FlatStyle.Flat,
            ForeColor = FluentColors.TextPrimary,
            BackColor = Color.FromArgb(120, 36, 36, 48),
            Cursor = Cursors.Hand,
            TabStop = false,
            Font = FluentFonts.Icons,
            TextAlign = ContentAlignment.MiddleCenter,
            Padding = Padding.Empty,
            Margin = Padding.Empty,
            AutoSize = false
        };
        _minimizeButton.FlatAppearance.BorderSize = 1;
        _minimizeButton.FlatAppearance.BorderColor = FluentColors.Border;
        _minimizeButton.FlatAppearance.MouseDownBackColor = FluentColors.SurfaceHover;
        _minimizeButton.FlatAppearance.MouseOverBackColor = FluentColors.SurfaceElevated;
        _minimizeButton.Click += (s, e) => WindowState = FormWindowState.Minimized;
        _minimizeButton.MouseEnter += (s, e) => { _minimizeButton.ForeColor = FluentColors.AccentGlow; _minimizeButton.FlatAppearance.BorderColor = FluentColors.AccentPrimary; _minimizeButton.Invalidate(); };
        _minimizeButton.MouseLeave += (s, e) => { _minimizeButton.ForeColor = FluentColors.TextPrimary; _minimizeButton.FlatAppearance.BorderColor = FluentColors.Border; _minimizeButton.Invalidate(); };
        MakeIconButton(_minimizeButton, "", FluentFonts.Icons);
        tip.SetToolTip(_minimizeButton, "Minimize to tray");

        _closeButton = new Button
        {
            Text = string.Empty,
            Size = new Size(32, 32),
            FlatStyle = FlatStyle.Flat,
            ForeColor = FluentColors.TextPrimary,
            BackColor = Color.FromArgb(120, 36, 36, 48),
            Cursor = Cursors.Hand,
            TabStop = false,
            Font = FluentFonts.Icons,
            TextAlign = ContentAlignment.MiddleCenter,
            Padding = Padding.Empty,
            Margin = Padding.Empty,
            AutoSize = false
        };
        _closeButton.FlatAppearance.BorderSize = 1;
        _closeButton.FlatAppearance.BorderColor = FluentColors.Border;
        _closeButton.FlatAppearance.MouseDownBackColor = Color.FromArgb(196, 43, 28);
        _closeButton.FlatAppearance.MouseOverBackColor = Color.FromArgb(80, 239, 68, 68);
        _closeButton.Click += (s, e) =>
        {
            Hide();
            ShowToastNotification("Koli", "The application continues in the background.", ToolTipIcon.Info);
        };
        _closeButton.MouseEnter += (s, e) => { _closeButton.ForeColor = FluentColors.Error; _closeButton.FlatAppearance.BorderColor = FluentColors.Error; _closeButton.Invalidate(); };
        _closeButton.MouseLeave += (s, e) => { _closeButton.ForeColor = FluentColors.TextPrimary; _closeButton.FlatAppearance.BorderColor = FluentColors.Border; _closeButton.Invalidate(); };
        MakeIconButton(_closeButton, "", FluentFonts.Icons);
        tip.SetToolTip(_closeButton, "Close (continues in tray)");
    }

    private void BuildHiddenRewriteButton()
    {
        _rewriteToggleButton = new Button
        {
            Text = "\uE8C8",
            Size = new Size(32, 32),
            Location = new Point(-200, -200),
            FlatStyle = FlatStyle.Flat,
            ForeColor = _settings.Rewrite.Enabled ? FluentColors.Success : FluentColors.TextSecondary,
            BackColor = FluentColors.Surface,
            Cursor = Cursors.Hand,
            TabStop = false,
            Font = FluentFonts.Icons,
            Visible = false
        };
        _rewriteToggleButton.FlatAppearance.BorderSize = 0;
        _rewriteToggleButton.Click += RewriteToggleButton_Click;

        _wavePanel = new WaveSurfacePanel
        {
            BackColor = Color.Transparent,
            Visible = true
        };
        _wavePanel.Paint += WavePanel_Paint;

        // Status label is created here so dock builder can wire it up
        _statusLabel = new Label
        {
            Text = "Ready",
            ForeColor = FluentColors.AccentGlow,
            BackColor = Color.Transparent
        };
    }

    // -----------------------------------------------------------------------
    // Update helpers (called from existing transcription / recording flow)
    // -----------------------------------------------------------------------
    private void UpdateTimerLabel()
    {
        if (_timerLabel == null) return;
        if (_recordingStartTime == DateTime.MinValue)
        {
            _timerLabel.Text = "00:00";
            return;
        }
        var elapsed = DateTime.UtcNow - _recordingStartTime;
        _timerLabel.Text = elapsed.TotalHours >= 1
            ? elapsed.ToString(@"hh\:mm\:ss")
            : elapsed.ToString(@"mm\:ss");
    }

    private void ShowTranscriptCard(string title, string body, bool recording)
    {
        if (_transcriptCard == null) return;
        if (_emptyStatePanel != null) _emptyStatePanel.Visible = false;
        _transcriptCard.Visible = true;
        _transcriptCardTitle.Text = title;
        _transcriptCardBadge.Text = recording ? "  LIVE  " : "  DONE  ";
        _transcriptCardBadge.ForeColor = recording ? FluentColors.RecordingGlow : FluentColors.AccentGlow;
        _transcriptCardBadge.Invalidate();
        _transcriptLabel.Text = body;
    }

    private void ResetTranscriptCard()
    {
        if (_transcriptCard == null) return;
        _transcriptLabel.Text = "";
        _transcriptCard.Visible = false;
        if (_emptyStatePanel != null) _emptyStatePanel.Visible = true;
    }

    // Helper method for creating rounded rectangles
    private static GraphicsPath GetRoundedRectPath(Rectangle rect, int radius)
    {
        var path = new GraphicsPath();
        int diameter = radius * 2;
        
        path.AddArc(rect.X, rect.Y, diameter, diameter, 180, 90);
        path.AddArc(rect.Right - diameter, rect.Y, diameter, diameter, 270, 90);
        path.AddArc(rect.Right - diameter, rect.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(rect.X, rect.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        
        return path;
    }
    
    // Helper method to apply rounded corners to a button
    private static void ApplyRoundedCorners(Button button, int radius = 6)
    {
        if (button.Region != null)
        {
            button.Region.Dispose();
        }
        
        var path = GetRoundedRectPath(new Rectangle(0, 0, button.Width, button.Height), radius);
        button.Region = new Region(path);
    }
    
    /// <summary>
    /// Applies Windows 11 modern styling including rounded corners and dark mode.
    /// </summary>
    private void ApplyModernWindowStyle()
    {
        try
        {
            // Enable dark mode for title bar
            var darkMode = 1;
            DwmSetWindowAttribute(Handle, DWMWA_USE_IMMERSIVE_DARK_MODE, ref darkMode, sizeof(int));

            // Enable Windows 11 rounded corners
            var cornerPreference = DWMWCP_ROUND;
            DwmSetWindowAttribute(Handle, DWMWA_WINDOW_CORNER_PREFERENCE, ref cornerPreference, sizeof(int));

            // Try Mica backdrop (Windows 11 22H2+)
            try
            {
                var backdropType = DWMSBT_TABBEDWINDOW; // Mica Alt for richer effect
                DwmSetWindowAttribute(Handle, DWMWA_SYSTEMBACKDROP_TYPE, ref backdropType, sizeof(int));
            }
            catch
            {
                // Mica not available on this Windows version
            }

            // Extend frame for shadow and Mica effect
            var margins = new MARGINS { Left = 1, Right = 1, Top = 1, Bottom = 1 };
            DwmExtendFrameIntoClientArea(Handle, ref margins);
        }
        catch
        {
            // Fallback for older Windows versions - styling will still work via Region
        }
    }
    
    /// <summary>
    /// Applies modern styling to dialog forms.
    /// </summary>
    private static void ApplyModernDialogStyle(Form dialog)
    {
        try
        {
            // Enable dark mode
            var darkMode = 1;
            DwmSetWindowAttribute(dialog.Handle, DWMWA_USE_IMMERSIVE_DARK_MODE, ref darkMode, sizeof(int));

            // Enable rounded corners
            var cornerPreference = DWMWCP_ROUND;
            DwmSetWindowAttribute(dialog.Handle, DWMWA_WINDOW_CORNER_PREFERENCE, ref cornerPreference, sizeof(int));

            // Try Mica backdrop for dialogs too
            try
            {
                var backdropType = DWMSBT_TABBEDWINDOW;
                DwmSetWindowAttribute(dialog.Handle, DWMWA_SYSTEMBACKDROP_TYPE, ref backdropType, sizeof(int));
            }
            catch { }
        }
        catch
        {
            // Fallback: use Region for rounded corners
            var path = new GraphicsPath();
            int radius = 14;
            path.AddArc(0, 0, radius * 2, radius * 2, 180, 90);
            path.AddArc(dialog.Width - radius * 2, 0, radius * 2, radius * 2, 270, 90);
            path.AddArc(dialog.Width - radius * 2, dialog.Height - radius * 2, radius * 2, radius * 2, 0, 90);
            path.AddArc(0, dialog.Height - radius * 2, radius * 2, radius * 2, 90, 90);
            path.CloseFigure();
            dialog.Region = new Region(path);
        }
    }
    
    // Form background - subtle ambient violet glow at top-left, soft outer border
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        // Ambient corner glow (top-left, behind header)
        var glowRect = new Rectangle(-120, -160, 520, 360);
        using (var glow = new PathGradientBrush(GetEllipsePath(glowRect)))
        {
            glow.CenterColor = Color.FromArgb(70, 124, 58, 237);
            glow.SurroundColors = new[] { Color.FromArgb(0, 124, 58, 237) };
            g.FillEllipse(glow, glowRect);
        }

        // Ambient corner glow (bottom-right, behind dock)
        var glowRect2 = new Rectangle(Width - 360, Height - 220, 480, 320);
        using (var glow2 = new PathGradientBrush(GetEllipsePath(glowRect2)))
        {
            glow2.CenterColor = Color.FromArgb(55, 99, 102, 241);
            glow2.SurroundColors = new[] { Color.FromArgb(0, 99, 102, 241) };
            g.FillEllipse(glow2, glowRect2);
        }

        // Subtle outer border (1px inside)
        using (var borderPen = new Pen(Color.FromArgb(40, 124, 58, 237), 1f))
        {
            g.DrawRectangle(borderPen, 0, 0, Width - 1, Height - 1);
        }
    }

    private static GraphicsPath GetEllipsePath(Rectangle rect)
    {
        var path = new GraphicsPath();
        path.AddEllipse(rect);
        return path;
    }

    /// <summary>
    /// Draws a Segoe Fluent Icons glyph precisely centered in <paramref name="bounds"/>.
    /// Necessary because the icon font has asymmetric internal bearings that throw off the
    /// default Button text rendering — even with TextAlign = MiddleCenter.
    /// </summary>
    private static void DrawCenteredGlyph(Graphics g, string glyph, Font font, Color color, Rectangle bounds)
    {
        const TextFormatFlags flags =
            TextFormatFlags.NoPadding |
            TextFormatFlags.NoPrefix |
            TextFormatFlags.SingleLine;

        var size = TextRenderer.MeasureText(g, glyph, font, new Size(int.MaxValue, int.MaxValue), flags);
        var x = bounds.X + (bounds.Width - size.Width) / 2;
        var y = bounds.Y + (bounds.Height - size.Height) / 2;
        TextRenderer.DrawText(g, glyph, font, new Point(x, y), color, Color.Transparent, flags);
    }

    /// <summary>
    /// Converts a Button into a glyph-only icon button with pixel-precise centering.
    /// Clears the Text property and renders the glyph in a Paint handler.
    /// </summary>
    private static void MakeIconButton(Button btn, string glyph, Font font)
    {
        btn.Text = string.Empty;
        btn.Paint += (s, e) =>
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
            DrawCenteredGlyph(g, glyph, font, btn.ForeColor, btn.ClientRectangle);
        };
    }

    // P/Invoke for dragging borderless form
    [System.Runtime.InteropServices.DllImport("user32.dll")]
    public static extern int SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam);
    [System.Runtime.InteropServices.DllImport("user32.dll")]
    public static extern bool ReleaseCapture();
    public const int WM_NCLBUTTONDOWN = 0xA1;
    public const int HT_CAPTION = 0x2;
    
    // P/Invoke for global keyboard shortcuts
    [System.Runtime.InteropServices.DllImport("user32.dll")]
    public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);
    [System.Runtime.InteropServices.DllImport("user32.dll")]
    public static extern bool UnregisterHotKey(IntPtr hWnd, int id);
    private const int HOTKEY_ID = 9000; // Unique ID for our hotkey (F9)
    private const int HOTKEY_ID_F7 = 9001; // Unique ID for F7 hotkey
    private const int HOTKEY_ID_F6 = 9002; // Unique ID for F6 hotkey
    private const uint MOD_NONE = 0x0000; // No modifier keys
    private const uint VK_F9 = 0x78; // Virtual key code for F9
    private const uint VK_F7 = 0x76; // Virtual key code for F7
    private const uint VK_F6 = 0x75; // Virtual key code for F6
    private const int WM_HOTKEY = 0x0312; // Windows message for hotkey
    
    // P/Invoke for keyboard language detection
    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern IntPtr GetKeyboardLayout(uint idThread);
    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();
    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);
    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool IsWindow(IntPtr hWnd);
    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);
    private const int WM_INPUTLANGCHANGE = 0x0051; // Windows message for input language change

    private void InitializeSystemTray()
    {
        _notifyIcon = new NotifyIcon
        {
            Text = "Koli",
            Visible = true
        };
        
        // Load icon for system tray from embedded resources
        var trayIcon = LoadEmbeddedIcon();
        _notifyIcon.Icon = trayIcon ?? SystemIcons.Application;

        var contextMenu = new ContextMenuStrip();
        var showMenuItem = new ToolStripMenuItem("Show");
        showMenuItem.Click += (s, e) =>
        {
            Show();
            WindowState = FormWindowState.Normal;
            Activate();
        };
        contextMenu.Items.Add(showMenuItem);

        var exitMenuItem = new ToolStripMenuItem("Exit");
        exitMenuItem.Click += (s, e) =>
        {
            _notifyIcon?.Dispose();
            Application.Exit();
        };
        contextMenu.Items.Add(exitMenuItem);

        _notifyIcon.ContextMenuStrip = contextMenu;
        _notifyIcon.DoubleClick += (s, e) =>
        {
            Show();
            WindowState = FormWindowState.Normal;
            Activate();
        };
    }

    private void ShowWindowsToast(string toastXml)
    {
        // Note: Windows Runtime APIs require additional setup for .NET 8.0
        // For now, we'll use the balloon tip which works reliably
        // The balloon tip will show the icon from the NotifyIcon
        // To enable true Windows Toast notifications with silent mode and custom icons,
        // you would need to add the Windows App SDK package with proper configuration
    }

    private void ShowToastNotification(string title, string message, ToolTipIcon icon = ToolTipIcon.Info)
    {
        // Error toasts stay longer so the full message can be read
        var durationMs = icon == ToolTipIcon.Error ? 12000 : (icon == ToolTipIcon.Warning ? 6000 : 3000);
        ShowToastNotification(title, message, icon, durationMs);
    }

    private void ShowToastNotification(string title, string message, ToolTipIcon icon, int displayDurationMs)
    {
        try
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action(() => ShowToastNotification(title, message, icon, displayDurationMs)));
                return;
            }

            var appIcon = LoadEmbeddedIcon() ?? _notifyIcon?.Icon ?? SystemIcons.Application;
            var toast = new ToastForm(appIcon, title, message, displayDurationMs);
            toast.Show();
        }
        catch
        {
            // En dernier recours, fallback sur un balloon tip sans son explicite
            if (_notifyIcon != null && _notifyIcon.Visible)
            {
                _notifyIcon.ShowBalloonTip(Math.Min(displayDurationMs, 10000), title, message, ToolTipIcon.None);
            }
        }
    }

    private string GetIconPath()
    {
        try
        {
            var appIcon = LoadEmbeddedIcon();
            if (appIcon != null)
            {
                // Save icon to temp file for toast notification
                var tempIconPath = Path.Combine(Path.GetTempPath(), "Koli_Icon.ico");
                using (var fs = new FileStream(tempIconPath, FileMode.Create))
                {
                    appIcon.Save(fs);
                }
                return tempIconPath;
            }
        }
        catch
        {
            // If icon save fails, return empty
        }
        return string.Empty;
    }

    private static string EscapeXml(string text)
    {
        return text
            .Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;")
            .Replace("\"", "&quot;")
            .Replace("'", "&apos;");
    }

    private void MainForm_Resize(object? sender, EventArgs e)
    {
        if (WindowState == FormWindowState.Minimized)
        {
            Hide();
            return;
        }

        // Re-apply the rounded region for the new window size
        ApplyRoundedFormRegion();

        // Reposition right-anchored header controls (no Anchor used - manual layout here)
        if (_headerPanel != null && _headerPanel.Width > 100)
        {
            var hw = _headerPanel.Width;
            if (_closeButton != null)
                _closeButton.Location = new Point(hw - _closeButton.Width - 12, (HeaderHeight - _closeButton.Height) / 2);
            if (_minimizeButton != null)
                _minimizeButton.Location = new Point(hw - _minimizeButton.Width - _closeButton!.Width - 18, (HeaderHeight - _minimizeButton.Height) / 2);
            if (_settingsButton != null)
                _settingsButton.Location = new Point(hw - _settingsButton.Width - _closeButton!.Width - _minimizeButton!.Width - 24, (HeaderHeight - _settingsButton.Height) / 2);
            if (_infoButton != null)
                _infoButton.Location = new Point(_settingsButton!.Left - _infoButton.Width - 6, (HeaderHeight - _infoButton.Height) / 2);
            if (_languageButton != null)
                _languageButton.Location = new Point(_infoButton!.Left - _languageButton.Width - 6, (HeaderHeight - _languageButton.Height) / 2);
        }

        // Re-anchor dock contents (record button, wave, timer, status)
        if (_dockPanel != null && _dockPanel.Width > 100)
        {
            var dw = _dockPanel.Width;
            if (_startStopButton != null)
                _startStopButton.Location = new Point(16, (DockHeight - RecordButtonSize) / 2);

            // Right cluster: status + timer
            var statusW = 86;
            var timerW = 74;
            if (_statusLabel != null)
            {
                _statusLabel.Size = new Size(statusW, 22);
                _statusLabel.Location = new Point(dw - statusW - 14, (DockHeight - 22) / 2);
            }
            if (_timerLabel != null)
            {
                _timerLabel.Size = new Size(timerW, 22);
                _timerLabel.Location = new Point(dw - statusW - timerW - 22, (DockHeight - 22) / 2);
            }

            if (_wavePanel != null)
            {
                var waveLeft = 16 + RecordButtonSize + 14;
                var waveRight = dw - statusW - timerW - 36;
                _wavePanel.Location = new Point(waveLeft, (DockHeight - 44) / 2);
                _wavePanel.Size = new Size(Math.Max(40, waveRight - waveLeft), 44);
            }
        }

        Invalidate();
    }

    private void MainForm_FormClosing(object? sender, FormClosingEventArgs e)
    {
        if (e.CloseReason == CloseReason.UserClosing)
        {
            e.Cancel = true;
            Hide();
        }
        else
        {
            // Only unregister hotkeys when actually closing (not just hiding)
            UnregisterHotKey(Handle, HOTKEY_ID);
            UnregisterHotKey(Handle, HOTKEY_ID_F7);
            UnregisterHotKey(Handle, HOTKEY_ID_F6);
            StopRecording();
            _notifyIcon?.Dispose();
        }
    }
    
    /// <summary>
    /// Handles input language change message. Called by the message filter.
    /// </summary>
    internal void HandleInputLanguageChange(IntPtr hkl)
    {
        if (hkl != IntPtr.Zero)
        {
            // Extract language ID from HKL (low word)
            var langId = (ushort)(hkl.ToInt64() & 0xFFFF);
            UpdateLanguageFromKeyboardLayout(langId);
        }
        else
        {
            // Fallback to getting current keyboard layout
            UpdateLanguageFromKeyboard();
        }
    }

    protected override void WndProc(ref Message m)
    {
        // Handle global hotkey messages
        if (m.Msg == WM_HOTKEY)
        {
            if (m.WParam.ToInt32() == HOTKEY_ID)
            {
                StartStopButton_Click(this, EventArgs.Empty);
                return;
            }
            else if (m.WParam.ToInt32() == HOTKEY_ID_F7)
            {
                if (_isRecording)
                {
                    CancelRecording();
                }
                return;
            }
            else if (m.WParam.ToInt32() == HOTKEY_ID_F6)
            {
                if (_isRecording)
                {
                    TogglePause();
                }
                return;
            }
        }
        
        // Note: WM_INPUTLANGCHANGE is now handled by the message filter
        // to prevent it from reaching child controls
        
        base.WndProc(ref m);
    }

    private void MainForm_KeyDown(object? sender, KeyEventArgs e)
    {
        // F9 key to start/stop recording
        if (e.KeyCode == Keys.F9)
        {
            e.Handled = true;
            e.SuppressKeyPress = true;
            StartStopButton_Click(sender, e);
        }
        // F7 key to cancel recording without transcribing
        else if (e.KeyCode == Keys.F7)
        {
            e.Handled = true;
            e.SuppressKeyPress = true;
            if (_isRecording)
            {
                CancelRecording();
            }
        }
        // F6 key to pause/resume recording
        else if (e.KeyCode == Keys.F6)
        {
            e.Handled = true;
            e.SuppressKeyPress = true;
            if (_isRecording)
            {
                TogglePause();
            }
        }
    }

    private async void StartStopButton_Click(object? sender, EventArgs e)
    {
        if (!_isRecording)
        {
            await StartRecording();
        }
        else
        {
            StopRecording();
        }
    }

    private async Task StartRecording()
    {
        try
        {
            // Save the window that currently has focus - we'll restore it when pasting transcribed text
            _targetWindowForTyping = GetForegroundWindow();

            // Update language from keyboard before starting recording
            UpdateLanguageFromKeyboard();
            
            _statusLabel.Text = "\u23F3  Starting...";
            _statusLabel.ForeColor = FluentColors.Warning;
            _statusLabel.BackColor = Color.Transparent; // Keep transparent
            _startStopButton.Enabled = false;

        // Resolve API key
        string apiKey;
        try
        {
            apiKey = await _secureStore.ResolveApiKeyAsync(_settings.AzureOpenAI.ApiKey, CancellationToken.None);
        }
        catch (InvalidOperationException ex)
        {
            MessageBox.Show(
                $"Configuration error:\n{ex.Message}\n\nPlease verify that the API key is configured in Config/appsettings.json",
                "Configuration Error",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
                    _statusLabel.Text = "\u26A0  Config Error";
                    _statusLabel.ForeColor = FluentColors.Error;
                    _startStopButton.Enabled = true;
            _startStopButton.Invalidate(); // Repaint
            return;
        }

            // Initialize audio capture service only
            _audioCapture = new AudioCaptureService(_settings.Audio);
            
            // Log info (check if console is not disposed first)
            if (_debugConsole != null && !_debugConsole.IsDisposed)
            {
                _debugConsole.LogInfo($"Starting audio capture - Endpoint: {_settings.AzureOpenAI.Endpoint}, Model: {_settings.AzureOpenAI.Model}");
            }

            _cancellationTokenSource = new CancellationTokenSource();

            // Start audio capture only (transcription will happen on stop)
            if (_debugConsole != null && !_debugConsole.IsDisposed)
            {
                _debugConsole.LogInfo("Starting audio capture...");
            }
            await _audioCapture.StartAsync(_cancellationTokenSource.Token);
            if (_debugConsole != null && !_debugConsole.IsDisposed)
            {
                _debugConsole.LogInfo("Audio capture started - collecting audio...");
            }

            _dictationUsedRealtime = OpenAiModelProfiles.ShouldUseRealtimeTranscription(_settings.AzureOpenAI);
            if (_dictationUsedRealtime)
            {
                _dictationRealtimeStt = new SpeechToTextService(_settings.AzureOpenAI, apiKey);
                _dictationRealtimeStt.RealtimeTranscript += OnDictationRealtimeTranscript;
                _dictationRealtimeStt.ErrorLogging += OnErrorLogging;
                if (_debugConsole != null && !_debugConsole.IsDisposed)
                {
                    _dictationRealtimeStt.RequestLogging += OnRequestLogging;
                    _dictationRealtimeStt.ResponseLogging += OnResponseLogging;
                }

                // Do not tie the stream or session to recording CTS: cancelling it on Stop races sends and skips commit.
                _dictationRealtimeTask = _dictationRealtimeStt.RunRealtimeTranscriptionAsync(
                    _audioCapture.GetAudioStreamAsync(CancellationToken.None),
                    CancellationToken.None);

                if (_debugConsole != null && !_debugConsole.IsDisposed)
                {
                    _debugConsole.LogInfo("OpenAI Realtime transcription session started (live captions during recording).");
                }
            }

            _isRecording = true;
            _isPaused = false;
            _accumulatedTranscription.Clear(); // Reset accumulated transcription
            _realtimeTypedLengthByItem.Clear();
            _realtimeTypedAnything = false;
            _startStopButton.Invalidate(); // Repaint
            _startStopButton.Enabled = true;
            _statusLabel.Text = "Recording";
            _statusLabel.ForeColor = FluentColors.Error;

            // Switch main content to live transcript card (only if on Home)
            if (_currentView == MainView.Home)
                ShowTranscriptCard("Live transcript", string.Empty, recording: true);

            // Start recording timer
            _recordingStartTime = DateTime.UtcNow;
            UpdateTimerLabel();
            _timerTickTimer?.Start();

            // Show wave visualization
            _wavePanel.Visible = true;
            _currentAudioLevel = 0f;
            _audioLevelHistory.Clear();
            
            // Subscribe to audio level events
            _audioCapture.AudioLevelChanged += AudioCapture_AudioLevelChanged;
            
            // Start wave animation timer
            _waveTimer = new System.Windows.Forms.Timer { Interval = 50 }; // ~20 FPS
            _waveTimer.Tick += (s, e) => _wavePanel.Invalidate();
            _waveTimer.Start();
            
            // Show toast notification
            ShowToastNotification("Koli", "Recording started", ToolTipIcon.Info);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error starting: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            _statusLabel.Text = "\u26A0  Error";
            _statusLabel.ForeColor = FluentColors.Error;
            _startStopButton.Enabled = true;
            _startStopButton.Invalidate();
            StopRecording();
        }
    }

    private async void StopRecording()
    {
        if (!_isRecording)
        {
            return;
        }

        _isRecording = false;

        // Stop recording timer (keep last value visible)
        _timerTickTimer?.Stop();

        // Keep wave panel visible but reset levels (we render an idle baseline)
        _currentAudioLevel = 0f;
        _audioLevelHistory.Clear();
        _wavePanel.Invalidate();

        // Stop wave timer
        _waveTimer?.Stop();
        _waveTimer?.Dispose();
        _waveTimer = null;
        
        // Unsubscribe from audio level events
        if (_audioCapture != null)
        {
            _audioCapture.AudioLevelChanged -= AudioCapture_AudioLevelChanged;
        }
        
        if (_debugConsole != null && !_debugConsole.IsDisposed)
        {
            _debugConsole.LogInfo("Stopping audio capture...");
        }

        // Realtime: completing the capture channel ends the stream cleanly (commit). Batch mode still uses Cancel.
        if (!_dictationUsedRealtime)
            _cancellationTokenSource?.Cancel();
        await Task.Delay(100); // Give a moment for graceful shutdown

        // Get collected audio before disposing
        byte[]? collectedAudio = null;
        if (_audioCapture != null)
        {
            collectedAudio = _audioCapture.GetCollectedAudio();
            if (_debugConsole != null && !_debugConsole.IsDisposed)
            {
                _debugConsole.LogInfo($"Collected audio size: {collectedAudio?.Length ?? 0} bytes");
            }
        }

        // Dispose audio capture
        try
        {
            _audioCapture?.DisposeAsync().AsTask().Wait(TimeSpan.FromSeconds(5));
        }
        catch (AggregateException ex) when (ex.InnerException is TaskCanceledException)
        {
            // Ignore cancellation exceptions during disposal
        }

        _audioCapture = null;
        _cancellationTokenSource?.Dispose();
        _cancellationTokenSource = null;

        _startStopButton.Invalidate(); // Repaint
        _statusLabel.Text = "\u2728  Transcribing...";
        _statusLabel.ForeColor = FluentColors.Warning;

        // Show cursor overlay to indicate processing
        _cursorOverlay ??= new CursorOverlay();
        _cursorOverlay.ShowOverlay(_dictationUsedRealtime ? "Finalizing realtime session..." : "Transcription in progress");

        try
        {
        // Show toast notification
        ShowToastNotification("Koli", "Recording stopped", ToolTipIcon.Info);

        string apiKey;
        try
        {
            apiKey = await _secureStore.ResolveApiKeyAsync(_settings.AzureOpenAI.ApiKey, CancellationToken.None);
        }
        catch (InvalidOperationException ex)
        {
            if (_dictationUsedRealtime)
                await FinalizeDictationRealtimeAsync(userCancelled: true).ConfigureAwait(true);
            MessageBox.Show(
                $"Configuration error:\n{ex.Message}\n\nPlease verify that the API key is configured in Config/appsettings.json",
                "Configuration Error",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
            _statusLabel.Text = "\u26A0  Config Error";
            _statusLabel.ForeColor = FluentColors.Error;
            _cursorOverlay?.HideOverlay();
            return;
        }

        if (_dictationUsedRealtime)
        {
            await FinalizeDictationRealtimeAsync(userCancelled: false).ConfigureAwait(true);
            if (_debugConsole != null && !_debugConsole.IsDisposed)
            {
                _debugConsole.LogInfo("Realtime transcription session finalized");
            }
        }
        else if (collectedAudio != null && collectedAudio.Length > 0)
        {
            try
            {
                // Initialize transcription service
                _speechToText = new SpeechToTextService(_settings.AzureOpenAI, apiKey);
                _speechToText.TranscriptionReceived += OnTranscriptionReceived;
                _speechToText.ErrorLogging += OnErrorLogging; // Always subscribe so API errors show toast
                
                // Connect debug request/response logging only when console is open
                if (_debugConsole != null && !_debugConsole.IsDisposed)
                {
                    _speechToText.RequestLogging += OnRequestLogging;
                    _speechToText.ResponseLogging += OnResponseLogging;
                    _debugConsole.LogInfo("Starting transcription of collected audio...");
                }

                // Transcribe the collected audio
                var transcriptionCts = new CancellationTokenSource();
                await _speechToText.TranscribeAudioAsync(collectedAudio, transcriptionCts.Token);

                // Disconnect logging before disposing
                if (_speechToText != null)
                {
                    _speechToText.RequestLogging -= OnRequestLogging;
                    _speechToText.ResponseLogging -= OnResponseLogging;
                    _speechToText.ErrorLogging -= OnErrorLogging;
                }

                // Dispose transcription service
                try
                {
                    _speechToText?.DisposeAsync().AsTask().Wait(TimeSpan.FromSeconds(5));
                }
                catch (AggregateException ex) when (ex.InnerException is TaskCanceledException)
                {
                    // Ignore cancellation exceptions during disposal
                }

                _speechToText = null;
                transcriptionCts.Dispose();

                if (_debugConsole != null && !_debugConsole.IsDisposed)
                {
                    _debugConsole.LogInfo("Transcription completed");
                }
            }
            catch (Exception ex)
            {
                if (_debugConsole != null && !_debugConsole.IsDisposed)
                {
                    _debugConsole.LogError("Error during transcription", ex);
                }
                MessageBox.Show($"Error during transcription: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        else
        {
            if (_debugConsole != null && !_debugConsole.IsDisposed)
            {
                _debugConsole.LogInfo("No audio collected to transcribe");
            }
        }

                // Opt-in post-transcription translation. Runs only when the user has
                // explicitly enabled translation and picked a target language; completely
                // independent from LanguageMode (which is just a Whisper input hint).
                if (_settings.Translation.Enabled
                    && !string.IsNullOrWhiteSpace(_settings.Translation.TargetLanguage)
                    && _accumulatedTranscription.Length > 0)
                {
                    _statusLabel.Text = "\u2728  Translating...";
                    _statusLabel.ForeColor = FluentColors.Warning;
                    _cursorOverlay?.UpdateText("Translation in progress");

                    try
                    {
                        if (_debugConsole != null && !_debugConsole.IsDisposed)
                        {
                            _debugConsole.LogInfo($"Starting translation to '{_settings.Translation.TargetLanguage}'...");
                        }

                        var translationService = new TextTranslationService(
                            _settings.Translation,
                            _settings.AzureOpenAI.Endpoint,
                            apiKey);

                        if (_debugConsole != null && !_debugConsole.IsDisposed)
                        {
                            translationService.RequestLogging += OnRequestLogging;
                            translationService.ResponseLogging += OnResponseLogging;
                            translationService.ErrorLogging += OnErrorLogging;
                        }

                        var originalText = _accumulatedTranscription.ToString();
                        var translatedText = await translationService.TranslateAsync(
                            originalText,
                            _settings.Translation.TargetLanguage,
                            CancellationToken.None);

                        if (_debugConsole != null && !_debugConsole.IsDisposed)
                        {
                            translationService.RequestLogging -= OnRequestLogging;
                            translationService.ResponseLogging -= OnResponseLogging;
                            translationService.ErrorLogging -= OnErrorLogging;
                        }

                        await translationService.DisposeAsync();

                        if (!string.IsNullOrWhiteSpace(translatedText))
                        {
                            _accumulatedTranscription.Clear();
                            _accumulatedTranscription.Append(translatedText);

                            if (_debugConsole != null && !_debugConsole.IsDisposed)
                            {
                                _debugConsole.LogInfo($"Translation completed ({translatedText.Length} characters)");
                            }
                        }
                        else
                        {
                            if (_debugConsole != null && !_debugConsole.IsDisposed)
                            {
                                _debugConsole.LogError("Translation returned empty, keeping original transcription", null);
                            }
                            ShowToastNotification("Translation Warning", "Translation failed, keeping original transcription", ToolTipIcon.Warning);
                        }
                    }
                    catch (Exception ex)
                    {
                        if (_debugConsole != null && !_debugConsole.IsDisposed)
                        {
                            _debugConsole.LogError($"Translation failed: {ex.Message}", ex);
                        }
                        ShowToastNotification("Translation Error", $"Translation failed: {ex.Message}", ToolTipIcon.Error);
                    }
                }

                // Rewrite text if enabled
                if (_settings.Rewrite.Enabled && _accumulatedTranscription.Length > 0)
                {
                    _statusLabel.Text = "\u270F  Rewriting...";
                    _statusLabel.ForeColor = FluentColors.Warning;
                    _cursorOverlay?.UpdateText("Rewrite in progress");

                    try
                    {
                        if (_debugConsole != null && !_debugConsole.IsDisposed)
                        {
                            _debugConsole.LogInfo("Starting text rewrite...");
                        }

                        var rewriteService = new TextRewriteService(_settings.Rewrite, apiKey);
                        
                        // Connect debug logging if console is open
                        if (_debugConsole != null && !_debugConsole.IsDisposed)
                        {
                            rewriteService.RequestLogging += OnRequestLogging;
                            rewriteService.ResponseLogging += OnResponseLogging;
                            rewriteService.ErrorLogging += OnErrorLogging;
                        }

                        var originalText = _accumulatedTranscription.ToString();
                        var currentLanguage = _settings.AzureOpenAI.Language; // Use the current language setting
                        var rewrittenText = await rewriteService.RewriteTextAsync(originalText, currentLanguage, CancellationToken.None);

                        // Disconnect logging before disposing
                        if (_debugConsole != null && !_debugConsole.IsDisposed)
                        {
                            rewriteService.RequestLogging -= OnRequestLogging;
                            rewriteService.ResponseLogging -= OnResponseLogging;
                            rewriteService.ErrorLogging -= OnErrorLogging;
                        }

                        await rewriteService.DisposeAsync();

                        if (!string.IsNullOrWhiteSpace(rewrittenText))
                        {
                            _accumulatedTranscription.Clear();
                            _accumulatedTranscription.Append(rewrittenText);

                            if (_debugConsole != null && !_debugConsole.IsDisposed)
                            {
                                _debugConsole.LogInfo($"Text rewritten successfully ({rewrittenText.Length} characters)");
                            }

                            ShowToastNotification("Text Rewritten", "Text has been rewritten professionally", ToolTipIcon.Info);
                        }
                        else
                        {
                            if (_debugConsole != null && !_debugConsole.IsDisposed)
                            {
                                _debugConsole.LogError("Rewrite returned empty text, using original", null);
                            }
                            ShowToastNotification("Rewrite Warning", "Rewrite returned empty, using original text", ToolTipIcon.Warning);
                        }
                    }
                    catch (Exception ex)
                    {
                        if (_debugConsole != null && !_debugConsole.IsDisposed)
                        {
                            _debugConsole.LogError("Error rewriting text, using original", ex);
                        }
                        ShowToastNotification("Rewrite Error", "Failed to rewrite, using original text", ToolTipIcon.Warning);
                    }
                }

                // Now copy final text (rewritten or original) to clipboard and type it
                if (_accumulatedTranscription.Length > 0)
                {
                    var finalText = _accumulatedTranscription.ToString();
                    
                    // Copy to clipboard
                    try
                    {
                        Clipboard.SetText(finalText);
                        if (_debugConsole != null && !_debugConsole.IsDisposed)
                        {
                            _debugConsole.LogInfo($"Final text copied to clipboard ({finalText.Length} characters)");
                        }
                    }
                    catch (Exception ex)
                    {
                        if (_debugConsole != null && !_debugConsole.IsDisposed)
                        {
                            _debugConsole.LogError("Error copying text to clipboard", ex);
                        }
                    }

                    // Type in active window if enabled.
                    // If a Realtime live session already typed in the active window, the final
                    // text would just duplicate it (and overwrite live text with corrections),
                    // so we keep the clipboard authoritative and skip the final paste. Note we
                    // gate on _realtimeTypedAnything (not _dictationUsedRealtime), because
                    // FinalizeDictationRealtimeAsync resets _dictationUsedRealtime BEFORE this
                    // point. _realtimeTypedAnything is reset only in StartRecording / Cancel.
                    if (_settings.Typing.TypeInActiveWindow)
                    {
                        if (_realtimeTypedAnything)
                        {
                            if ((_settings.Translation.Enabled
                                    && !string.IsNullOrWhiteSpace(_settings.Translation.TargetLanguage))
                                || _settings.Rewrite.Enabled)
                            {
                                ShowToastNotification(
                                    "Realtime",
                                    "Translation/Rewrite applied to clipboard only (live typing kept original).",
                                    ToolTipIcon.Info);
                            }
                        }
                        else
                        {
                            // Don't add leading space - we're typing the complete final text
                            TypeTextInActiveWindow(finalText, addLeadingSpace: false);
                        }
                    }
                }

        _statusLabel.Text = "Ready";
        _statusLabel.ForeColor = FluentColors.AccentGlow;

        // Push to history + flip transcript card to DONE with final text
        if (_accumulatedTranscription.Length > 0)
        {
            PushHistoryEntry(_accumulatedTranscription.ToString());
            if (_currentView == MainView.Home)
            {
                ShowTranscriptCard("Last transcript", _accumulatedTranscription.ToString(), recording: false);
            }
        }
        }
        catch (Exception ex)
        {
            if (_debugConsole != null && !_debugConsole.IsDisposed)
            {
                _debugConsole.LogError("Error after recording stopped", ex);
            }
            MessageBox.Show($"Error: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            _statusLabel.Text = "\u2022  Ready";
            _statusLabel.ForeColor = FluentColors.AccentGlow;
        }
        finally
        {
            _cursorOverlay?.HideOverlay();
        }
    }

    private async void CancelRecording()
    {
        if (!_isRecording)
        {
            return;
        }

        _isRecording = false;
        _isPaused = false;
        
        // Hide wave visualization
        _wavePanel.Visible = false;
        _currentAudioLevel = 0f;
        _audioLevelHistory.Clear();
        
        // Stop wave timer
        _waveTimer?.Stop();
        _waveTimer?.Dispose();
        _waveTimer = null;
        
        // Unsubscribe from audio level events
        if (_audioCapture != null)
        {
            _audioCapture.AudioLevelChanged -= AudioCapture_AudioLevelChanged;
        }
        
        if (_debugConsole != null && !_debugConsole.IsDisposed)
        {
            _debugConsole.LogInfo("Cancelling audio capture (no transcription)...");
        }

        // Stop audio capture first
        _cancellationTokenSource?.Cancel();
        await Task.Delay(100); // Give a moment for graceful shutdown

        // Dispose audio capture without collecting audio
        try
        {
            _audioCapture?.DisposeAsync().AsTask().Wait(TimeSpan.FromSeconds(5));
        }
        catch (AggregateException ex) when (ex.InnerException is TaskCanceledException)
        {
            // Ignore cancellation exceptions during disposal
        }

        _audioCapture = null;
        _cancellationTokenSource?.Dispose();
        _cancellationTokenSource = null;

        if (_dictationUsedRealtime)
            await FinalizeDictationRealtimeAsync(userCancelled: true).ConfigureAwait(true);

        _startStopButton.Invalidate(); // Repaint
        _statusLabel.Text = "\u2716  Cancelled";
        _statusLabel.ForeColor = FluentColors.Warning;
        
        // Show toast notification
        ShowToastNotification("Koli", "Recording cancelled", ToolTipIcon.Info);
        
        // Clear accumulated transcription
        _accumulatedTranscription.Clear();
        _realtimeTypedLengthByItem.Clear();
        _realtimeTypedAnything = false;
        
        if (_debugConsole != null && !_debugConsole.IsDisposed)
        {
            _debugConsole.LogInfo("Recording cancelled - no transcription performed");
        }

        // Reset status after a brief moment
        await Task.Delay(1000);
        if (InvokeRequired)
        {
            BeginInvoke(new Action(() =>
            {
                _statusLabel.Text = "\u2022  Ready";
                _statusLabel.ForeColor = FluentColors.AccentGlow;
            }));
        }
        else
        {
            _statusLabel.Text = "\u2022  Ready";
            _statusLabel.ForeColor = FluentColors.AccentGlow;
        }
    }

    private async void TogglePause()
    {
        if (!_isRecording || _audioCapture == null)
        {
            return;
        }

        try
        {
            if (_isPaused)
            {
                // Resume recording
                await _audioCapture.ResumeAsync();
                _isPaused = false;
                _statusLabel.Text = "\uD83C\uDFA4  Recording...";
                _statusLabel.ForeColor = FluentColors.Success;
                
                // Resume wave visualization
                _waveTimer?.Start();
                
                if (_debugConsole != null && !_debugConsole.IsDisposed)
                {
                    _debugConsole.LogInfo("Recording resumed");
                }
                
                ShowToastNotification("Koli", "Recording resumed", ToolTipIcon.Info);
            }
            else
            {
                // Pause recording
                await _audioCapture.PauseAsync();
                _isPaused = true;
                _statusLabel.Text = "\u23F8  Paused";
                _statusLabel.ForeColor = FluentColors.Warning;
                
                // Pause wave visualization (stop timer but keep panel visible)
                _waveTimer?.Stop();
                _currentAudioLevel = 0f;
                _wavePanel.Invalidate();
                
                if (_debugConsole != null && !_debugConsole.IsDisposed)
                {
                    _debugConsole.LogInfo("Recording paused");
                }
                
                ShowToastNotification("Koli", "Recording paused", ToolTipIcon.Info);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error toggling pause: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            if (_debugConsole != null && !_debugConsole.IsDisposed)
            {
                _debugConsole.LogError("Error toggling pause", ex);
            }
        }
    }

    private void OnTranscriptionReceived(object? sender, string text)
    {
        if (InvokeRequired)
        {
            Invoke(new Action<object?, string>(OnTranscriptionReceived), sender, text);
            return;
        }

        AppendAccumulatedTranscription(text);
        // Live update the transcript card with the running accumulated text (only on Home)
        if (_currentView == MainView.Home)
            ShowTranscriptCard("Live transcript", _accumulatedTranscription.ToString(), recording: _isRecording);
    }

    private void OnDictationRealtimeTranscript(object? sender, RealtimeTranscriptEventArgs e)
    {
        if (InvokeRequired)
        {
            BeginInvoke(new Action(() => OnDictationRealtimeTranscript(sender, e)));
            return;
        }

        if (!e.IsFinal)
        {
            // Short status (kept).
            _statusLabel.Text = "Recording";
            // Live transcript preview in the card (only on Home view)
            if (_currentView == MainView.Home)
                ShowTranscriptCard("Live transcript", e.Text ?? string.Empty, recording: true);

            // Live typing: only the new delta, never the accumulated text.
            if (_settings.Typing.TypeInActiveWindow && !string.IsNullOrEmpty(e.Delta))
            {
                TypeRealtimeChunk(e.ItemId, e.Delta!);
            }
            return;
        }

        // Completed: source-of-truth final transcript for this utterance.
        var finalText = e.Text ?? string.Empty;

        // Two distinct paths:
        //   - Whisper Realtime emits ONLY 'completed' (no deltas). _realtimeTypedLengthByItem
        //     has no entry for this item, so we type the full transcript now.
        //   - gpt-realtime / gpt-4o-transcribe emit deltas first, then a 'completed' that may
        //     contain corrections (punctuation, casing). Length-based dedup overlapped corrections
        //     and produced duplicated characters at the boundary, so we now SKIP the completed
        //     typing entirely and trust the live deltas. The clipboard at end-of-recording still
        //     receives the canonical final transcript.
        if (_settings.Typing.TypeInActiveWindow && finalText.Length > 0)
        {
            var deltasAlreadyTyped = _realtimeTypedLengthByItem.TryGetValue(e.ItemId, out var n) && n > 0;
            if (!deltasAlreadyTyped)
                TypeRealtimeChunk(e.ItemId, finalText);
        }

        _realtimeTypedLengthByItem.Remove(e.ItemId);
        AppendAccumulatedTranscription(finalText.Trim());
        if (_currentView == MainView.Home)
            ShowTranscriptCard("Live transcript", _accumulatedTranscription.ToString(), recording: _isRecording);
    }

    /// <summary>
    /// Sends <paramref name="chunk"/> to the originally-focused window using a lightweight clipboard
    /// paste path tuned for high-frequency Realtime deltas: focus is restored only on the first
    /// chunk of the session, and we don't race a background task to restore the original clipboard
    /// between deltas (the final clipboard value is overwritten in <see cref="StopRecording"/>).
    /// Tracks per-item character count so we can dedupe between deltas and the final transcript.
    /// </summary>
    private void TypeRealtimeChunk(string itemId, string chunk)
    {
        if (string.IsNullOrEmpty(chunk))
            return;

        try
        {
            if (!_realtimeTypedAnything)
            {
                if (_targetWindowForTyping != IntPtr.Zero
                    && IsWindow(_targetWindowForTyping)
                    && _targetWindowForTyping != Handle)
                {
                    SetForegroundWindow(_targetWindowForTyping);
                    Application.DoEvents();
                    Thread.Sleep(80);
                }
            }

            try
            {
                Clipboard.SetText(chunk);
                SendKeys.SendWait("^v");
            }
            catch (Exception clipboardEx)
            {
                // Fall back to SendKeys character pump (slower but no clipboard dependency).
                if (_debugConsole != null && !_debugConsole.IsDisposed)
                    _debugConsole.LogError("Realtime clipboard paste failed, falling back to SendKeys", clipboardEx);
                SendKeysEscaped(chunk);
            }

            _realtimeTypedAnything = true;
            var current = _realtimeTypedLengthByItem.TryGetValue(itemId, out var n) ? n : 0;
            _realtimeTypedLengthByItem[itemId] = current + chunk.Length;
        }
        catch (Exception ex)
        {
            if (_debugConsole != null && !_debugConsole.IsDisposed)
                _debugConsole.LogError("Realtime live-typing failed for chunk", ex);
        }
    }

    /// <summary>
    /// SendKeys-safe character pump: emits each char individually so brace/parens/+^%~[ ] are
    /// escaped exactly once (chained <see cref="string.Replace(string, string)"/> calls would
    /// double-escape braces produced by previous replacements).
    /// </summary>
    private static void SendKeysEscaped(string text)
    {
        foreach (var c in text)
        {
            if (c == '\r')
                continue;
            if (c == '\n')
            {
                SendKeys.SendWait("{ENTER}");
                continue;
            }
            var s = c switch
            {
                '{' or '}' or '(' or ')' or '+' or '^' or '%' or '~' or '[' or ']' => "{" + c + "}",
                _ => c.ToString()
            };
            SendKeys.SendWait(s);
        }
    }

    private void AppendAccumulatedTranscription(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return;

        if (_accumulatedTranscription.Length > 0)
            _accumulatedTranscription.Append(' ');
        _accumulatedTranscription.Append(text);
    }

    private async Task FinalizeDictationRealtimeAsync(bool userCancelled)
    {
        if (_dictationRealtimeStt == null)
            return;

        if (userCancelled)
            await _dictationRealtimeStt.StopRealtimeTranscriptionAsync().ConfigureAwait(true);

        var wait = userCancelled ? TimeSpan.FromSeconds(8) : TimeSpan.FromSeconds(45);
        if (_dictationRealtimeTask != null)
            await Task.WhenAny(_dictationRealtimeTask, Task.Delay(wait)).ConfigureAwait(true);

        if (!userCancelled)
            await _dictationRealtimeStt.StopRealtimeTranscriptionAsync().ConfigureAwait(true);

        _dictationRealtimeStt.RealtimeTranscript -= OnDictationRealtimeTranscript;
        _dictationRealtimeStt.ErrorLogging -= OnErrorLogging;
        if (_debugConsole != null && !_debugConsole.IsDisposed)
        {
            _dictationRealtimeStt.RequestLogging -= OnRequestLogging;
            _dictationRealtimeStt.ResponseLogging -= OnResponseLogging;
        }

        await _dictationRealtimeStt.DisposeAsync().ConfigureAwait(true);
        _dictationRealtimeStt = null;
        _dictationRealtimeTask = null;
        _dictationUsedRealtime = false;
    }

    private void SetTranscriptionModel(string model)
    {
        if (OpenAiModelProfiles.IsOnPremiseStyleEndpoint(_settings.AzureOpenAI.Endpoint)
            && OpenAiModelProfiles.IsRealtimeTranscriptionModel(model))
        {
            MessageBox.Show(
                "Realtime transcription models require OpenAI's api.openai.com. Your current endpoint is not the public OpenAI API.",
                "Transcription model",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        _settings.AzureOpenAI.Model = model;
        try
        {
            _settings.Save(_configPath);
            ShowToastNotification("Transcription model", $"Model set to {model}", ToolTipIcon.Info);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error saving settings: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void RefreshTranscriptionModelMenuChecks(ToolStripMenuItem menu)
    {
        var onPrem = OpenAiModelProfiles.IsOnPremiseStyleEndpoint(_settings.AzureOpenAI.Endpoint);
        foreach (var obj in menu.DropDownItems)
        {
            if (obj is not ToolStripMenuItem item || item.Tag is not string id)
                continue;
            var isRt = OpenAiModelProfiles.IsRealtimeTranscriptionModel(id);
            item.Enabled = !(onPrem && isRt);
            item.Checked = id.Equals(_settings.AzureOpenAI.Model.Trim(), StringComparison.OrdinalIgnoreCase);
        }
    }

    private void TypeTextInActiveWindow(string text, bool addLeadingSpace = false)
    {
        try
        {
            // Restore focus to the window that had focus when recording started,
            // so the paste goes to the target app (Word, browser, etc.) instead of Koli
            if (_targetWindowForTyping != IntPtr.Zero && IsWindow(_targetWindowForTyping) && _targetWindowForTyping != Handle)
            {
                SetForegroundWindow(_targetWindowForTyping);
                Application.DoEvents();
                Thread.Sleep(80); // Brief delay so the target window is fully active before paste
            }

            // Add space if auto-space is enabled and requested (for chunk-by-chunk typing)
            if (_settings.Typing.AutoSpace && addLeadingSpace)
            {
                text = " " + text;
            }

            // Debug: Log the StreamingMode value to help diagnose issues
            if (_debugConsole != null && !_debugConsole.IsDisposed)
            {
                _debugConsole.LogInfo($"Typing mode: StreamingMode = {_settings.Typing.StreamingMode}");
            }

            if (_settings.Typing.StreamingMode)
            {
                // Letter-by-letter streaming mode with 10ms delay
                foreach (char c in text)
                {
                    if (c == '\r')
                    {
                        // Skip \r, handle \n separately
                        continue;
                    }
                    else if (c == '\n')
                    {
                        // Send Enter key as a single unit
                        SendKeys.SendWait("{ENTER}");
                    }
                    else
                    {
                        // Escape special characters for SendKeys
                        var charToSend = c.ToString();
                        if (c == '{' || c == '}' || c == '(' || c == ')' || c == '+' || c == '^' || c == '%' || c == '~' || c == '[' || c == ']')
                        {
                            charToSend = "{" + c + "}";
                        }
                        SendKeys.SendWait(charToSend);
                    }
                    
                    // 10ms delay between characters
                    Thread.Sleep(10);
                }
            }
            else
            {
                // All at once mode - use clipboard paste for instant insertion
                try
                {
                    // Save current clipboard content to restore it later
                    string? originalClipboard = null;
                    try
                    {
                        if (Clipboard.ContainsText())
                        {
                            originalClipboard = Clipboard.GetText();
                        }
                    }
                    catch
                    {
                        // Ignore errors when reading clipboard
                    }

                    // Set text to clipboard
                    Clipboard.SetText(text);

                    // Send Ctrl+V to paste
                    SendKeys.SendWait("^v");

                    // Restore original clipboard content after a short delay
                    if (originalClipboard != null)
                    {
                        Task.Run(async () =>
                        {
                            await Task.Delay(100); // Small delay to ensure paste completes
                            try
                            {
                                Clipboard.SetText(originalClipboard);
                            }
                            catch
                            {
                                // Ignore errors when restoring clipboard
                            }
                        });
                    }
                }
                catch (Exception clipboardEx)
                {
                    // Fallback to SendKeys if clipboard fails
                    if (_debugConsole != null && !_debugConsole.IsDisposed)
                    {
                        _debugConsole.LogError("Clipboard paste failed, falling back to SendKeys", clipboardEx);
                    }
                    var textToSend = text.Replace("\r\n", "{ENTER}").Replace("\n", "{ENTER}");
                    SendKeys.SendWait(textToSend);
                }
            }
        }
        catch (Exception ex)
        {
            if (_debugConsole != null && !_debugConsole.IsDisposed)
            {
                _debugConsole.LogError("Error sending keys to active window", ex);
            }
        }
    }

    // Removed ClearButton_Click method as button was removed

    private void InitializeSettingsMenu()
    {
        _settingsMenu = new ContextMenuStrip
        {
            BackColor = FluentColors.Surface,
            ForeColor = FluentColors.TextPrimary,
            Font = FluentFonts.Body,
            RenderMode = ToolStripRenderMode.Professional
        };
        
        // Custom renderer for Fluent Design styling
        _settingsMenu.Renderer = new FluentMenuRenderer();

        // Debug Console menu item
        var debugMenuItem = new ToolStripMenuItem("Debug Console")
        {
            ForeColor = FluentColors.TextPrimary,
            Font = FluentFonts.Body
        };
        debugMenuItem.Click += (s, e) => DebugButton_Click(s, e);
        _settingsMenu.Items.Add(debugMenuItem);

        // Edit Prompt menu item
        var promptMenuItem = new ToolStripMenuItem("Edit Prompt")
        {
            ForeColor = FluentColors.TextPrimary,
            Font = FluentFonts.Body
        };
        promptMenuItem.Click += (s, e) => PromptButton_Click(s, e);
        _settingsMenu.Items.Add(promptMenuItem);

        // Toggle Rewrite menu item
        _toggleRewriteMenuItem = new ToolStripMenuItem("Toggle Rewrite")
        {
            ForeColor = FluentColors.TextPrimary,
            Font = FluentFonts.Body
        };
        _toggleRewriteMenuItem.Click += (s, e) => RewriteToggleButton_Click(s, e);
        _settingsMenu.Items.Add(_toggleRewriteMenuItem);

        // Rewrite Settings menu item
        var rewriteSettingsMenuItem = new ToolStripMenuItem("Rewrite Settings...")
        {
            ForeColor = FluentColors.TextPrimary,
            Font = FluentFonts.Body
        };
        rewriteSettingsMenuItem.Click += (s, e) => RewritePromptButton_Click(s, e);
        _settingsMenu.Items.Add(rewriteSettingsMenuItem);

        // Separator
        _settingsMenu.Items.Add(new ToolStripSeparator());

        // API Configuration menu item
        var apiConfigMenuItem = new ToolStripMenuItem("API Configuration...")
        {
            ForeColor = FluentColors.TextPrimary,
            Font = FluentFonts.Body
        };
        apiConfigMenuItem.Click += ApiConfigurationButton_Click;
        _settingsMenu.Items.Add(apiConfigMenuItem);

        var transcriptionModelMenu = new ToolStripMenuItem("Transcription model")
        {
            ForeColor = FluentColors.TextPrimary,
            Font = FluentFonts.Body
        };
        transcriptionModelMenu.DropDownOpening += (_, _) => RefreshTranscriptionModelMenuChecks(transcriptionModelMenu);
        foreach (var (label, modelId) in new (string Label, string Id)[]
        {
            ("gpt-4o-transcribe (batch)", "gpt-4o-transcribe"),
            ("whisper-1 (batch)", "whisper-1"),
            ("gpt-realtime-whisper (live)", "gpt-realtime-whisper"),
            ("gpt-realtime (live)", "gpt-realtime"),
        })
        {
            var mi = new ToolStripMenuItem(label) { Tag = modelId };
            var id = modelId;
            mi.Click += (_, _) => SetTranscriptionModel(id);
            transcriptionModelMenu.DropDownItems.Add(mi);
        }

        _settingsMenu.Items.Add(transcriptionModelMenu);

        // Typing Settings menu item
        var typingSettingsMenuItem = new ToolStripMenuItem("Typing Settings...")
        {
            ForeColor = FluentColors.TextPrimary,
            Font = FluentFonts.Body
        };
        typingSettingsMenuItem.Click += (s, e) => TypingSettingsButton_Click(s, e);
        _settingsMenu.Items.Add(typingSettingsMenuItem);

        // Separator before Meeting Mode
        _settingsMenu.Items.Add(new ToolStripSeparator());

        // Meeting Mode menu item
        var meetingMenuItem = new ToolStripMenuItem("Meeting Mode...")
        {
            ForeColor = FluentColors.TextPrimary,
            Font = FluentFonts.Body
        };
        meetingMenuItem.Click += MeetingMode_Click;
        _settingsMenu.Items.Add(meetingMenuItem);

        // Update initial state
        UpdateSettingsMenuRewriteState();
    }

    private void InfoButton_Click(object? sender, EventArgs e)
    {
        using var dialog = new AboutDialog();
        dialog.ShowDialog(this);
    }

    private void SettingsButton_Click(object? sender, EventArgs e)
    {
        if (_settingsMenu != null && _settingsButton != null)
        {
            // Show menu at the bottom-left of the button
            _settingsMenu.Show(_settingsButton, new Point(0, _settingsButton.Height));
        }
    }

    private void MeetingMode_Click(object? sender, EventArgs e)
    {
        // Settings menu shortcut: route to the embedded Meeting view instead of a new window.
        SetActiveNav(_navMeeting);
        SwitchMainView(MainView.Meeting);
    }

    private void UpdateSettingsMenuRewriteState()
    {
        if (_toggleRewriteMenuItem != null)
        {
            _toggleRewriteMenuItem.Checked = _settings.Rewrite.Enabled;
            _toggleRewriteMenuItem.Text = _settings.Rewrite.Enabled ? "\u2713  Rewrite ON" : "Toggle Rewrite";
            _toggleRewriteMenuItem.ForeColor = _settings.Rewrite.Enabled
                ? FluentColors.Success
                : FluentColors.TextPrimary;
        }
    }

    private void DebugButton_Click(object? sender, EventArgs e)
    {
        // Settings menu shortcut: route to the embedded Debug view instead of a new window.
        SetActiveNav(_navDebug);
        SwitchMainView(MainView.Debug);
    }

    private void OnRequestLogging(object? sender, (string Method, string Url, Dictionary<string, string> Headers, string? Body) args)
    {
        _debugConsole?.LogRequest(args.Method, args.Url, args.Headers, args.Body);
    }

    private void OnResponseLogging(object? sender, (int StatusCode, string? StatusMessage, Dictionary<string, string> Headers, string? Body) args)
    {
        _debugConsole?.LogResponse(args.StatusCode, args.StatusMessage, args.Headers, args.Body);
    }

    private void OnErrorLogging(object? sender, (string Message, Exception? Exception) args)
    {
        _debugConsole?.LogError(args.Message, args.Exception);
        if (args.Message.StartsWith("Transcription API error", StringComparison.OrdinalIgnoreCase))
        {
            var msg = args.Message;
            if (InvokeRequired)
                BeginInvoke(new Action(() => ShowToastNotification("Transcription Error", msg, ToolTipIcon.Error)));
            else
                ShowToastNotification("Transcription Error", msg, ToolTipIcon.Error);
        }
    }

    private string GetLanguageDisplayName(string languageCode)
    {
        return languageCode.ToLower() switch
        {
            "he" => "עברית",
            "fr" => "Français",
            "en" => "English",
            _ => languageCode.ToUpper()
        };
    }

    private string GetLanguageCode(string displayName)
    {
        return displayName switch
        {
            "עברית" => "he",
            "Français" => "fr",
            "English" => "en",
            _ => "fr"
        };
    }

    private string GetLanguageShortName(string languageCode)
    {
        return languageCode.ToLower() switch
        {
            "he" => "עב",
            "fr" => "FR",
            "en" => "EN",
            _ => languageCode.ToUpper()
        };
    }

    private string GetLanguageButtonText()
    {
        // The top-left chip now represents the translation target (opt-in).
        // When translation is disabled the chip shows a discreet "Off" label;
        // when enabled it shows the short name of the target language.
        if (!_settings.Translation.Enabled
            || string.IsNullOrWhiteSpace(_settings.Translation.TargetLanguage))
        {
            return "\uE8C1 Off"; // \uE8C1 = Language / translate icon
        }

        var shortName = GetLanguageShortName(_settings.Translation.TargetLanguage);
        return $"\uE8C1 {shortName}";
    }

    private void LanguageButton_Click(object? sender, EventArgs e)
    {
        if (_isRecording)
        {
            MessageBox.Show("Please stop recording before changing translation settings.",
                "Recording in progress", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        ShowTranslationSelectionDialog();
    }

    /// <summary>
    /// Detects the current keyboard language and updates the settings accordingly.
    /// Only runs when LanguageMode is set to "Auto".
    /// </summary>
    private void UpdateLanguageFromKeyboard()
    {
        // Only auto-detect if in Auto mode
        if (_settings.AzureOpenAI.LanguageMode != "Auto")
        {
            return;
        }

        try
        {
            var langId = GetActiveKeyboardLanguageId();
            if (langId != 0)
            {
                UpdateLanguageFromKeyboardLayout(langId);
            }
        }
        catch (Exception ex)
        {
            // Log error but don't interrupt the application
            if (_debugConsole != null && !_debugConsole.IsDisposed)
            {
                _debugConsole.LogError($"Error detecting keyboard language: {ex.Message}", ex);
            }
        }
    }

    /// <summary>
    /// Retrieves the current keyboard layout language ID, preferring the foreground window's layout.
    /// </summary>
    private ushort GetActiveKeyboardLanguageId()
    {
        // Try to read the layout from the currently active window (what the user sees)
        var foregroundWindow = GetForegroundWindow();
        if (foregroundWindow != IntPtr.Zero)
        {
            var foregroundThreadId = GetWindowThreadProcessId(foregroundWindow, out _);
            if (foregroundThreadId != 0)
            {
                var foregroundLayout = GetKeyboardLayout(foregroundThreadId);
                if (foregroundLayout != IntPtr.Zero)
                {
                    return (ushort)(foregroundLayout.ToInt64() & 0xFFFF);
                }
            }
        }

        // Fallback to the current thread's layout if we cannot detect the foreground one
        var keyboardLayout = GetKeyboardLayout(0);
        return keyboardLayout == IntPtr.Zero ? (ushort)0 : (ushort)(keyboardLayout.ToInt64() & 0xFFFF);
    }
    
    /// <summary>
    /// Updates the language setting from a Windows language ID.
    /// Only runs when LanguageMode is set to "Auto".
    /// </summary>
    private void UpdateLanguageFromKeyboardLayout(ushort langId)
    {
        // Only auto-detect if in Auto mode
        if (_settings.AzureOpenAI.LanguageMode != "Auto")
        {
            return;
        }

        try
        {
            // Map language ID to language code
            var languageCode = MapLanguageIdToCode(langId);
            
            // Update settings if language changed
            if (_settings.AzureOpenAI.Language != languageCode)
            {
                _settings.AzureOpenAI.Language = languageCode;
                if (_speechToText != null)
                    _speechToText.CurrentLanguage = languageCode;

                // Update UI on the UI thread
                if (InvokeRequired)
                {
                    BeginInvoke(new Action(() =>
                    {
                        _languageButton.Text = GetLanguageButtonText();
                    }));
                }
                else
                {
                    _languageButton.Text = GetLanguageButtonText();
                }
                
                // Save settings
                try
                {
                    _settings.Save(_configPath);
                }
                catch (Exception ex)
                {
                    // Log error but don't show message to user for automatic updates
                    if (_debugConsole != null && !_debugConsole.IsDisposed)
                    {
                        _debugConsole.LogError($"Error saving language setting: {ex.Message}", ex);
                    }
                }
            }
            else
            {
                // Even if language didn't change, update the button text to reflect current state
                if (InvokeRequired)
                {
                    BeginInvoke(new Action(() =>
                    {
                        _languageButton.Text = GetLanguageButtonText();
                    }));
                }
                else
                {
                    _languageButton.Text = GetLanguageButtonText();
                }
            }
        }
        catch (Exception ex)
        {
            // Log error but don't interrupt the application
            if (_debugConsole != null && !_debugConsole.IsDisposed)
            {
                _debugConsole.LogError($"Error updating language from layout: {ex.Message}", ex);
            }
        }
    }
    
    /// <summary>
    /// Maps Windows language ID to ISO-639-1 language code.
    /// Works in invariant globalization mode by using direct language ID mapping.
    /// </summary>
    private string MapLanguageIdToCode(ushort langId)
    {
        // Common language IDs (LCID - Locale Identifier)
        // Hebrew: 0x040D (1037)
        // French: 0x040C (1036) or 0x080C (2060 for Canadian French)
        // English (US): 0x0409 (1033)
        // English (UK): 0x0809 (2057)
        
        // Extract primary language ID (low 10 bits)
        var primaryLangId = langId & 0x03FF;
        
        return primaryLangId switch
        {
            0x000D => "he", // Hebrew (0x040D, 0x080D, etc.)
            0x000C => "fr", // French (0x040C, 0x080C, 0x0C0C, etc.)
            0x0009 => "en", // English (0x0409, 0x0809, 0x0C09, etc.)
            _ => "en" // Default to English for unsupported languages
        };
    }

    private void AudioCapture_AudioLevelChanged(object? sender, float level)
    {
        if (InvokeRequired)
        {
            BeginInvoke(new Action<object?, float>(AudioCapture_AudioLevelChanged), sender, level);
            return;
        }
        
        _currentAudioLevel = level;

        var normalizedLevel = MathF.Min(1f, MathF.Max(0f, level));
        _waveEnergy = (_waveEnergy * 0.82f) + (normalizedLevel * 0.18f);
        
        // Add to history for smooth wave scrolling
        _audioLevelHistory.Enqueue(level);
        while (_audioLevelHistory.Count > MaxHistorySize)
        {
            _audioLevelHistory.Dequeue();
        }
    }
    
    private void WavePanel_Paint(object? sender, PaintEventArgs e)
    {
        var panel = (Panel)sender!;
        var width = panel.Width;
        var height = panel.Height;

        if (width <= 1 || height <= 1)
            return;

        var graphics = e.Graphics;
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.CompositingQuality = CompositingQuality.HighQuality;
        graphics.Clear(Color.Transparent);

        // Draw background with rounded top corners
        using (var backgroundPath = new GraphicsPath())
        {
            var rect = panel.ClientRectangle;
            int cornerRadius = 8;
            
            // Add rounded top corners, square bottom corners
            backgroundPath.AddArc(rect.X, rect.Y, cornerRadius * 2, cornerRadius * 2, 180, 90);
            backgroundPath.AddArc(rect.Right - cornerRadius * 2, rect.Y, cornerRadius * 2, cornerRadius * 2, 270, 90);
            backgroundPath.AddLine(rect.Right, rect.Y + cornerRadius, rect.Right, rect.Bottom);
            backgroundPath.AddLine(rect.Right, rect.Bottom, rect.X, rect.Bottom);
            backgroundPath.AddLine(rect.X, rect.Bottom, rect.X, rect.Y + cornerRadius);
            backgroundPath.CloseFigure();
            
            using (var background = new LinearGradientBrush(panel.ClientRectangle,
                       Color.FromArgb(10, 10, 16),
                       Color.FromArgb(6, 6, 10),
                       LinearGradientMode.Vertical))
            {
                graphics.FillPath(background, backgroundPath);
            }
        }

        var centerY = height / 2f;
        var baseAmplitude = height * 0.45f;

        if (!_isRecording || _audioLevelHistory.Count < 2 || _isPaused)
        {
            DrawIdleBaseline(graphics, width, centerY);
            return;
        }

        var envelope = BuildWaveEnvelope(width);
        var time = (float)_waveStopwatch.Elapsed.TotalSeconds;

        foreach (var layer in _waveLayers)
        {
            DrawWaveLayer(graphics, envelope, baseAmplitude, centerY, width, time, layer);
        }

        DrawHighlightRibbon(graphics, envelope, baseAmplitude, centerY, width, time);
    }

    private float[] BuildWaveEnvelope(int width)
    {
        var targetWidth = Math.Max(2, width);
        var envelope = new float[targetWidth];

        if (_audioLevelHistory.Count == 0)
        {
            for (int i = 0; i < envelope.Length; i++)
            {
                envelope[i] = 0.2f;
            }
            return envelope;
        }

        var history = _audioLevelHistory.ToArray();
        for (int x = 0; x < envelope.Length; x++)
        {
            var progress = 1f - (float)x / (envelope.Length - 1);
            var sampleIndex = progress * (history.Length - 1);
            var leftIndex = (int)MathF.Floor(sampleIndex);
            var rightIndex = Math.Min(leftIndex + 1, history.Length - 1);
            var lerpAmount = sampleIndex - leftIndex;
            var sample = history[leftIndex] + (history[rightIndex] - history[leftIndex]) * lerpAmount;
            var eased = MathF.Pow(MathF.Max(0f, sample), 1.25f);
            envelope[x] = 0.15f + (eased * 0.85f);
        }

        return envelope;
    }

    private void DrawWaveLayer(Graphics graphics, float[] envelope, float baseAmplitude, float centerY, int width, float time, WaveLayer layer)
    {
        if (width < 3)
            return;

        var crest = new PointF[width];
        var tau = MathF.PI * 2f;

        for (int x = 0; x < width; x++)
        {
            var progress = (float)x / (width - 1);
            var env = envelope[Math.Min(x, envelope.Length - 1)];
            var carrier = MathF.Sin((progress * layer.Frequency + time * layer.Speed) * tau);
            var detail = MathF.Sin((progress * layer.DetailFrequency + time * (layer.Speed * 1.6f)) * tau) * layer.DetailAmplitude;
            var shimmer = MathF.Sin(((progress * layer.DetailFrequency * 2f) - time * (layer.Speed * 0.65f)) * tau) * 0.08f;
            var amplitude = baseAmplitude * layer.AmplitudeMultiplier * env;
            var y = centerY + ((carrier + detail + shimmer) * amplitude) + (layer.VerticalShift * baseAmplitude);
            crest[x] = new PointF(x, y);
        }

        using (var fillPath = CreateWaveFillPath(crest, width, centerY + baseAmplitude + 6f, layer.Smoothness))
        {
            var fillRect = new RectangleF(0, centerY - baseAmplitude, width, baseAmplitude * 2f);
            using var brush = new LinearGradientBrush(
                fillRect,
                Color.FromArgb(layer.FillAlpha, layer.FillColor),
                Color.FromArgb(10, layer.FillColor),
                LinearGradientMode.Vertical);
            graphics.FillPath(brush, fillPath);
        }

        if (layer.GlowAlpha > 0)
        {
            using var glowPen = new Pen(Color.FromArgb(layer.GlowAlpha, layer.GlowColor), layer.OutlineWidth * 3f)
            {
                LineJoin = LineJoin.Round
            };
            graphics.DrawCurve(glowPen, crest, layer.Smoothness);
        }

        using var outlinePen = new Pen(Color.FromArgb(layer.OutlineAlpha, layer.OutlineColor), layer.OutlineWidth)
        {
            LineJoin = LineJoin.Round
        };
        graphics.DrawCurve(outlinePen, crest, layer.Smoothness);
    }

    private static GraphicsPath CreateWaveFillPath(PointF[] crest, int width, float baselineY, float tension)
    {
        var path = new GraphicsPath();
        if (crest.Length < 2)
            return path;

        path.AddCurve(crest, tension);
        var last = crest[crest.Length - 1];
        var first = crest[0];
        path.AddLine(last, new PointF(width, baselineY));
        path.AddLine(new PointF(width, baselineY), new PointF(0, baselineY));
        path.AddLine(new PointF(0, baselineY), first);
        path.CloseFigure();
        return path;
    }

    private void DrawHighlightRibbon(Graphics graphics, float[] envelope, float baseAmplitude, float centerY, int width, float time)
    {
        if (width < 3)
            return;

        var highlightPoints = new PointF[width];
        var energy = MathF.Min(1f, MathF.Max(0f, _waveEnergy));

        for (int x = 0; x < width; x++)
        {
            var progress = (float)x / (width - 1);
            var sparkle = MathF.Sin((progress * 6f) + time * 2.1f) * 0.05f;
            var y = centerY - (envelope[x] - 0.15f + sparkle) * baseAmplitude * 0.18f - 2f;
            highlightPoints[x] = new PointF(x, y);
        }

        var glowAlpha = (int)(80 + energy * 140f);
        using (var glowPen = new Pen(Color.FromArgb(Math.Min(255, glowAlpha), Color.White), 1.4f)
        {
            LineJoin = LineJoin.Round
        })
        {
            graphics.DrawCurve(glowPen, highlightPoints, 0.65f);
        }

        var rimAlpha = Math.Max(0, glowAlpha - 20);
        using var rimPen = new Pen(Color.FromArgb(rimAlpha, Color.FromArgb(180, 230, 255)), 0.7f)
        {
            LineJoin = LineJoin.Round
        };
        graphics.DrawCurve(rimPen, highlightPoints, 0.65f);
    }

    private static void DrawIdleBaseline(Graphics graphics, int width, float centerY)
    {
        using var pen = new Pen(Color.FromArgb(90, 130, 160, 190), 1.3f)
        {
            LineJoin = LineJoin.Round
        };
        graphics.DrawLine(pen, 0, centerY, width, centerY);
    }

    private readonly struct WaveLayer
    {
        public WaveLayer(
            float frequency,
            float speed,
            float detailFrequency,
            float detailAmplitude,
            float amplitudeMultiplier,
            float verticalShift,
            float smoothness,
            float outlineWidth,
            int fillAlpha,
            int glowAlpha,
            int outlineAlpha,
            Color fillColor,
            Color glowColor,
            Color outlineColor)
        {
            Frequency = frequency;
            Speed = speed;
            DetailFrequency = detailFrequency;
            DetailAmplitude = detailAmplitude;
            AmplitudeMultiplier = amplitudeMultiplier;
            VerticalShift = verticalShift;
            Smoothness = smoothness;
            OutlineWidth = outlineWidth;
            FillAlpha = fillAlpha;
            GlowAlpha = glowAlpha;
            OutlineAlpha = outlineAlpha;
            FillColor = fillColor;
            GlowColor = glowColor;
            OutlineColor = outlineColor;
        }

        public float Frequency { get; }
        public float Speed { get; }
        public float DetailFrequency { get; }
        public float DetailAmplitude { get; }
        public float AmplitudeMultiplier { get; }
        public float VerticalShift { get; }
        public float Smoothness { get; }
        public float OutlineWidth { get; }
        public int FillAlpha { get; }
        public int GlowAlpha { get; }
        public int OutlineAlpha { get; }
        public Color FillColor { get; }
        public Color GlowColor { get; }
        public Color OutlineColor { get; }
    }

    private sealed class WaveSurfacePanel : Panel
    {
        public WaveSurfacePanel()
        {
            DoubleBuffered = true;
            ResizeRedraw = true;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
            UpdateStyles();
        }
    }
    
    private void ShowTranslationSelectionDialog()
    {
        using var dialog = new Form
        {
            Text = "Translation",
            Size = new Size(460, 360),
            StartPosition = FormStartPosition.CenterParent,
            FormBorderStyle = FormBorderStyle.None,
            MaximizeBox = false,
            MinimizeBox = false,
            BackColor = FluentColors.Background,
            ShowInTaskbar = false
        };

        ApplyModernDialogStyle(dialog);

        var titleBar = new Panel
        {
            Dock = DockStyle.Top,
            Height = 52,
            BackColor = FluentColors.Surface
        };

        var titleLabel = new Label
        {
            Text = "\uE8C1  Translation",
            Font = new Font("Segoe UI", 12F, FontStyle.Bold),
            ForeColor = FluentColors.TextPrimary,
            AutoSize = true,
            Location = new Point(20, 16)
        };
        titleBar.Controls.Add(titleLabel);

        titleBar.MouseDown += (s, e) =>
        {
            if (e.Button == MouseButtons.Left)
            {
                ReleaseCapture();
                SendMessage(dialog.Handle, WM_NCLBUTTONDOWN, HT_CAPTION, 0);
            }
        };

        var mainPanel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = FluentColors.Background,
            Padding = new Padding(20, 12, 20, 0)
        };

        var enabledCheckBox = new CheckBox
        {
            Text = "  Translate transcription into another language",
            Location = new Point(0, 4),
            Size = new Size(400, 28),
            ForeColor = FluentColors.TextPrimary,
            Font = FluentFonts.Body,
            Checked = _settings.Translation.Enabled
                      && !string.IsNullOrWhiteSpace(_settings.Translation.TargetLanguage)
        };

        var languageGroupBox = new GroupBox
        {
            Text = "Target language",
            Location = new Point(0, 40),
            Size = new Size(400, 170),
            ForeColor = FluentColors.TextPrimary,
            Font = FluentFonts.Body,
            BackColor = FluentColors.Background
        };

        var englishRadio = new RadioButton
        {
            Text = "  English",
            Location = new Point(14, 32),
            Size = new Size(360, 28),
            ForeColor = FluentColors.TextPrimary,
            Font = FluentFonts.Caption,
            Checked = _settings.Translation.TargetLanguage == "en"
        };

        var frenchRadio = new RadioButton
        {
            Text = "  Français (French)",
            Location = new Point(14, 72),
            Size = new Size(360, 28),
            ForeColor = FluentColors.TextPrimary,
            Font = FluentFonts.Caption,
            Checked = _settings.Translation.TargetLanguage == "fr"
        };

        var hebrewRadio = new RadioButton
        {
            Text = "  עברית (Hebrew)",
            Location = new Point(14, 112),
            Size = new Size(360, 28),
            ForeColor = FluentColors.TextPrimary,
            Font = FluentFonts.Caption,
            Checked = _settings.Translation.TargetLanguage == "he"
        };

        // Pre-select English when nothing is stored yet, so enabling the checkbox has a sensible default.
        if (!englishRadio.Checked && !frenchRadio.Checked && !hebrewRadio.Checked)
            englishRadio.Checked = true;

        languageGroupBox.Controls.Add(englishRadio);
        languageGroupBox.Controls.Add(frenchRadio);
        languageGroupBox.Controls.Add(hebrewRadio);

        // Radios stay visually enabled at all times (white text on the dark background)
        // regardless of the master checkbox; the save logic below already ignores their
        // values when translation is disabled, so no grayed-out state is needed.

        mainPanel.Controls.Add(enabledCheckBox);
        mainPanel.Controls.Add(languageGroupBox);

        var buttonPanel = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 60,
            BackColor = FluentColors.Background
        };

        var okButton = new Button
        {
            Text = "Save",
            Size = new Size(90, 36),
            Location = new Point(265, 12),
            FlatStyle = FlatStyle.Flat,
            ForeColor = FluentColors.TextPrimary,
            BackColor = FluentColors.AccentPrimary,
            DialogResult = DialogResult.OK,
            Font = FluentFonts.ButtonText,
            Cursor = Cursors.Hand
        };
        okButton.FlatAppearance.BorderSize = 0;
        okButton.FlatAppearance.MouseDownBackColor = FluentColors.AccentPressed;
        okButton.FlatAppearance.MouseOverBackColor = FluentColors.AccentHover;

        var cancelButton = new Button
        {
            Text = "Cancel",
            Size = new Size(90, 36),
            Location = new Point(365, 12),
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

        buttonPanel.Controls.Add(okButton);
        buttonPanel.Controls.Add(cancelButton);

        dialog.Controls.Add(mainPanel);
        dialog.Controls.Add(titleBar);
        dialog.Controls.Add(buttonPanel);
        dialog.AcceptButton = okButton;
        dialog.CancelButton = cancelButton;

        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            if (enabledCheckBox.Checked)
            {
                _settings.Translation.Enabled = true;
                if (frenchRadio.Checked)
                    _settings.Translation.TargetLanguage = "fr";
                else if (hebrewRadio.Checked)
                    _settings.Translation.TargetLanguage = "he";
                else
                    _settings.Translation.TargetLanguage = "en";
            }
            else
            {
                _settings.Translation.Enabled = false;
                _settings.Translation.TargetLanguage = "";
            }

            try
            {
                _settings.Save(_configPath);
                _languageButton.Text = GetLanguageButtonText();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving settings: {ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }

    private void PromptButton_Click(object? sender, EventArgs e)
    {
        if (_isRecording)
        {
            MessageBox.Show("Please stop recording before modifying the prompt.", "Recording in progress", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        using var dialog = new Form
        {
            Text = "Edit prompt",
            Size = new Size(520, 360),
            StartPosition = FormStartPosition.CenterParent,
            FormBorderStyle = FormBorderStyle.None,
            MaximizeBox = false,
            MinimizeBox = false,
            BackColor = FluentColors.Background,
            ShowInTaskbar = false
        };

        // Apply modern styling
        ApplyModernDialogStyle(dialog);

        // Title bar
        var titleBar = new Panel
        {
            Dock = DockStyle.Top,
            Height = 52,
            BackColor = FluentColors.Surface
        };

        var titleLabel = new Label
        {
            Text = "\uE70F  Edit Prompt",
            Font = new Font("Segoe UI", 12F, FontStyle.Bold),
            ForeColor = FluentColors.TextPrimary,
            AutoSize = true,
            Location = new Point(20, 16)
        };
        titleBar.Controls.Add(titleLabel);
        
        titleBar.MouseDown += (s, e) => {
            if (e.Button == MouseButtons.Left)
            {
                ReleaseCapture();
                SendMessage(dialog.Handle, WM_NCLBUTTONDOWN, HT_CAPTION, 0);
            }
        };
        
        var contentPanel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = FluentColors.Background,
            Padding = new Padding(16, 12, 16, 0)
        };

        var textBox = new TextBox
        {
            Multiline = true,
            Dock = DockStyle.Fill,
            Text = _settings.AzureOpenAI.Prompt,
            BackColor = FluentColors.Surface,
            ForeColor = FluentColors.TextPrimary,
            Font = new Font("Segoe UI", 10F),
            ScrollBars = ScrollBars.Vertical,
            BorderStyle = BorderStyle.FixedSingle
        };

        contentPanel.Controls.Add(textBox);

        var buttonPanel = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 60,
            BackColor = FluentColors.Background
        };

        var okButton = new Button
        {
            Text = "Save",
            Size = new Size(90, 36),
            Location = new Point(315, 12),
            FlatStyle = FlatStyle.Flat,
            ForeColor = FluentColors.TextPrimary,
            BackColor = FluentColors.AccentPrimary,
            DialogResult = DialogResult.OK,
            Font = FluentFonts.ButtonText,
            Cursor = Cursors.Hand
        };
        okButton.FlatAppearance.BorderSize = 0;
        okButton.FlatAppearance.MouseDownBackColor = FluentColors.AccentPressed;
        okButton.FlatAppearance.MouseOverBackColor = FluentColors.AccentHover;

        var cancelButton = new Button
        {
            Text = "Cancel",
            Size = new Size(90, 36),
            Location = new Point(415, 12),
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

        buttonPanel.Controls.Add(okButton);
        buttonPanel.Controls.Add(cancelButton);
        dialog.Controls.Add(contentPanel);
        dialog.Controls.Add(titleBar);
        dialog.Controls.Add(buttonPanel);
        dialog.AcceptButton = okButton;
        dialog.CancelButton = cancelButton;

        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            _settings.AzureOpenAI.Prompt = textBox.Text;
            
            try
            {
                _settings.Save(_configPath);
                MessageBox.Show("Prompt updated successfully.", "Prompt updated", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }

    private void RewriteToggleButton_Click(object? sender, EventArgs e)
    {
        if (_isRecording)
        {
            MessageBox.Show("Please stop recording before changing rewrite settings.", "Recording in progress", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        _settings.Rewrite.Enabled = !_settings.Rewrite.Enabled;
        UpdateRewriteButtonState();
        UpdateSettingsMenuRewriteState(); // Update menu state
        
        try
        {
            _settings.Save(_configPath);
            ShowToastNotification("Rewrite Mode", 
                _settings.Rewrite.Enabled ? "Rewrite enabled" : "Rewrite disabled", 
                ToolTipIcon.Info);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error saving settings: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void RewritePromptButton_Click(object? sender, EventArgs e)
    {
        if (_isRecording)
        {
            MessageBox.Show("Please stop recording before modifying the rewrite settings.", "Recording in progress", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        using var dialog = new Form
        {
            Text = "Rewrite Settings",
            Size = new Size(560, 540),
            StartPosition = FormStartPosition.CenterParent,
            FormBorderStyle = FormBorderStyle.None,
            MaximizeBox = false,
            MinimizeBox = false,
            BackColor = FluentColors.Background,
            ShowInTaskbar = false
        };

        // Apply modern styling
        ApplyModernDialogStyle(dialog);

        // Title bar
        var titleBar = new Panel
        {
            Dock = DockStyle.Top,
            Height = 52,
            BackColor = FluentColors.Surface
        };

        var titleLabel = new Label
        {
            Text = "\uE8C8  Rewrite Settings",
            Font = new Font("Segoe UI", 12F, FontStyle.Bold),
            ForeColor = FluentColors.TextPrimary,
            AutoSize = true,
            Location = new Point(20, 16)
        };
        titleBar.Controls.Add(titleLabel);
        
        titleBar.MouseDown += (s, e) => {
            if (e.Button == MouseButtons.Left)
            {
                ReleaseCapture();
                SendMessage(dialog.Handle, WM_NCLBUTTONDOWN, HT_CAPTION, 0);
            }
        };

        var mainPanel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = FluentColors.Background,
            Padding = new Padding(20, 12, 20, 0),
            AutoScroll = true
        };

        // Level selection group
        var levelGroupBox = new GroupBox
        {
            Text = "Professionalism Level",
            Location = new Point(0, 0),
            Size = new Size(500, 210),
            ForeColor = FluentColors.TextPrimary,
            Font = FluentFonts.Body,
            BackColor = FluentColors.Background
        };

        var casualRadio = new RadioButton
        {
            Text = "  Casual - Slightly polished, conversational tone",
            Location = new Point(10, 32),
            Size = new Size(470, 28),
            ForeColor = FluentColors.TextSecondary,
            Font = FluentFonts.Caption,
            Checked = _settings.Rewrite.ProfessionalismLevel == "Casual"
        };

        var polishedRadio = new RadioButton
        {
            Text = "  Polished - Clear and natural, approachable",
            Location = new Point(10, 64),
            Size = new Size(470, 28),
            ForeColor = FluentColors.TextSecondary,
            Font = FluentFonts.Caption,
            Checked = _settings.Rewrite.ProfessionalismLevel == "Polished"
        };

        var professionalRadio = new RadioButton
        {
            Text = "  Professional - Business-appropriate, formal",
            Location = new Point(10, 96),
            Size = new Size(470, 28),
            ForeColor = FluentColors.TextSecondary,
            Font = FluentFonts.Caption,
            Checked = _settings.Rewrite.ProfessionalismLevel == "Professional"
        };

        var formalRadio = new RadioButton
        {
            Text = "  Formal - Highly professional, sophisticated",
            Location = new Point(10, 128),
            Size = new Size(470, 28),
            ForeColor = FluentColors.TextSecondary,
            Font = FluentFonts.Caption,
            Checked = _settings.Rewrite.ProfessionalismLevel == "Formal"
        };

        var executiveRadio = new RadioButton
        {
            Text = "  Executive - Corporate, authoritative style",
            Location = new Point(10, 160),
            Size = new Size(470, 28),
            ForeColor = FluentColors.TextSecondary,
            Font = FluentFonts.Caption,
            Checked = _settings.Rewrite.ProfessionalismLevel == "Executive"
        };

        levelGroupBox.Controls.Add(casualRadio);
        levelGroupBox.Controls.Add(polishedRadio);
        levelGroupBox.Controls.Add(professionalRadio);
        levelGroupBox.Controls.Add(formalRadio);
        levelGroupBox.Controls.Add(executiveRadio);

        // Custom prompt group
        var customGroupBox = new GroupBox
        {
            Text = "Custom Prompt (Optional - overrides level selection)",
            Location = new Point(0, 220),
            Size = new Size(500, 150),
            ForeColor = FluentColors.TextPrimary,
            Font = FluentFonts.Body,
            BackColor = FluentColors.Background
        };

        var customTextBox = new TextBox
        {
            Multiline = true,
            Location = new Point(10, 28),
            Size = new Size(480, 108),
            Text = _settings.Rewrite.Prompt,
            BackColor = FluentColors.Surface,
            ForeColor = FluentColors.TextPrimary,
            Font = new Font("Segoe UI", 9.5F),
            ScrollBars = ScrollBars.Vertical,
            BorderStyle = BorderStyle.FixedSingle
        };

        customGroupBox.Controls.Add(customTextBox);

        mainPanel.Controls.Add(levelGroupBox);
        mainPanel.Controls.Add(customGroupBox);

        // Buttons
        var buttonPanel = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 60,
            BackColor = FluentColors.Background
        };

        var okButton = new Button
        {
            Text = "Save",
            Size = new Size(90, 36),
            Location = new Point(365, 12),
            FlatStyle = FlatStyle.Flat,
            ForeColor = FluentColors.TextPrimary,
            BackColor = FluentColors.AccentPrimary,
            DialogResult = DialogResult.OK,
            Font = FluentFonts.ButtonText,
            Cursor = Cursors.Hand
        };
        okButton.FlatAppearance.BorderSize = 0;
        okButton.FlatAppearance.MouseDownBackColor = FluentColors.AccentPressed;
        okButton.FlatAppearance.MouseOverBackColor = FluentColors.AccentHover;

        var cancelButton = new Button
        {
            Text = "Cancel",
            Size = new Size(90, 36),
            Location = new Point(465, 12),
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

        buttonPanel.Controls.Add(okButton);
        buttonPanel.Controls.Add(cancelButton);

        dialog.Controls.Add(mainPanel);
        dialog.Controls.Add(titleBar);
        dialog.Controls.Add(buttonPanel);
        dialog.AcceptButton = okButton;
        dialog.CancelButton = cancelButton;

        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            // Determine selected level
            if (casualRadio.Checked)
                _settings.Rewrite.ProfessionalismLevel = "Casual";
            else if (polishedRadio.Checked)
                _settings.Rewrite.ProfessionalismLevel = "Polished";
            else if (professionalRadio.Checked)
                _settings.Rewrite.ProfessionalismLevel = "Professional";
            else if (formalRadio.Checked)
                _settings.Rewrite.ProfessionalismLevel = "Formal";
            else if (executiveRadio.Checked)
                _settings.Rewrite.ProfessionalismLevel = "Executive";

            // Save custom prompt if provided
            _settings.Rewrite.Prompt = customTextBox.Text;
            
            try
            {
                _settings.Save(_configPath);
                MessageBox.Show($"Rewrite settings updated successfully.\nLevel: {_settings.Rewrite.ProfessionalismLevel}", "Settings updated", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }

    private void UpdateRewriteButtonState()
    {
        if (_rewriteToggleButton == null) return;
        
        _rewriteToggleButton.ForeColor = _settings.Rewrite.Enabled ? FluentColors.Success : FluentColors.TextSecondary;
        
        var levelText = _settings.Rewrite.ProfessionalismLevel switch
        {
            "Casual" => "Casual",
            "Polished" => "Polished",
            "Professional" => "Professional",
            "Formal" => "Formal",
            "Executive" => "Executive",
            _ => "Custom"
        };
        
        _rewriteTooltip?.SetToolTip(_rewriteToggleButton, 
            _settings.Rewrite.Enabled 
                ? $"Rewrite enabled - Level: {levelText}\n(Left click: toggle, Right click: settings)" 
                : $"Rewrite disabled - Level: {levelText}\n(Left click: toggle, Right click: settings)");
    }

    private void TypingSettingsButton_Click(object? sender, EventArgs e)
    {
        if (_isRecording)
        {
            MessageBox.Show("Please stop recording before changing typing settings.", "Recording in progress", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        ShowTypingSettingsDialog();
    }

    private void ApiConfigurationButton_Click(object? sender, EventArgs e)
    {
        if (_isRecording)
        {
            MessageBox.Show("Please stop recording before changing the API configuration.", "Recording in progress", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        using var dialog = new ApiConfigurationDialog(_settings.AzureOpenAI, isStartup: false);
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        try
        {
            _settings.Save(_configPath);
            ShowToastNotification("API Configuration", "Settings updated", ToolTipIcon.Info);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error saving settings: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void ShowTypingSettingsDialog()
    {
        using var dialog = new Form
        {
            Text = "Typing Settings",
            Size = new Size(400, 260),
            StartPosition = FormStartPosition.CenterParent,
            FormBorderStyle = FormBorderStyle.None,
            MaximizeBox = false,
            MinimizeBox = false,
            BackColor = FluentColors.Background,
            ShowInTaskbar = false
        };

        // Apply modern styling
        ApplyModernDialogStyle(dialog);

        // Title bar
        var titleBar = new Panel
        {
            Dock = DockStyle.Top,
            Height = 52,
            BackColor = FluentColors.Surface
        };

        var titleLabel = new Label
        {
            Text = "\uE144  Typing Settings",
            Font = new Font("Segoe UI", 12F, FontStyle.Bold),
            ForeColor = FluentColors.TextPrimary,
            AutoSize = true,
            Location = new Point(20, 16)
        };
        titleBar.Controls.Add(titleLabel);
        
        titleBar.MouseDown += (s, e) => {
            if (e.Button == MouseButtons.Left)
            {
                ReleaseCapture();
                SendMessage(dialog.Handle, WM_NCLBUTTONDOWN, HT_CAPTION, 0);
            }
        };

        var mainPanel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = FluentColors.Background,
            Padding = new Padding(20)
        };

        // Typing mode selection group
        var modeGroupBox = new GroupBox
        {
            Text = "Typing Mode",
            Location = new Point(0, 0),
            Size = new Size(340, 100),
            ForeColor = FluentColors.TextPrimary,
            Font = FluentFonts.Body,
            BackColor = FluentColors.Background
        };

        var allAtOnceRadio = new RadioButton
        {
            Text = "  All at once (instant)",
            Location = new Point(10, 28),
            AutoSize = true,
            ForeColor = FluentColors.TextSecondary,
            Font = FluentFonts.Caption,
            Checked = !_settings.Typing.StreamingMode
        };

        var streamingRadio = new RadioButton
        {
            Text = "  Letter by letter (streaming)",
            Location = new Point(10, 55),
            AutoSize = true,
            ForeColor = FluentColors.TextSecondary,
            Font = FluentFonts.Caption,
            Checked = _settings.Typing.StreamingMode
        };

        modeGroupBox.Controls.Add(allAtOnceRadio);
        modeGroupBox.Controls.Add(streamingRadio);
        mainPanel.Controls.Add(modeGroupBox);

        // Buttons
        var buttonPanel = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 60,
            BackColor = FluentColors.Background
        };

        var okButton = new Button
        {
            Text = "Save",
            Size = new Size(100, 32),
            Location = new Point(220, 14),
            FlatStyle = FlatStyle.Flat,
            BackColor = FluentColors.AccentPrimary,
            ForeColor = Color.White,
            Font = FluentFonts.ButtonText,
            Cursor = Cursors.Hand,
            DialogResult = DialogResult.OK
        };
        okButton.FlatAppearance.BorderSize = 0;
        okButton.FlatAppearance.MouseDownBackColor = FluentColors.AccentPressed;
        okButton.FlatAppearance.MouseOverBackColor = FluentColors.AccentHover;

        var cancelButton = new Button
        {
            Text = "Cancel",
            Size = new Size(100, 32),
            Location = new Point(110, 14),
            FlatStyle = FlatStyle.Flat,
            BackColor = FluentColors.Surface,
            ForeColor = FluentColors.TextPrimary,
            Font = FluentFonts.ButtonText,
            Cursor = Cursors.Hand,
            DialogResult = DialogResult.Cancel
        };
        cancelButton.FlatAppearance.BorderSize = 0;
        cancelButton.FlatAppearance.MouseDownBackColor = FluentColors.SurfaceHover;
        cancelButton.FlatAppearance.MouseOverBackColor = FluentColors.SurfaceHover;

        buttonPanel.Controls.Add(okButton);
        buttonPanel.Controls.Add(cancelButton);

        dialog.Controls.Add(mainPanel);
        dialog.Controls.Add(titleBar);
        dialog.Controls.Add(buttonPanel);
        dialog.AcceptButton = okButton;
        dialog.CancelButton = cancelButton;

        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            _settings.Typing.StreamingMode = streamingRadio.Checked;
            
            try
            {
                _settings.Save(_configPath);
                ShowToastNotification("Typing Settings", 
                    _settings.Typing.StreamingMode ? "Streaming mode enabled" : "All at once mode enabled", 
                    ToolTipIcon.Info);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            // Persist history before tearing down
            SaveHistory();

            // Unregister global hotkeys
            UnregisterHotKey(Handle, HOTKEY_ID);
            UnregisterHotKey(Handle, HOTKEY_ID_F7);
            UnregisterHotKey(Handle, HOTKEY_ID_F6);
            StopRecording();
            _waveTimer?.Stop();
            _waveTimer?.Dispose();
            _languageMonitorTimer?.Stop();
            _languageMonitorTimer?.Dispose();
            _timerTickTimer?.Stop();
            _timerTickTimer?.Dispose();
            _notifyIcon?.Dispose();
            _cursorOverlay?.Dispose();
            _cursorOverlay = null;
            _rewriteTooltip?.Dispose();
            _settingsMenu?.Dispose();
            
            // Remove message filter
            if (_messageFilter != null)
            {
                Application.RemoveMessageFilter(_messageFilter);
                _messageFilter = null;
            }
        }
        base.Dispose(disposing);
    }
    
    /// <summary>
    /// Modern toast notification with rounded corners and blur effect.
    /// </summary>
    private sealed class ToastForm : Form
    {
        private readonly System.Windows.Forms.Timer _timer;
        private readonly System.Windows.Forms.Timer _fadeTimer;
        private float _opacity = 0f;
        private bool _fadingOut = false;

        // P/Invoke for rounded corners on Windows 11
        [System.Runtime.InteropServices.DllImport("dwmapi.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode, PreserveSig = false)]
        private static extern void DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int pvAttribute, int cbAttribute);
        
        private const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
        private const int DWMWCP_ROUND = 2;

        private const int ToastWidth = 340;
        private const int MinHeight = 96;
        private const int MaxHeight = 380;
        private const int MessageAreaWidth = 260; // ToastWidth - 80
        private const int TopPadding = 46;  // Y position of message (title + gap)
        private const int BottomPadding = 12;

        private static int MeasureMessageHeight(string message, Font font, int maxWidth)
        {
            if (string.IsNullOrEmpty(message)) return 34;
            var flags = TextFormatFlags.WordBreak | TextFormatFlags.TextBoxControl | TextFormatFlags.NoPadding;
            var size = TextRenderer.MeasureText(message, font, new Size(maxWidth, int.MaxValue), flags);
            return Math.Max(34, Math.Min(size.Height, MaxHeight - TopPadding - BottomPadding));
        }

        public ToastForm(Icon icon, string title, string message, int displayDurationMs = 3000)
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

            // Enable double buffering for smooth rendering
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);

            // Apply rounded corners on Windows 11
            try
            {
                var preference = DWMWCP_ROUND;
                DwmSetWindowAttribute(Handle, DWMWA_WINDOW_CORNER_PREFERENCE, ref preference, sizeof(int));
            }
            catch
            {
                // Fallback for older Windows versions - use region
                var path = new GraphicsPath();
                int radius = 12;
                path.AddArc(0, 0, radius * 2, radius * 2, 180, 90);
                path.AddArc(Width - radius * 2, 0, radius * 2, radius * 2, 270, 90);
                path.AddArc(Width - radius * 2, Height - radius * 2, radius * 2, radius * 2, 0, 90);
                path.AddArc(0, Height - radius * 2, radius * 2, radius * 2, 90, 90);
                path.CloseFigure();
                Region = new Region(path);
            }

            // Position in top right of primary screen
            var primaryScreen = Screen.PrimaryScreen;
            if (primaryScreen != null)
            {
                var wa = primaryScreen.WorkingArea;
                Location = new Point(wa.Right - Width - 16, wa.Top + 16);
            }

            // Icon with modern styling
            var pictureBox = new PictureBox
            {
                Image = icon.ToBitmap(),
                SizeMode = PictureBoxSizeMode.Zoom,
                Size = new Size(36, 36),
                Location = new Point(16, 28),
                BackColor = Color.Transparent
            };

            // Title with modern font
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

            // Message: height auto-scaled from measured text (no ellipsis so full text wraps)
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

            // Accent bar on the left - gradient violet
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

            // Fade in animation
            _fadeTimer = new System.Windows.Forms.Timer { Interval = 16 };
            _fadeTimer.Tick += (s, e) =>
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

            // Auto-close after displayDurationMs
            _timer = new System.Windows.Forms.Timer { Interval = Math.Max(1000, displayDurationMs) };
            _timer.Tick += (s, e) =>
            {
                _timer.Stop();
                _fadingOut = true;
                _fadeTimer.Start();
            };
            _timer.Start();

            // Close on click with fade out
            Click += (_, _) => StartFadeOut();
            foreach (Control c in Controls)
            {
                c.Click += (_, _) => StartFadeOut();
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

            // Draw subtle glass border
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
    }
    
    /// <summary>
    /// Custom button that doesn't show focus cues (dotted rectangle).
    /// </summary>
    private sealed class NoFocusCueButton : Button
    {
        protected override bool ShowFocusCues => false;
        
        public NoFocusCueButton()
        {
            SetStyle(ControlStyles.Selectable, false);
        }
    }

    /// <summary>
    /// A small topmost, click-through overlay that follows the mouse cursor
    /// and displays a processing status message (e.g. "Transcription in progress...").
    /// </summary>
    private sealed class CursorOverlay : Form
    {
        private readonly System.Windows.Forms.Timer _followTimer;
        private readonly Label _iconLabel;
        private readonly Label _textLabel;
        private int _dotCount;
        private readonly System.Windows.Forms.Timer _animationTimer;
        private string _baseText = "Processing";

        // Click-through: WS_EX_LAYERED | WS_EX_TRANSPARENT | WS_EX_TOOLWINDOW | WS_EX_TOPMOST | WS_EX_NOACTIVATE
        private const int WS_EX_LAYERED = 0x80000;
        private const int WS_EX_TRANSPARENT = 0x20;
        private const int WS_EX_TOOLWINDOW = 0x80;
        private const int WS_EX_TOPMOST = 0x8;
        private const int WS_EX_NOACTIVATE = 0x08000000;

        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                cp.ExStyle |= WS_EX_LAYERED | WS_EX_TRANSPARENT | WS_EX_TOOLWINDOW | WS_EX_TOPMOST | WS_EX_NOACTIVATE;
                return cp;
            }
        }

        protected override bool ShowWithoutActivation => true;

        public CursorOverlay()
        {
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            TopMost = true;
            ShowInTaskbar = false;
            BackColor = Color.FromArgb(22, 22, 30);
            Size = new Size(220, 36);
            Opacity = 0.94;
            DoubleBuffered = true;

            // Spinner icon label (uses a Unicode character)
            _iconLabel = new Label
            {
                Text = "\u23F3", // Hourglass with flowing sand
                Font = new Font("Segoe UI Emoji", 11f, FontStyle.Regular),
                ForeColor = FluentColors.AccentPrimary,
                AutoSize = false,
                Size = new Size(30, 36),
                Location = new Point(8, 0),
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = Color.Transparent
            };

            // Text label
            _textLabel = new Label
            {
                Text = "Processing...",
                Font = new Font("Segoe UI", 9.5f, FontStyle.Regular),
                ForeColor = Color.FromArgb(240, 240, 244),
                AutoSize = false,
                Size = new Size(175, 36),
                Location = new Point(36, 0),
                TextAlign = ContentAlignment.MiddleLeft,
                BackColor = Color.Transparent
            };

            Controls.Add(_iconLabel);
            Controls.Add(_textLabel);

            // Timer to follow mouse position
            _followTimer = new System.Windows.Forms.Timer { Interval = 16 }; // ~60fps
            _followTimer.Tick += (s, e) =>
            {
                var pos = Control.MousePosition;
                Location = new Point(pos.X + 18, pos.Y + 20);
            };

            // Timer for dot animation
            _animationTimer = new System.Windows.Forms.Timer { Interval = 400 };
            _animationTimer.Tick += (s, e) =>
            {
                _dotCount = (_dotCount + 1) % 4;
                _textLabel.Text = _baseText + new string('.', _dotCount == 0 ? 3 : _dotCount);
            };
        }

        public void ShowOverlay(string text)
        {
            _baseText = text;
            _dotCount = 3;
            _textLabel.Text = text + "...";

            var pos = Control.MousePosition;
            Location = new Point(pos.X + 18, pos.Y + 20);

            Show();
            _followTimer.Start();
            _animationTimer.Start();
        }

        public void UpdateText(string text)
        {
            _baseText = text;
            _dotCount = 3;
            _textLabel.Text = text + "...";
        }

        public void HideOverlay()
        {
            _animationTimer.Stop();
            _followTimer.Stop();
            Hide();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

            // Rounded rectangle background
            using var bgBrush = new SolidBrush(Color.FromArgb(22, 22, 30));
            using var borderPen = new Pen(Color.FromArgb(42, 42, 56), 1);
            var rect = new Rectangle(0, 0, Width - 1, Height - 1);
            using var path = RoundedRect(rect, 8);
            e.Graphics.FillPath(bgBrush, path);
            e.Graphics.DrawPath(borderPen, path);

            // Accent left stripe
            using var accentBrush = new SolidBrush(FluentColors.AccentPrimary);
            e.Graphics.FillRectangle(accentBrush, 0, 6, 3, Height - 12);
        }

        private static System.Drawing.Drawing2D.GraphicsPath RoundedRect(Rectangle bounds, int radius)
        {
            var path = new System.Drawing.Drawing2D.GraphicsPath();
            int d = radius * 2;
            path.AddArc(bounds.X, bounds.Y, d, d, 180, 90);
            path.AddArc(bounds.Right - d, bounds.Y, d, d, 270, 90);
            path.AddArc(bounds.Right - d, bounds.Bottom - d, d, d, 0, 90);
            path.AddArc(bounds.X, bounds.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _followTimer.Dispose();
                _animationTimer.Dispose();
                _iconLabel.Dispose();
                _textLabel.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}

