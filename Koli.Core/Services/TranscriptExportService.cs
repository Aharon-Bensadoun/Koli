using System.Text;
using System.Text.Json;
using Koli.Models;

namespace Koli.Services;

/// <summary>
/// Exports meeting transcripts to various formats.
/// </summary>
public sealed class TranscriptExportService
{
    public string ExportToText(MeetingSession session)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Meeting: {session.Title}");
        sb.AppendLine($"Date: {session.StartedAt:yyyy-MM-dd HH:mm} - {session.EndedAt:HH:mm}");
        sb.AppendLine($"Participants: {string.Join(", ", session.Participants.Select(p => p.DisplayName))}");
        sb.AppendLine(new string('-', 60));
        sb.AppendLine();

        foreach (var segment in session.Segments)
        {
            var displayName = session.Participants
                .FirstOrDefault(p => p.Id == segment.SpeakerId)?.DisplayName ?? segment.SpeakerId;
            var time = $"[{segment.Timestamp.Hours:D2}:{segment.Timestamp.Minutes:D2}:{segment.Timestamp.Seconds:D2}]";
            sb.AppendLine($"{time} {displayName}:");
            sb.AppendLine($"  {segment.Text}");
            sb.AppendLine();
        }

        return sb.ToString();
    }

    public string ExportToMarkdown(MeetingSession session)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# {session.Title}");
        sb.AppendLine();
        sb.AppendLine($"**Date:** {session.StartedAt:yyyy-MM-dd HH:mm} - {session.EndedAt:HH:mm}");
        sb.AppendLine();
        sb.AppendLine("**Participants:**");
        foreach (var p in session.Participants)
            sb.AppendLine($"- {p.DisplayName}");
        sb.AppendLine();
        sb.AppendLine("---");
        sb.AppendLine();

        string? lastSpeaker = null;
        foreach (var segment in session.Segments)
        {
            var displayName = session.Participants
                .FirstOrDefault(p => p.Id == segment.SpeakerId)?.DisplayName ?? segment.SpeakerId;
            var time = $"{segment.Timestamp.Hours:D2}:{segment.Timestamp.Minutes:D2}:{segment.Timestamp.Seconds:D2}";

            if (displayName != lastSpeaker)
            {
                sb.AppendLine($"### {displayName} `{time}`");
                sb.AppendLine();
                lastSpeaker = displayName;
            }

            sb.AppendLine(segment.Text);
            sb.AppendLine();
        }

        return sb.ToString();
    }

    public string ExportToJson(MeetingSession session)
    {
        return JsonSerializer.Serialize(session, new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
    }
}
