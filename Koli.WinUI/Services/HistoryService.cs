using System.Text.Json;
using Koli.Models;

namespace Koli.WinUI.Services;

public sealed class HistoryService
{
    private const int MaxHistoryEntries = 100;
    private readonly List<TranscriptHistoryEntry> _history = new();
    private readonly string _historyPath;

    public HistoryService(string configDirectory)
    {
        _historyPath = Path.Combine(configDirectory, "history.json");
        Load();
    }

    public IReadOnlyList<TranscriptHistoryEntry> Entries => _history;

    public event EventHandler? HistoryChanged;

    public void Add(string text, string language)
    {
        if (string.IsNullOrWhiteSpace(text))
            return;

        _history.Insert(0, new TranscriptHistoryEntry
        {
            Timestamp = DateTime.UtcNow,
            Language = language,
            Text = text.Trim()
        });

        while (_history.Count > MaxHistoryEntries)
            _history.RemoveAt(_history.Count - 1);

        Save();
        HistoryChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Load()
    {
        try
        {
            if (!File.Exists(_historyPath))
                return;

            var json = File.ReadAllText(_historyPath);
            if (string.IsNullOrWhiteSpace(json))
                return;

            var loaded = JsonSerializer.Deserialize<List<TranscriptHistoryEntry>>(json);
            if (loaded == null)
                return;

            _history.Clear();
            _history.AddRange(loaded);
            while (_history.Count > MaxHistoryEntries)
                _history.RemoveAt(_history.Count - 1);
        }
        catch
        {
            // Ignore corrupted history
        }
    }

    public void Save()
    {
        try
        {
            var dir = Path.GetDirectoryName(_historyPath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            var json = JsonSerializer.Serialize(_history, new JsonSerializerOptions { WriteIndented = false });
            File.WriteAllText(_historyPath, json);
            HistoryChanged?.Invoke(this, EventArgs.Empty);
        }
        catch
        {
            // Best effort
        }
    }
}
