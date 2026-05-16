using System.Text.Json;

namespace Koli.Services;

/// <summary>
/// Metadata describing a recording that was captured but whose transcription
/// failed (network error, invalid API key, API 4xx/5xx response, empty result…).
/// The actual audio bytes live as a standalone WAV file at <see cref="FilePath"/>.
/// </summary>
public sealed class PendingAudioEntry
{
    public Guid Id { get; init; }
    public string FilePath { get; init; } = "";
    public DateTime CapturedAt { get; init; }
    public TimeSpan Duration { get; init; }
    public string Language { get; init; } = "";
    public int SampleRate { get; init; } = WavWriter.DefaultSampleRate;
    public string? LastError { get; set; }
}

/// <summary>
/// Persists failed-transcription recordings as WAV files on disk together with a JSON
/// index, so they survive application restarts and can be replayed / retranscribed
/// from the history view.
/// </summary>
public sealed class PendingAudioStore
{
    private const int MaxEntries = 50;

    private readonly string _audioFolder;
    private readonly string _indexPath;
    private readonly object _gate = new();
    private List<PendingAudioEntry> _entries = new();

    public PendingAudioStore(string audioFolder, string indexPath)
    {
        _audioFolder = audioFolder;
        _indexPath = indexPath;
        Load();
    }

    public IReadOnlyList<PendingAudioEntry> List()
    {
        lock (_gate)
        {
            return _entries
                .OrderByDescending(e => e.CapturedAt)
                .ToArray();
        }
    }

    public PendingAudioEntry Add(byte[] pcm16Mono, int sampleRate, string language, string? error)
    {
        if (pcm16Mono == null || pcm16Mono.Length == 0)
            throw new ArgumentException("Audio buffer is empty", nameof(pcm16Mono));

        var id = Guid.NewGuid();
        Directory.CreateDirectory(_audioFolder);
        var path = Path.Combine(_audioFolder, $"{id:N}.wav");

        WavWriter.WriteFile(path, pcm16Mono, sampleRate);

        // PCM16 mono: bytes / (sampleRate * 2) gives seconds.
        var seconds = pcm16Mono.Length / (double)(sampleRate * 2);
        var entry = new PendingAudioEntry
        {
            Id = id,
            FilePath = path,
            CapturedAt = DateTime.UtcNow,
            Duration = TimeSpan.FromSeconds(seconds),
            Language = language ?? "",
            SampleRate = sampleRate,
            LastError = string.IsNullOrWhiteSpace(error) ? null : error
        };

        lock (_gate)
        {
            _entries.Add(entry);
            EvictOldestIfNeeded();
            Save();
        }

        return entry;
    }

    public void Remove(Guid id)
    {
        PendingAudioEntry? removed = null;
        lock (_gate)
        {
            var idx = _entries.FindIndex(e => e.Id == id);
            if (idx < 0) return;
            removed = _entries[idx];
            _entries.RemoveAt(idx);
            Save();
        }

        if (removed != null)
        {
            TryDeleteFile(removed.FilePath);
        }
    }

    /// <summary>
    /// Updates the last error message stored for an existing entry (e.g. after a failed retry).
    /// Silently no-ops if the entry has been removed concurrently.
    /// </summary>
    public void UpdateLastError(Guid id, string? error)
    {
        lock (_gate)
        {
            var entry = _entries.FirstOrDefault(e => e.Id == id);
            if (entry == null) return;
            entry.LastError = string.IsNullOrWhiteSpace(error) ? null : error;
            Save();
        }
    }

    private void EvictOldestIfNeeded()
    {
        while (_entries.Count > MaxEntries)
        {
            var oldest = _entries
                .OrderBy(e => e.CapturedAt)
                .First();
            _entries.Remove(oldest);
            TryDeleteFile(oldest.FilePath);
        }
    }

    private void Load()
    {
        try
        {
            if (!File.Exists(_indexPath)) return;
            var json = File.ReadAllText(_indexPath);
            if (string.IsNullOrWhiteSpace(json)) return;
            var loaded = JsonSerializer.Deserialize<List<PendingAudioEntry>>(json);
            if (loaded == null) return;

            // Drop entries whose file is missing (e.g. user wiped the folder).
            _entries = loaded.Where(e => File.Exists(e.FilePath)).ToList();
        }
        catch
        {
            // Corrupted index: start fresh rather than crashing the app.
            _entries = new List<PendingAudioEntry>();
        }
    }

    private void Save()
    {
        try
        {
            var dir = Path.GetDirectoryName(_indexPath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            var json = JsonSerializer.Serialize(_entries, new JsonSerializerOptions
            {
                WriteIndented = false
            });
            File.WriteAllText(_indexPath, json);
        }
        catch
        {
            // Best-effort persistence; the in-memory list still works for the session.
        }
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (!string.IsNullOrEmpty(path) && File.Exists(path))
                File.Delete(path);
        }
        catch
        {
            // Ignore: the file may be locked (e.g. playback in progress); next launch will clean it up.
        }
    }
}
