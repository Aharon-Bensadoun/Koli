using System.Runtime.InteropServices;
using System.Windows.Forms;
using Koli.Config;

namespace Koli.Platform;

public enum HotkeyAction
{
    ToggleRecording,
    CancelRecording,
    TogglePause,
    ToggleAssistantRecording
}

public sealed class GlobalHotkeyService : IDisposable
{
    private const int HotkeyToggleRecording = 9000;
    private const int HotkeyCancelRecording = 9001;
    private const int HotkeyTogglePause = 9002;
    private const int CustomHotkeyStart = 9100;
    private const uint ModNone = 0x0000;
    private const uint ModAlt = 0x0001;
    private const uint ModControl = 0x0002;
    private const uint ModShift = 0x0004;
    private const uint ModWin = 0x0008;
    private const uint ModNoRepeat = 0x4000;
    private const uint VkF9 = 0x78;
    private const uint VkF7 = 0x76;
    private const uint VkF6 = 0x75;

    private const int WhKeyboardLl = 13;
    private const int WmKeydown = 0x0100;
    private const int WmKeyup = 0x0101;
    private const int WmSyskeydown = 0x0104;
    private const int WmSyskeyup = 0x0105;

    private IntPtr _hwnd;
    private WindowMessageSubclass? _subclass;
    private bool _registered;

    private IntPtr _keyboardHook;
    private HookProc? _keyboardHookProc;
    private readonly AltGrToggleTracker _altGrTracker = new();
    private readonly Dictionary<int, Guid> _customHotkeys = new();

    public bool RegistrationFailed { get; private set; }
    public bool AssistantHotkeyRegistrationFailed { get; private set; }

    public event EventHandler<HotkeyAction>? HotkeyPressed;
    public event EventHandler<Guid>? CustomHotkeyPressed;

    public IReadOnlyDictionary<Guid, string> CustomHotkeyErrors { get; private set; } = new Dictionary<Guid, string>();

    public void Register(IntPtr hwnd)
    {
        if (_registered && _hwnd == hwnd)
            return;

        Unregister();
        _hwnd = hwnd;

        var dictationRegistered = RegisterHotKey(_hwnd, HotkeyToggleRecording, ModNone, VkF9)
            & RegisterHotKey(_hwnd, HotkeyCancelRecording, ModNone, VkF7)
            & RegisterHotKey(_hwnd, HotkeyTogglePause, ModNone, VkF6);
        var assistantRegistered = InstallAltGrHook();

        RegistrationFailed = !dictationRegistered;
        AssistantHotkeyRegistrationFailed = !assistantRegistered;

        _subclass = new WindowMessageSubclass(hwnd, OnWindowMessage);
        _ = _subclass.Attach();
        _registered = true;
    }

    public void RegisterCustomHotkeys(IEnumerable<CustomActionProfile> profiles)
    {
        UnregisterCustomHotkeys();
        var errors = new Dictionary<Guid, string>();
        if (!_registered || _hwnd == IntPtr.Zero)
        {
            CustomHotkeyErrors = errors;
            return;
        }

        var id = CustomHotkeyStart;
        foreach (var profile in profiles.Where(profile => profile.Enabled))
        {
            if (!TryResolveHotkey(profile.Hotkey, out var modifiers, out var virtualKey, out var error))
            {
                errors[profile.Id] = error;
                continue;
            }

            if (!RegisterHotKey(_hwnd, id, modifiers | ModNoRepeat, virtualKey))
            {
                errors[profile.Id] = "This shortcut is already used by Windows or another application.";
                continue;
            }

            _customHotkeys[id] = profile.Id;
            id++;
        }
        CustomHotkeyErrors = errors;
    }

    public void Unregister()
    {
        if (!_registered)
            return;

        _subclass?.Detach();
        _subclass = null;

        UninstallAltGrHook();
        UnregisterCustomHotkeys();

        if (_hwnd != IntPtr.Zero)
        {
            UnregisterHotKey(_hwnd, HotkeyToggleRecording);
            UnregisterHotKey(_hwnd, HotkeyCancelRecording);
            UnregisterHotKey(_hwnd, HotkeyTogglePause);
        }

        _registered = false;
        _hwnd = IntPtr.Zero;
    }

    private bool InstallAltGrHook()
    {
        UninstallAltGrHook();

        _keyboardHookProc = OnKeyboardHook;
        _keyboardHook = SetWindowsHookEx(WhKeyboardLl, _keyboardHookProc, GetModuleHandle(null), 0);
        return _keyboardHook != IntPtr.Zero;
    }

    private void UninstallAltGrHook()
    {
        if (_keyboardHook == IntPtr.Zero)
            return;

        UnhookWindowsHookEx(_keyboardHook);
        _keyboardHook = IntPtr.Zero;
        _keyboardHookProc = null;
    }

    private IntPtr OnKeyboardHook(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            var kb = Marshal.PtrToStructure<KbdLlHookStruct>(lParam);
            var message = wParam.ToInt32();
            var isKeyDown = message is WmKeydown or WmSyskeydown;
            var isKeyUp = message is WmKeyup or WmSyskeyup;

            if (isKeyDown || isKeyUp)
            {
                var toggle = _altGrTracker.ProcessKey(kb.VkCode, isKeyDown);
                if (isKeyUp && toggle == true)
                    HotkeyPressed?.Invoke(this, HotkeyAction.ToggleAssistantRecording);
            }
        }

        return CallNextHookEx(_keyboardHook, nCode, wParam, lParam);
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
        else if (_customHotkeys.TryGetValue(m.WParam.ToInt32(), out var profileId))
            CustomHotkeyPressed?.Invoke(this, profileId);
    }

    private void UnregisterCustomHotkeys()
    {
        if (_hwnd != IntPtr.Zero)
        {
            foreach (var id in _customHotkeys.Keys)
                UnregisterHotKey(_hwnd, id);
        }
        _customHotkeys.Clear();
    }

    public static bool TryResolveHotkey(CustomHotkey hotkey, out uint modifiers, out uint virtualKey, out string error)
    {
        modifiers = ModNone;
        virtualKey = 0;
        error = "";
        if (!(hotkey.Ctrl || hotkey.Alt || hotkey.Shift || hotkey.Win))
        {
            error = "Select at least one modifier key.";
            return false;
        }

        if (hotkey.Ctrl) modifiers |= ModControl;
        if (hotkey.Alt) modifiers |= ModAlt;
        if (hotkey.Shift) modifiers |= ModShift;
        if (hotkey.Win) modifiers |= ModWin;

        var key = hotkey.Key.Trim();
        if (!Enum.TryParse<Keys>(key, true, out var parsed) || parsed is Keys.ControlKey or Keys.Menu or Keys.ShiftKey or Keys.LWin or Keys.RWin)
        {
            error = "Enter a valid non-modifier key, such as M, 8, Space or F10.";
            return false;
        }
        virtualKey = (uint)parsed;
        return true;
    }

    public void Dispose() => Unregister();

    private const int WM_HOTKEY = 0x0312;

    private delegate IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    private struct KbdLlHookStruct
    {
        public uint VkCode;
        public uint ScanCode;
        public uint Flags;
        public uint Time;
        public IntPtr ExtraInfo;
    }

    [DllImport("user32.dll")]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll")]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, HookProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr GetModuleHandle(string? lpModuleName);

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
