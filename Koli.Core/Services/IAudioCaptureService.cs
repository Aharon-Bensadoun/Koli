namespace Koli.Services;

public interface IAudioCaptureService : IAsyncDisposable
{
    IAsyncEnumerable<byte[]> GetAudioStreamAsync(CancellationToken cancellationToken);
    Task StartAsync(CancellationToken cancellationToken);
    byte[]? GetCollectedAudio();
    void ClearBuffer();
    Task PauseAsync();
    Task ResumeAsync();
    Task StopAsync();
    event EventHandler<float>? AudioLevelChanged;
    bool IsPaused { get; }
}
