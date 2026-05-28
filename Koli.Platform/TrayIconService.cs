using System.Drawing;
using System.Windows.Forms;

namespace Koli.Platform;

public sealed class TrayIconService : IDisposable
{
    private NotifyIcon? _notifyIcon;
    private Icon? _icon;

    public event EventHandler? ShowRequested;
    public event EventHandler? ExitRequested;

    public void Initialize(string tooltip, Stream? iconStream)
    {
        DisposeIcon();

        if (iconStream != null)
            _icon = new Icon(iconStream);

        _notifyIcon = new NotifyIcon
        {
            Text = tooltip,
            Icon = _icon ?? SystemIcons.Application,
            Visible = true
        };

        var menu = new ContextMenuStrip();
        var showItem = new ToolStripMenuItem("Show");
        showItem.Click += (_, _) => ShowRequested?.Invoke(this, EventArgs.Empty);
        menu.Items.Add(showItem);

        var exitItem = new ToolStripMenuItem("Exit");
        exitItem.Click += (_, _) => ExitRequested?.Invoke(this, EventArgs.Empty);
        menu.Items.Add(exitItem);

        _notifyIcon.ContextMenuStrip = menu;
        _notifyIcon.DoubleClick += (_, _) => ShowRequested?.Invoke(this, EventArgs.Empty);
    }

    public Icon ApplicationIcon => _icon ?? SystemIcons.Application;

    public void SetTooltip(string tooltip)
    {
        if (_notifyIcon == null)
            return;

        _notifyIcon.Text = tooltip.Length <= 63 ? tooltip : tooltip[..60] + "…";
    }

    public void ShowBalloon(string title, string message, ToolTipIcon icon = ToolTipIcon.Info, int timeoutMs = 3000)
    {
        _notifyIcon?.ShowBalloonTip(Math.Min(timeoutMs, 10000), title, message, icon);
    }

    public void Dispose()
    {
        if (_notifyIcon != null)
        {
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
            _notifyIcon = null;
        }

        DisposeIcon();
    }

    private void DisposeIcon()
    {
        _icon?.Dispose();
        _icon = null;
    }
}
