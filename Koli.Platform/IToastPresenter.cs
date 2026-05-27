namespace Koli.Platform;

public interface IToastPresenter
{
    void Show(string title, string message, int displayDurationMs = 3000);
}
