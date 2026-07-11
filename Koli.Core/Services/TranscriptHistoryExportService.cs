using System.Text;
using Koli.Models;

namespace Koli.Services;

public sealed class TranscriptHistoryExportService
{
    public string ExportToText(IEnumerable<TranscriptHistoryEntry> entries)
    {
        var ordered = entries.OrderBy(entry => entry.Timestamp).ToList();
        var output = new StringBuilder();

        foreach (var entry in ordered)
        {
            if (output.Length > 0)
                output.AppendLine().AppendLine(new string('-', 72)).AppendLine();

            var local = entry.Timestamp.Kind == DateTimeKind.Utc
                ? entry.Timestamp.ToLocalTime()
                : entry.Timestamp;
            output.AppendLine($"Date: {local:yyyy-MM-dd HH:mm:ss zzz}");
            output.AppendLine($"Language: {entry.Language}");
            output.AppendLine($"Type: {entry.Kind}");
            if (!string.IsNullOrWhiteSpace(entry.ProfileName))
                output.AppendLine($"Profile: {entry.ProfileName}");
            if (!string.IsNullOrWhiteSpace(entry.SourceText))
            {
                output.AppendLine().AppendLine("Source:");
                output.AppendLine(entry.SourceText.Trim());
                output.AppendLine().AppendLine("Result:");
            }
            else
            {
                output.AppendLine().AppendLine("Text:");
            }
            output.AppendLine(entry.Text.Trim());
        }

        return output.ToString();
    }
}
