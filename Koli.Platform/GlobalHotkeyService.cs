using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Koli.Platform;

public enum HotkeyAction
{
    ToggleRecording,
    CancelRecording,
    TogglePause
}

public sealed class GlobalHotkeyService : IDisposable
{
    private const int HotkeyToggleRecording = 9000;
    private const int HotkeyCancelRecording = 9001;
    private const int HotkeyTogglePause = 9002;
    private const uint ModNone = 0x0000;
    private const uint VkF9 = 0x78;
    private const uint VkF7 = 0x76;
    private const uint VkF6 = 0x75;

    private IntPtr _hwnd;
    private nint _subclassId;
    private WindowMessageSubclass? _subclass;
    private bool _registered;

    public event EventHandler<HotkeyAction>? HotkeyPressed;

    public void Register(IntPtr hwnd)
    {
        if (_registered && _hwnd == hwnd)
            return;

        Unregister();
        _hwnd = hwnd;

        if (!RegisterHotKey(_hwnd, HotkeyToggleRecording, ModNone, VkF9)
            || !RegisterHotKey(_hwnd, HotkeyCancelRecording, ModNone, VkF7)
            || !RegisterHotKey(_hwnd, HotkeyTogglePause, ModNone, VkF6))
        {
            throw new InvalidOperationException("Failed to register global hotkeys (F6/F7/F9).");
        }

        _subclass = new WindowMessageSubclass(hwnd, OnWindowMessage);
        _subclassId = _subclass.Attach();
        _registered = true;
    }

    public void Unregister()
    {
        if (!_registered)
            return;

        _subclass?.Detach();
        _subclass = null;

        if (_hwnd != IntPtr.Zero)
        {
            UnregisterHotKey(_hwnd, HotkeyToggleRecording);
            UnregisterHotKey(_hwnd, HotkeyCancelRecording);
            UnregisterHotKey(_hwnd, HotkeyTogglePause);
        }

        _registered = false;
        _hwnd = IntPtr.Zero;
    }

    private void OnWindowMessage(Message m)
    {
        if (m.Msg != WM_HOTKEY)
            return;

        var action = m.WParam.ToInt32() switch
        {
            HotkeyToggleRecording => HotkeyAction.ToggleRecording,
            HotkeyCancelRecording => HotkeyAction.CancelRecording,
            HotkeyTogglePause => HotkeyAction.TogglePause,
            _ => (HotkeyAction?)null
        };

        if (action.HasValue)
            HotkeyPressed?.Invoke(this, action.Value);
    }

    public void Dispose() => Unregister();

    private const int WM_HOTKEY = 0x0312;

    [DllImport("user32.dll")]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll")]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    private sealed class WindowMessageSubclass : NativeWindow
    {
        private readonly Action<Message> _handler;

        public WindowMessageSubclass(IntPtr hwnd, Action<Message> handler)
        {
            _handler = handler;
            AssignHandle(hwnd);
        }

        public nint Attach() => Handle;

        public void Detach() => ReleaseHandle();

        protected override void WndProc(ref Message m)
        {
            _handler(m);
            base.WndProc(ref m);
        }
    }
}
