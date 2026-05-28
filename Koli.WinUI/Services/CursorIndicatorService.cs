using Koli.WinUI.Overlays;
using Microsoft.UI.Dispatching;

namespace Koli.WinUI.Services;

public sealed class CursorIndicatorService
{
    private readonly WindowContext _context;
    private CursorIndicatorWindow? _window;

    public CursorIndicatorService(WindowContext context) => _context = context;

    public void Show(CursorIndicatorState state)
    {
        _context.DispatcherQueue.TryEnqueue(() =>
        {
            _window ??= new CursorIndicatorWindow();
            _window.ShowState(state);
        });
    }

    public void Hide()
    {
        _context.DispatcherQueue.TryEnqueue(() => _window?.HideIndicator());
    }
}
