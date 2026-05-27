using Koli.Config;
using Koli.Platform;
using Koli.Services;
using Xunit;

namespace Koli.Core.Tests;

public class InputLanguageServiceTests
{
    [Theory]
    [InlineData(0x0409, "en")]
    [InlineData(0x040C, "fr")]
    [InlineData(0x040D, "he")]
    [InlineData(0x0411, "en")]
    public void MapLanguageIdToCode_MapsCommonLayouts(ushort langId, string expected)
    {
        Assert.Equal(expected, InputLanguageService.MapLanguageIdToCode(langId));
    }
}

public class OpenAiModelProfilesTests
{
    [Theory]
    [InlineData("", false)]
    [InlineData("https://api.openai.com", false)]
    [InlineData("https://corp.example.com/api/AI/queryAudio", true)]
    public void IsOnPremiseStyleEndpoint_DetectsEndpointStyle(string endpoint, bool expected)
    {
        Assert.Equal(expected, OpenAiModelProfiles.IsOnPremiseStyleEndpoint(endpoint));
    }

    [Theory]
    [InlineData("gpt-realtime-whisper", true)]
    [InlineData("whisper-1", false)]
    public void IsRealtimeTranscriptionModel_DetectsModel(string model, bool expected)
    {
        Assert.Equal(expected, OpenAiModelProfiles.IsRealtimeTranscriptionModel(model));
    }

    [Fact]
    public void CreateMeetingTranscriptionSettings_ForcesRealtimeOnOpenAiCloud()
    {
        var source = new AzureOpenAISettings { Endpoint = "", Model = "whisper-1" };
        var meeting = OpenAiModelProfiles.CreateMeetingTranscriptionSettings(source);
        Assert.Equal("gpt-realtime-whisper", meeting.Model);
        Assert.True(OpenAiModelProfiles.ShouldUseRealtimeTranscription(meeting));
    }

    [Fact]
    public void CreateMeetingTranscriptionSettings_KeepsExistingRealtimeModel()
    {
        var source = new AzureOpenAISettings { Endpoint = "", Model = "gpt-realtime" };
        var meeting = OpenAiModelProfiles.CreateMeetingTranscriptionSettings(source);
        Assert.Equal("gpt-realtime", meeting.Model);
    }

    [Theory]
    [InlineData("gpt-realtime-whisper", "gpt-realtime")]
    [InlineData("gpt-realtime", "gpt-realtime")]
    [InlineData("whisper-1", "gpt-realtime")]
    public void ResolveRealtimeSessionModel_MapsWhisperToRealtimeSession(string configured, string expected)
    {
        Assert.Equal(expected, OpenAiModelProfiles.ResolveRealtimeSessionModel(configured));
    }

    [Theory]
    [InlineData("gpt-realtime-whisper", "gpt-realtime-whisper")]
    [InlineData("gpt-realtime", "gpt-realtime-whisper")]
    public void ResolveRealtimeTranscriptionModel_MapsSessionToWhisperTranscription(string configured, string expected)
    {
        Assert.Equal(expected, OpenAiModelProfiles.ResolveRealtimeTranscriptionModel(configured));
    }

    [Fact]
    public void BuildWebSocketUrl_UsesSessionModelNotWhisperTranscriptionModel()
    {
        var settings = new AzureOpenAISettings { Model = "gpt-realtime-whisper" };
        var url = OpenAiRealtimeTranscriptionSession.BuildWebSocketUrl(settings);
        Assert.Contains("model=gpt-realtime", url, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("gpt-realtime-whisper", url, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BuildSessionUpdateJson_PutsWhisperInTranscriptionSubModel()
    {
        var settings = new AzureOpenAISettings { Model = "gpt-realtime-whisper" };
        var json = OpenAiRealtimeTranscriptionSession.BuildSessionUpdateJson(settings);
        Assert.Contains("\"model\":\"gpt-realtime-whisper\"", json, StringComparison.Ordinal);
    }
}
