namespace Koli.Platform;

public sealed class ToastNotificationService
{
    private readonly IToastPresenter _presenter;

    public ToastNotificationService(IToastPresenter presenter) => _presenter = presenter;

    public void ShowInfo(string title, string message) => _presenter.Show(title, message);

    public void ShowWarning(string title, string message) => _presenter.Show(title, message);

    public void ShowError(string title, string message) => _presenter.Show(title, message);
}
