using System;
using System.Linq;
using Briefing.Core.Contract;

namespace Briefing.Tests;

public class BriefingParserTests
{
    [Fact]
    public void Reads_top_level_fields()
    {
        var r = FixtureFiles.Load(FixtureFiles.Active, "kr");

        Assert.Equal(1, r.SchemaVersion);
        Assert.Equal("test", r.QuestCodexVersion);
        Assert.Equal(new DateTimeOffset(1970, 1, 1, 0, 0, 0, TimeSpan.Zero), r.GeneratedAt);
        Assert.Equal("kr", r.Lang);
        Assert.Equal(15, r.Traders.Count);
        Assert.Equal("프라퍼", r.Traders["54cb50c76803fa8b248b4571"].Name);
    }

    [Fact]
    public void Reads_maps_in_server_order()
    {
        var r = FixtureFiles.Load(FixtureFiles.Active, "kr");

        Assert.Equal(new[] { "bigmap", "factory4_day", "labyrinth" }, r.Maps.Select(m => m.Key));
        var customs = r.Maps[0];
        Assert.Equal("세관", customs.Name);
        Assert.Equal(10, customs.Count);
        Assert.True(customs.HasMap);
        Assert.Equal("bigmap", customs.MapFolder);
        Assert.Equal(new[] { "bigmap" }, customs.LocationIds);
        Assert.Equal(new[] { "factory4_day", "factory4_night" }, r.Maps[1].LocationIds);
        Assert.Equal(0, r.Maps[2].Count);
        Assert.Empty(r.Maps[2].Quests);
    }

    [Fact]
    public void Reads_quests_with_server_markers_and_progress()
    {
        var customs = FixtureFiles.Load(FixtureFiles.Active, "kr").Maps[0];

        Assert.Equal(7, customs.Quests.Count);
        Assert.Equal(new int?[] { 1, 2, 3, 4, null, null, null }, customs.Quests.Select(q => q.Marker));
        var first = customs.Quests[0];
        Assert.Equal("675c15fbf7da9792a4059871", first.QuestId);
        Assert.Equal("6617beeaa9cfa777ca915b7c", first.TraderId);
        Assert.Equal("Provide Viewership", first.Name);
        Assert.Equal("Eliminate any target inside the industrial plant on Customs ×15", first.Summary);
        Assert.Equal(6d, first.Current);
        Assert.Equal(15d, first.Target);
        Assert.Equal("목표 4개", customs.Quests[3].Summary);
    }

    [Fact]
    public void Reads_anywhere_quests_without_markers()
    {
        var anywhere = FixtureFiles.Load(FixtureFiles.Active, "kr").Anywhere;

        Assert.Equal(8, anywhere.Quests.Count);
        Assert.All(anywhere.Quests, q => Assert.Null(q.Marker));
        Assert.Equal("영점 조정", anywhere.Quests[0].Name);
        Assert.Equal(5d, anywhere.Quests[0].Current);
        Assert.Equal(20d, anywhere.Quests[0].Target);
    }

    [Fact]
    public void Reads_empty_profile()
    {
        var r = FixtureFiles.Load(FixtureFiles.Empty, "kr");

        Assert.Equal(3, r.Maps.Count);
        Assert.All(r.Maps, m => { Assert.Equal(0, m.Count); Assert.Empty(m.Quests); });
        Assert.Empty(r.Anywhere.Quests);
        Assert.Equal(15, r.Traders.Count);
    }

    [Fact]
    public void Reads_english_texts()
    {
        var r = FixtureFiles.Load(FixtureFiles.Active, "en");

        Assert.Equal("en", r.Lang);
        Assert.Equal("Customs", r.Maps[0].Name);
        Assert.Equal("Capturing Outposts", r.Maps[0].Quests[1].Name);
        Assert.Equal("Prapor", r.Traders["54cb50c76803fa8b248b4571"].Name);
    }

    [Fact]
    public void Ignores_unknown_fields()
    {
        const string json = @"{""schemaVersion"":1,""future"":{""x"":[1,2]},
            ""maps"":[{""key"":""bigmap"",""newField"":[1,2],""quests"":[{""questId"":""q"",""extra"":true}]}],
            ""anywhere"":{""quests"":[],""bring"":[{""need"":1}]}}";

        var r = BriefingParser.Parse(json);

        Assert.Equal(ParseOutcome.Ok, r.Outcome);
        Assert.Equal("bigmap", r.Response.Maps[0].Key);
        Assert.Equal("q", r.Response.Maps[0].Quests[0].QuestId);
    }

    [Fact]
    public void Nulls_and_missing_collections_become_safe_defaults()
    {
        const string json = @"{""schemaVersion"":1,""traders"":null,""anywhere"":null,
            ""maps"":[{""key"":""labyrinth"",""mapFolder"":null,""locationIds"":null,""quests"":[
              {""questId"":""q"",""marker"":null,""current"":null,""target"":null}]},{""key"":""woods""}]}";

        var r = BriefingParser.Parse(json).Response;

        Assert.Empty(r.Traders);
        Assert.Empty(r.Anywhere.Quests);
        Assert.Null(r.Maps[0].MapFolder);
        Assert.Empty(r.Maps[0].LocationIds);
        var q = r.Maps[0].Quests[0];
        Assert.Null(q.Marker);
        Assert.Null(q.Current);
        Assert.Null(q.Target);
        Assert.Empty(r.Maps[1].Quests);
    }

    [Theory]
    [InlineData(0, ParseOutcome.QuestCodexOutdated)]
    [InlineData(2, ParseOutcome.BriefingOutdated)]
    [InlineData(1, ParseOutcome.Ok)]
    public void Judges_schema_version(int version, ParseOutcome expected)
    {
        var r = BriefingParser.Parse($@"{{""schemaVersion"":{version}}}");

        Assert.Equal(expected, r.Outcome);
        Assert.Equal(version, r.SchemaVersion);
        Assert.Equal(expected == ParseOutcome.Ok, r.Response != null);
    }

    [Theory]
    [InlineData((string)null)]
    [InlineData("")]
    [InlineData("<html>Not Found</html>")]
    [InlineData("[]")]
    [InlineData(@"{""error"":""profileNotFound""}")]
    [InlineData(@"{""schemaVersion"":""1""}")]
    [InlineData(@"{""schemaVersion"":1,""maps"":""x""}")]
    public void Anything_else_is_not_a_briefing(string json)
    {
        var r = BriefingParser.Parse(json);

        Assert.Equal(ParseOutcome.NotBriefing, r.Outcome);
        Assert.Null(r.Response);
    }

    [Theory]
    [InlineData(@"{""error"":""profileNotFound""}", "profileNotFound")]
    [InlineData(@"{""error"":""unknownLang"",""supported"":[""en"",""kr""]}", "unknownLang")]
    [InlineData(@"{""error"":5}", null)]
    [InlineData("Not Found", null)]
    [InlineData("", null)]
    [InlineData(null, null)]
    public void Reads_error_code_from_error_body(string body, string expected)
    {
        Assert.Equal(expected, BriefingParser.ErrorCode(body));
    }
}
