using System.Reflection;
using System.Text;
using Koli.Config;
using Microsoft.UI.Dispatching;

namespace Koli.WinUI.Services;

public sealed class DebugLogService
{
    private readonly object _lock = new();
    private readonly StringBuilder _buffer = new();
    private readonly DispatcherQueue? _dispatcher;

    public event EventHandler? LogChanged;

    public DebugLogService(WindowContext windowContext)
    {
        _dispatcher = windowContext.DispatcherQueue;
    }

    public string FullText
    {
        get
        {
            lock (_lock)
                return _buffer.ToString();
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
                var value = header.Key.Contains("api-key", StringComparison.OrdinalIgnoreCase)
                            || header.Key.Contains("authorization", StringComparison.OrdinalIgnoreCase)
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
        Append(sb.ToString());
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
                sb.AppendLine($"  {header.Key}: {header.Value}");
            sb.AppendLine();
        }

        if (!string.IsNullOrEmpty(body))
        {
            sb.AppendLine("Body:");
            var displayBody = body.Length > 2000
                ? body[..2000] + $"\n... (truncated, {body.Length} characters total)"
                : body;
            sb.AppendLine(displayBody);
            sb.AppendLine();
        }

        sb.AppendLine(new string('─', 80));
        sb.AppendLine();
        Append(sb.ToString());
    }

    public void LogError(string message, Exception? exception = null)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] ERROR");
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
        Append(sb.ToString());
    }

    public void LogInfo(string message) =>
        Append($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] INFO: {message}\n");

    public void Clear()
    {
        lock (_lock)
            _buffer.Clear();
        RaiseLogChanged();
    }

    private void Append(string text)
    {
        lock (_lock)
            _buffer.Append(text);
        RaiseLogChanged();
    }

    private void RaiseLogChanged()
    {
        if (_dispatcher != null && !_dispatcher.HasThreadAccess)
        {
            _dispatcher.TryEnqueue(() => LogChanged?.Invoke(this, EventArgs.Empty));
            return;
        }

        LogChanged?.Invoke(this, EventArgs.Empty);
    }
}
