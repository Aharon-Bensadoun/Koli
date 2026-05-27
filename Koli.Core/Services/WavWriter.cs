using System.Text;

namespace Koli.Services;

/// <summary>
/// Helper for writing a standard RIFF/WAVE header for mono 16-bit PCM data.
/// Shared between the transcription service (chunk-to-WAV) and the pending
/// audio store (failed recordings persisted to disk for replay / retry).
/// </summary>
public static class WavWriter
{
    public const int DefaultSampleRate = 16_000;
    public const short DefaultChannels = 1;
    public const short DefaultBitsPerSample = 16;

    /// <summary>
    /// Writes a 44-byte WAV header for a mono 16-bit PCM payload of <paramref name="dataLength"/> bytes
    /// into <paramref name="stream"/>. The caller is responsible for writing the PCM bytes immediately
    /// after the header.
    /// </summary>
    public static void WriteHeader(
        Stream stream,
        int dataLength,
        int sampleRate = DefaultSampleRate,
        short channels = DefaultChannels,
        short bitsPerSample = DefaultBitsPerSample)
    {
        int byteRate = sampleRate * channels * bitsPerSample / 8;
        short blockAlign = (short)(channels * bitsPerSample / 8);

        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        writer.Write(Encoding.ASCII.GetBytes("RIFF"));
        writer.Write(dataLength + 36); // File size - 8
        writer.Write(Encoding.ASCII.GetBytes("WAVE"));
        writer.Write(Encoding.ASCII.GetBytes("fmt "));
        writer.Write(16); // Subchunk1Size (16 for PCM)
        writer.Write((short)1); // AudioFormat (1 for PCM)
        writer.Write(channels);
        writer.Write(sampleRate);
        writer.Write(byteRate);
        writer.Write(blockAlign);
        writer.Write(bitsPerSample);
        writer.Write(Encoding.ASCII.GetBytes("data"));
        writer.Write(dataLength);
    }

    /// <summary>
    /// Convenience helper: writes a complete WAV file (header + PCM payload) to <paramref name="path"/>,
    /// creating the parent directory if it does not exist.
    /// </summary>
    public static void WriteFile(
        string path,
        byte[] pcmData,
        int sampleRate = DefaultSampleRate,
        short channels = DefaultChannels,
        short bitsPerSample = DefaultBitsPerSample)
    {
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        using var fs = File.Create(path);
        WriteHeader(fs, pcmData.Length, sampleRate, channels, bitsPerSample);
        fs.Write(pcmData, 0, pcmData.Length);
    }
}
