using System.Threading.Channels;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using Koli.Config;

namespace Koli.Services;

public sealed class AudioCaptureService : IAudioCaptureService
{
    private readonly AudioSettings _settings;
    private readonly Channel<byte[]> _channel;
    private WasapiCapture? _capture;
    private MemoryStream? _audioBuffer;
    private MMDevice? _device;
    private bool _isPaused;
    
    public event EventHandler<float>? AudioLevelChanged;
    
    public bool IsPaused => _isPaused;

    public AudioCaptureService(AudioSettings settings)
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
        {
            return Task.CompletedTask;
        }

        try
        {
            // Clear any existing buffer and initialize new audio buffer for collecting all audio
            ClearBuffer();
            _audioBuffer = new MemoryStream();
            
            _device = ResolveDevice();
            _capture = new WasapiCapture(_device, false, 100);
            _capture.WaveFormat = new WaveFormat(_settings.SampleRate, 16, 1);
            _capture.DataAvailable += OnDataAvailable;
            _capture.RecordingStopped += (_, _) => _channel.Writer.TryComplete();
            _capture.StartRecording();
            _isPaused = false;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to start audio capture: {ex.Message}", ex);
        }
        
        return Task.CompletedTask;
    }

    private MMDevice ResolveDevice()
    {
        var enumerator = new MMDeviceEnumerator();
        if (_settings.Device.Equals("default", StringComparison.OrdinalIgnoreCase))
        {
            return enumerator.GetDefaultAudioEndpoint(DataFlow.Capture, Role.Multimedia);
        }

        var device = enumerator.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active)
            .FirstOrDefault(d => d.FriendlyName.Contains(_settings.Device, StringComparison.OrdinalIgnoreCase));

        return device ?? enumerator.GetDefaultAudioEndpoint(DataFlow.Capture, Role.Multimedia);
    }

    private void OnDataAvailable(object? sender, WaveInEventArgs e)
    {
        var buffer = new byte[e.BytesRecorded];
        Array.Copy(e.Buffer, buffer, e.BytesRecorded);
        
        // Calculate audio level (RMS)
        var level = CalculateAudioLevel(buffer);
        AudioLevelChanged?.Invoke(this, level);
        
        // Store audio in buffer
        if (_audioBuffer != null)
        {
            _audioBuffer.Write(buffer, 0, buffer.Length);
        }
        
        _channel.Writer.TryWrite(buffer);
    }
    
    private float CalculateAudioLevel(byte[] buffer)
    {
        if (buffer.Length == 0)
            return 0f;
        
        // Convert bytes to 16-bit samples and calculate both RMS and peak
        long sumOfSquares = 0;
        int sampleCount = buffer.Length / 2; // 16-bit = 2 bytes per sample
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
        
        // Calculate RMS
        double rms = Math.Sqrt(sumOfSquares / (double)sampleCount);
        
        // Use a combination of RMS and peak for more dynamic response
        var rmsLevel = rms / 32768.0;
        var peakLevel = maxPeak / 32768.0;
        
        // Combine RMS (smooth) with peak (responsive) - weighted average
        var combinedLevel = (rmsLevel * 0.7 + peakLevel * 0.3);
        
        // Apply logarithmic scaling for better sensitivity to low levels
        // This makes small changes more visible
        var logLevel = combinedLevel > 0 ? Math.Log10(combinedLevel * 9 + 1) : 0;
        
        // Amplify the signal for better visibility (multiply by 2-3x)
        var amplifiedLevel = (float)Math.Min(1.0, logLevel * 2.5);
        
        return amplifiedLevel;
    }
    
    public byte[]? GetCollectedAudio()
    {
        if (_audioBuffer == null || _audioBuffer.Length == 0)
        {
            return null;
        }
        
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
        {
            return Task.CompletedTask;
        }

        try
        {
            _capture.DataAvailable -= OnDataAvailable;
            _capture.StopRecording();
            _isPaused = true;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to pause audio capture: {ex.Message}", ex);
        }
        
        return Task.CompletedTask;
    }

    public Task ResumeAsync()
    {
        if (_capture is null || !_isPaused)
        {
            return Task.CompletedTask;
        }

        try
        {
            // Ensure buffer exists (should always be the case if we're resuming)
            if (_audioBuffer == null)
            {
                _audioBuffer = new MemoryStream();
            }

            // Reattach event handler and restart recording
            _capture.DataAvailable += OnDataAvailable;
            _capture.StartRecording();
            _isPaused = false;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to resume audio capture: {ex.Message}", ex);
        }
        
        return Task.CompletedTask;
    }

    public Task StopAsync()
    {
        if (_capture is null)
        {
            return Task.CompletedTask;
        }

        _capture.DataAvailable -= OnDataAvailable;
        
        // Only stop recording if not already paused (paused means already stopped)
        if (!_isPaused)
        {
            _capture.StopRecording();
        }
        
        _capture.Dispose();
        _capture = null;
        _device = null;
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
