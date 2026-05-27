using Koli.Platform;
using Koli.WinUI.Overlays;
using Microsoft.UI.Dispatching;

namespace Koli.WinUI.Services;

public sealed class WinUiToastPresenter : IToastPresenter
{
    private readonly WindowContext _context;
    private ToastOverlayWindow? _window;

    public WinUiToastPresenter(WindowContext context) => _context = context;

    public void Show(string title, string message, int displayDurationMs = 3000)
    {
        _context.DispatcherQueue.TryEnqueue(() =>
        {
            _window ??= new ToastOverlayWindow();
            _window.ShowToast(title, message, displayDurationMs);
        });
    }
}
