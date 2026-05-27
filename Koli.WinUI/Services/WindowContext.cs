using Microsoft.UI.Dispatching;

namespace Koli.WinUI.Services;

public sealed class WindowContext
{
    public Func<IntPtr> GetWindowHandle { get; set; } = () => IntPtr.Zero;
    public DispatcherQueue DispatcherQueue { get; set; } = DispatcherQueue.GetForCurrentThread();
}
