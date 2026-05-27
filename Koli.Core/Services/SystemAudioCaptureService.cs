using System.Threading.Channels;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using Koli.Config;

namespace Koli.Services;

/// <summary>
/// Captures system audio output (loopback) for virtual meeting transcription.
/// Uses WASAPI loopback to record what comes out of the speakers/headphones.
/// </summary>
public sealed class SystemAudioCaptureService : IAudioCaptureService
{
    private readonly AudioSettings _settings;
    private readonly Channel<byte[]> _channel;
    private WasapiLoopbackCapture? _capture;
    private MemoryStream? _audioBuffer;
    private WaveFormat? _captureFormat;
    private bool _isPaused;

    public event EventHandler<float>? AudioLevelChanged;

    public bool IsPaused => _isPaused;

    public SystemAudioCaptureService(AudioSettings settings)
    {
        _settings = settings;
        _channel = Channel.CreateUnbounded<byte[]>(new UnboundedChannelOptions
        {
            SingleReader = false,
            SingleWriter = true
        });
    }

    public IAsyncEnumerable<byte[]> GetAudioStreamAsync(CancellationToken cancellationToken)
    {
        return _channel.Reader.ReadAllAsync(cancellationToken);
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (_capture is not null)
            return Task.CompletedTask;

        try
        {
            ClearBuffer();
            _audioBuffer = new MemoryStream();

            _capture = new WasapiLoopbackCapture();
            _captureFormat = _capture.WaveFormat;
            _capture.DataAvailable += OnDataAvailable;
            _capture.RecordingStopped += (_, _) => _channel.Writer.TryComplete();
            _capture.StartRecording();
            _isPaused = false;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to start system audio capture: {ex.Message}", ex);
        }

        return Task.CompletedTask;
    }

    private void OnDataAvailable(object? sender, WaveInEventArgs e)
    {
        if (e.BytesRecorded == 0)
            return;

        // Resample from system format (typically 48kHz/32-bit/float/stereo) to 16kHz/16-bit/mono
        var resampled = ResampleToTarget(e.Buffer, e.BytesRecorded);
        if (resampled.Length == 0)
            return;

        var level = CalculateAudioLevel(resampled);
        AudioLevelChanged?.Invoke(this, level);

        _audioBuffer?.Write(resampled, 0, resampled.Length);
        _channel.Writer.TryWrite(resampled);
    }

    private byte[] ResampleToTarget(byte[] buffer, int bytesRecorded)
    {
        if (_captureFormat == null)
            return Array.Empty<byte>();

        var targetFormat = new WaveFormat(_settings.SampleRate, 16, 1);

        using var inputStream = new RawSourceWaveStream(buffer, 0, bytesRecorded, _captureFormat);
        using var resampler = new MediaFoundationResampler(inputStream, targetFormat);
        resampler.ResamplerQuality = 60;

        using var outputStream = new MemoryStream();
        var readBuffer = new byte[4096];
        int read;
        while ((read = resampler.Read(readBuffer, 0, readBuffer.Length)) > 0)
        {
            outputStream.Write(readBuffer, 0, read);
        }

        return outputStream.ToArray();
    }

    private static float CalculateAudioLevel(byte[] buffer)
    {
        if (buffer.Length < 2)
            return 0f;

        long sumOfSquares = 0;
        int sampleCount = buffer.Length / 2;
        short maxPeak = 0;

        for (int i = 0; i < buffer.Length - 1; i += 2)
        {
            short sample = (short)(buffer[i] | (buffer[i + 1] << 8));
            var absSample = Math.Abs(sample);
            sumOfSquares += (long)sample * sample;
            if (absSample > maxPeak)
                maxPeak = absSample;
        }

        if (sampleCount == 0)
            return 0f;

        double rms = Math.Sqrt(sumOfSquares / (double)sampleCount);
        var rmsLevel = rms / 32768.0;
        var peakLevel = maxPeak / 32768.0;
        var combinedLevel = (rmsLevel * 0.7 + peakLevel * 0.3);
        var logLevel = combinedLevel > 0 ? Math.Log10(combinedLevel * 9 + 1) : 0;
        return (float)Math.Min(1.0, logLevel * 2.5);
    }

    public byte[]? GetCollectedAudio()
    {
        if (_audioBuffer == null || _audioBuffer.Length == 0)
            return null;

        return _audioBuffer.ToArray();
    }

    public void ClearBuffer()
    {
        _audioBuffer?.Dispose();
        _audioBuffer = null;
    }

    public Task PauseAsync()
    {
        if (_capture is null || _isPaused)
            return Task.CompletedTask;

        try
        {
            _capture.DataAvailable -= OnDataAvailable;
            _capture.StopRecording();
            _isPaused = true;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to pause system audio capture: {ex.Message}", ex);
        }

        return Task.CompletedTask;
    }

    public Task ResumeAsync()
    {
        if (_capture is null || !_isPaused)
            return Task.CompletedTask;

        try
        {
            _audioBuffer ??= new MemoryStream();
            _capture.DataAvailable += OnDataAvailable;
            _capture.StartRecording();
            _isPaused = false;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to resume system audio capture: {ex.Message}", ex);
        }

        return Task.CompletedTask;
    }

    public Task StopAsync()
    {
        if (_capture is null)
            return Task.CompletedTask;

        _capture.DataAvailable -= OnDataAvailable;

        if (!_isPaused)
            _capture.StopRecording();

        _capture.Dispose();
        _capture = null;
        _isPaused = false;
        _channel.Writer.TryComplete();
        return Task.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync().ConfigureAwait(false);
        _audioBuffer?.Dispose();
        _audioBuffer = null;
    }
}
