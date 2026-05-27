using NAudio.Wave;

namespace Koli.Services;

/// <summary>
/// Lightweight single-instance WAV player used by the history view to let users
/// listen back to recordings whose transcription failed. Only one file can play
/// at a time; calling <see cref="Play"/> while another track is active stops the
/// previous one before starting the new one.
/// </summary>
public sealed class AudioPlaybackService : IDisposable
{
    private readonly object _gate = new();
    private WaveOutEvent? _output;
    private WaveFileReader? _reader;
    private string? _currentPath;

    /// <summary>
    /// Raised when playback finishes either naturally or via <see cref="Stop"/>.
    /// Always marshalled to the thread that owns the underlying NAudio device;
    /// UI callers should marshal back to the UI thread if needed.
    /// </summary>
    public event EventHandler? PlaybackEnded;

    public bool IsPlaying
    {
        get
        {
            lock (_gate)
            {
                return _output?.PlaybackState == PlaybackState.Playing;
            }
        }
    }

    public string? CurrentPath
    {
        get
        {
            lock (_gate)
            {
                return _currentPath;
            }
        }
    }

    public void Play(string wavPath)
    {
        if (string.IsNullOrWhiteSpace(wavPath) || !File.Exists(wavPath))
            throw new FileNotFoundException("WAV file not found", wavPath);

        lock (_gate)
        {
            StopInternal();

            _reader = new WaveFileReader(wavPath);
            _output = new WaveOutEvent();
            _output.PlaybackStopped += OnPlaybackStopped;
            _output.Init(_reader);
            _output.Play();
            _currentPath = wavPath;
        }
    }

    public void Stop()
    {
        lock (_gate)
        {
            StopInternal();
        }

        // Fire the event outside the lock so subscribers can safely call back into us.
        PlaybackEnded?.Invoke(this, EventArgs.Empty);
    }

    private void OnPlaybackStopped(object? sender, StoppedEventArgs e)
    {
        bool shouldRaise;
        lock (_gate)
        {
            shouldRaise = _output != null;
            StopInternal();
        }

        if (shouldRaise)
            PlaybackEnded?.Invoke(this, EventArgs.Empty);
    }

    private void StopInternal()
    {
        // Caller holds _gate.
        if (_output != null)
        {
            try
            {
                _output.PlaybackStopped -= OnPlaybackStopped;
                _output.Stop();
            }
            catch
            {
                // Ignore: device might already be stopped/disposed.
            }

            try { _output.Dispose(); } catch { /* ignore */ }
            _output = null;
        }

        if (_reader != null)
        {
            try { _reader.Dispose(); } catch { /* ignore */ }
            _reader = null;
        }

        _currentPath = null;
    }

    public void Dispose()
    {
        lock (_gate)
        {
            StopInternal();
        }
    }
}
