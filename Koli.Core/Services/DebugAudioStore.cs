using System.Text.Json;

namespace Koli.Services;

/// <summary>
/// Metadata for a recording kept for debug replay (success or failure).
/// Audio bytes live as a WAV at <see cref="FilePath"/>.
/// </summary>
public sealed class DebugAudioEntry
{
    public Guid Id { get; init; }
    public string FilePath { get; init; } = "";
    public DateTime CapturedAt { get; init; }
    public TimeSpan Duration { get; init; }
    public string Language { get; init; } = "";
    public int SampleRate { get; init; } = WavWriter.DefaultSampleRate;
    /// <summary>Recording kind, e.g. Dictation, Assistant, or a custom action name.</summary>
    public string Kind { get; init; } = "Dictation";

    public string DurationLabel =>
        Duration.TotalHours >= 1
            ? Duration.ToString(@"h\:mm\:ss")
            : Duration.ToString(@"m\:ss");
}

/// <summary>
/// Persists captures as WAV files for debug replay, with a JSON index and retention limit.
/// Separate from <see cref="PendingAudioStore"/> (failure retry queue).
/// </summary>
public sealed class DebugAudioStore
{
    public const int DefaultMaxEntries = 30;

    private readonly string _audioFolder;
    private readonly string _indexPath;
    private readonly object _gate = new();
    private List<DebugAudioEntry> _entries = new();

    public DebugAudioStore(string audioFolder, string indexPath)
    {
        _audioFolder = audioFolder;
        _indexPath = indexPath;
        Load();
    }

    public IReadOnlyList<DebugAudioEntry> List()
    {
        lock (_gate)
        {
            return _entries
                .OrderByDescending(e => e.CapturedAt)
                .ToArray();
        }
    }

    public IReadOnlyList<DebugAudioEntry> GetAll() => List();

    public DebugAudioEntry Add(
        byte[] pcm16Mono,
        int sampleRate,
        string language,
        string kind,
        int maxEntries = DefaultMaxEntries)
    {
        if (pcm16Mono == null || pcm16Mono.Length == 0)
            throw new ArgumentException("Audio buffer is empty", nameof(pcm16Mono));

        if (maxEntries < 1)
            maxEntries = DefaultMaxEntries;

        var id = Guid.NewGuid();
        Directory.CreateDirectory(_audioFolder);
        var path = Path.Combine(_audioFolder, $"{id:N}.wav");

        WavWriter.WriteFile(path, pcm16Mono, sampleRate);

        var seconds = pcm16Mono.Length / (double)(sampleRate * 2);
        var entry = new DebugAudioEntry
        {
            Id = id,
            FilePath = path,
            CapturedAt = DateTime.Now,
            Duration = TimeSpan.FromSeconds(seconds),
            Language = language ?? "",
            SampleRate = sampleRate,
            Kind = string.IsNullOrWhiteSpace(kind) ? "Dictation" : kind.Trim()
        };

        lock (_gate)
        {
            _entries.Add(entry);
            EvictOldestIfNeeded(maxEntries);
            Save();
        }

        return entry;
    }

    public void Remove(Guid id)
    {
        DebugAudioEntry? removed = null;
        lock (_gate)
        {
            var idx = _entries.FindIndex(e => e.Id == id);
            if (idx < 0) return;
            removed = _entries[idx];
            _entries.RemoveAt(idx);
            Save();
        }

        if (removed != null)
            TryDeleteFile(removed.FilePath);
    }

    public void Clear()
    {
        List<DebugAudioEntry> snapshot;
        lock (_gate)
        {
            snapshot = _entries.ToList();
            _entries.Clear();
            Save();
        }

        foreach (var entry in snapshot)
            TryDeleteFile(entry.FilePath);
    }

    private void EvictOldestIfNeeded(int maxEntries)
    {
        while (_entries.Count > maxEntries)
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
            var loaded = JsonSerializer.Deserialize<List<DebugAudioEntry>>(json);
            if (loaded == null) return;

            _entries = loaded.Where(e => File.Exists(e.FilePath)).ToList();
        }
        catch
        {
            _entries = new List<DebugAudioEntry>();
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
            // Ignore: file may be locked during playback; next launch cleans it up.
        }
    }
}
