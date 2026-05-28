using Windows.ApplicationModel;

namespace Koli.WinUI.Services;

/// <summary>
/// Manages MSIX startup task registration (Settings → Apps → Startup apps).
/// Only available when the app is installed as an MSIX package.
/// </summary>
public sealed class StartupTaskService
{
    public const string TaskId = "KoliStartup";

    public bool IsAvailable { get; }

    public StartupTaskService() => IsAvailable = IsPackagedApp();

    public async Task<bool> IsEnabledAsync(CancellationToken cancellationToken = default)
    {
        if (!IsAvailable)
            return false;

        cancellationToken.ThrowIfCancellationRequested();
        var task = await StartupTask.GetAsync(TaskId);
        return task.State == StartupTaskState.Enabled;
    }

    public async Task<StartupTaskChangeResult> SetEnabledAsync(bool enabled, CancellationToken cancellationToken = default)
    {
        if (!IsAvailable)
            return StartupTaskChangeResult.NotAvailable;

        cancellationToken.ThrowIfCancellationRequested();
        var task = await StartupTask.GetAsync(TaskId);

        if (enabled)
        {
            return task.State switch
            {
                StartupTaskState.Enabled => StartupTaskChangeResult.Success,
                StartupTaskState.DisabledByUser => StartupTaskChangeResult.DisabledByUser,
                StartupTaskState.DisabledByPolicy => StartupTaskChangeResult.DisabledByPolicy,
                _ => await EnableAsync(task, cancellationToken)
            };
        }

        if (task.State == StartupTaskState.Enabled)
            task.Disable();

        return StartupTaskChangeResult.Success;
    }

    private static async Task<StartupTaskChangeResult> EnableAsync(StartupTask task, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var state = await task.RequestEnableAsync();
        return state == StartupTaskState.Enabled
            ? StartupTaskChangeResult.Success
            : StartupTaskChangeResult.Denied;
    }

    private static bool IsPackagedApp()
    {
        try
        {
            return Package.Current != null;
        }
        catch
        {
            return false;
        }
    }
}

public enum StartupTaskChangeResult
{
    Success,
    NotAvailable,
    Denied,
    DisabledByUser,
    DisabledByPolicy
}
