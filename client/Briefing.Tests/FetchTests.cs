using System;
using Briefing.Core.Fetch;

namespace Briefing.Tests;

public class FetchTests
{
    [Theory]
    [InlineData("kr", "kr")]
    [InlineData(" KR ", "kr")]
    [InlineData("ru", "ru")]
    [InlineData("", "en")]
    [InlineData(null, "en")]
    public void Lang_follows_game_culture(string culture, string expected)
    {
        Assert.Equal(expected, BriefingRequest.LangFor(culture));
    }

    [Fact]
    public void Path_escapes_query_values()
    {
        Assert.Equal("/questcodex/api/briefing?profile=6a8328d2c1ae327dd00378cf&lang=kr",
            BriefingRequest.PathFor("6a8328d2c1ae327dd00378cf", "kr"));
        Assert.Equal("/questcodex/api/briefing?profile=a%20b%26c&lang=en", BriefingRequest.PathFor("a b&c", "en"));
        Assert.Equal("/questcodex/api/briefing?profile=&lang=en", BriefingRequest.PathFor(null, "en"));
    }

    [Fact]
    public void Ok_response_is_parsed()
    {
        var result = FetchClassifier.Classify(200, FixtureFiles.Read(FixtureFiles.Active, "kr"));

        Assert.Equal(FetchKind.Ok, result.Kind);
        Assert.Equal(3, result.Response.Maps.Count);
        Assert.Null(result.Detail);
    }

    [Theory]
    [InlineData(200, @"{""schemaVersion"":0}", FetchKind.QuestCodexOutdated, "schemaVersion 0")]
    [InlineData(200, @"{""schemaVersion"":2}", FetchKind.BriefingOutdated, "schemaVersion 2")]
    [InlineData(200, "<html></html>", FetchKind.Failed, "invalidResponse")]
    [InlineData(404, "", FetchKind.QuestCodexMissing, "http 404")]
    [InlineData(404, "Not Found", FetchKind.QuestCodexMissing, "http 404")]
    [InlineData(404, @"{""error"":""profileNotFound""}", FetchKind.Failed, "profileNotFound")]
    [InlineData(400, @"{""error"":""missingProfile""}", FetchKind.Failed, "missingProfile")]
    [InlineData(400, @"{""error"":""unknownLang"",""supported"":[""en"",""kr""]}", FetchKind.Failed, "unknownLang")]
    [InlineData(409, @"{""error"":""noCharacter""}", FetchKind.Failed, "noCharacter")]
    [InlineData(500, @"{""error"":""briefingBuildFailed""}", FetchKind.Failed, "briefingBuildFailed")]
    [InlineData(502, "", FetchKind.Failed, "http 502")]
    public void Other_responses_are_classified(int status, string body, FetchKind kind, string detail)
    {
        var result = FetchClassifier.Classify(status, body);

        Assert.Equal(kind, result.Kind);
        Assert.Equal(detail, result.Detail);
        Assert.Null(result.Response);
    }

    [Fact]
    public void Cache_keeps_last_success_through_failures()
    {
        var cache = new BriefingCache();
        var t0 = new DateTimeOffset(2026, 10, 11, 12, 0, 0, TimeSpan.Zero);
        var ok = FetchClassifier.Classify(200, FixtureFiles.Read(FixtureFiles.Active, "kr"));

        cache.Record(ok, t0);
        cache.Record(FetchResult.Failed("timeout"), t0.AddMinutes(5));

        Assert.Same(ok.Response, cache.Last);
        Assert.Equal(t0, cache.LastSuccessAt);
        Assert.Equal(FetchKind.Failed, cache.LastResult.Kind);
        Assert.Equal("timeout", cache.LastResult.Detail);
    }

    [Fact]
    public void Empty_cache_has_nothing()
    {
        var cache = new BriefingCache();

        Assert.Null(cache.Last);
        Assert.Null(cache.LastSuccessAt);
        Assert.Null(cache.LastResult);
    }
}
