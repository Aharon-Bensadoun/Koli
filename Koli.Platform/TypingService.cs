using System.Runtime.InteropServices;
using System.Windows.Forms;
using Koli.Config;

namespace Koli.Platform;

public sealed class TypingService
{
    private readonly Dictionary<string, int> _realtimeTypedLengthByItem = new(StringComparer.Ordinal);
    private bool _realtimeTypedAnything;
    private IntPtr _targetWindow;

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool IsWindow(IntPtr hWnd);

    public void CaptureTargetWindow()
    {
        _targetWindow = GetForegroundWindow();
    }

    public void ResetRealtimeSession()
    {
        _realtimeTypedLengthByItem.Clear();
        _realtimeTypedAnything = false;
    }

    public bool RealtimeTypedAnything => _realtimeTypedAnything;

    public void TypeRealtimeChunk(string itemId, string chunk, IntPtr excludeWindow)
    {
        if (string.IsNullOrEmpty(chunk))
            return;

        try
        {
            if (!_realtimeTypedAnything)
                RestoreTargetFocus(excludeWindow);

            try
            {
                Clipboard.SetText(chunk);
                SendKeys.SendWait("^v");
            }
            catch
            {
                SendKeysEscaped(chunk);
            }

            _realtimeTypedAnything = true;
            var current = _realtimeTypedLengthByItem.TryGetValue(itemId, out var n) ? n : 0;
            _realtimeTypedLengthByItem[itemId] = current + chunk.Length;
        }
        catch
        {
            // Best-effort live typing
        }
    }

    public void OnRealtimeItemCompleted(string itemId, string finalText, TypingSettings settings, IntPtr excludeWindow)
    {
        if (!settings.TypeInActiveWindow || finalText.Length == 0)
            return;

        var deltasAlreadyTyped = _realtimeTypedLengthByItem.TryGetValue(itemId, out var n) && n > 0;
        if (!deltasAlreadyTyped)
            TypeRealtimeChunk(itemId, finalText, excludeWindow);

        _realtimeTypedLengthByItem.Remove(itemId);
    }

    public void TypeText(string text, TypingSettings settings, IntPtr excludeWindow, bool addLeadingSpace = false)
    {
        if (string.IsNullOrEmpty(text))
            return;

        RestoreTargetFocus(excludeWindow);

        if (settings.AutoSpace && addLeadingSpace)
            text = " " + text;

        if (settings.StreamingMode)
        {
            foreach (var c in text)
            {
                if (c == '\r')
                    continue;
                if (c == '\n')
                {
                    SendKeys.SendWait("{ENTER}");
                }
                else
                {
                    var charToSend = c switch
                    {
                        '{' or '}' or '(' or ')' or '+' or '^' or '%' or '~' or '[' or ']' => "{" + c + "}",
                        _ => c.ToString()
                    };
                    SendKeys.SendWait(charToSend);
                }

                Thread.Sleep(Math.Max(settings.ChunkDelayMs, 1));
            }
        }
        else
        {
            try
            {
                string? originalClipboard = null;
                try
                {
                    if (Clipboard.ContainsText())
                        originalClipboard = Clipboard.GetText();
                }
                catch
                {
                    // ignore
                }

                Clipboard.SetText(text);
                SendKeys.SendWait("^v");

                if (originalClipboard != null)
                {
                    Task.Run(async () =>
                    {
                        await Task.Delay(100);
                        try { Clipboard.SetText(originalClipboard); }
                        catch { /* ignore */ }
                    });
                }
            }
            catch
            {
                var textToSend = text.Replace("\r\n", "{ENTER}").Replace("\n", "{ENTER}");
                SendKeys.SendWait(textToSend);
            }
        }
    }

    private void RestoreTargetFocus(IntPtr excludeWindow)
    {
        if (_targetWindow == IntPtr.Zero || !IsWindow(_targetWindow) || _targetWindow == excludeWindow)
            return;

        SetForegroundWindow(_targetWindow);
        Application.DoEvents();
        Thread.Sleep(80);
    }

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
}
