using System.Linq;
using Briefing.Core.Contract;

namespace Briefing.Tests;

public class ObjectiveParsingTests
{
    private static readonly BriefingResponse Active = FixtureFiles.Load(FixtureFiles.Active, "kr");

    [Fact]
    public void Reads_objective_fields_and_locations()
    {
        var o = Active.Maps[0].Quests[0].Objectives.Single(); // Provide Viewership

        Assert.Equal("675c1c980fd114390c638b89", o.ConditionId);
        Assert.Equal("Eliminate any target inside the industrial plant on Customs ×15", o.Text);
        Assert.Equal(6d, o.Current);
        Assert.Equal(15d, o.Target);
        Assert.False(o.InferredMap);
        var loc = o.Locations.Single();
        Assert.Equal("bigmap", loc.Map);
        Assert.Single(loc.Points);
        Assert.Single(loc.Areas);
    }

    [Fact]
    public void Reads_all_objectives_in_server_order()
    {
        var hobby = Active.Maps[0].Quests[3]; // Hobby Club, 목표 4개

        Assert.Equal(new[] { "685e95c44b14e68996ad6590", "685e95d28ba62d57a3235675", "685e95def338135da83ff978", "685e95f0ac5f975749f6c1f3" },
            hobby.Objectives.Select(o => o.ConditionId));
        Assert.All(hobby.Objectives, o => Assert.True(o.InferredMap));
        Assert.All(hobby.Objectives, o => Assert.Empty(o.Locations.Single().Areas));
    }

    [Fact]
    public void Objective_without_coordinates_has_no_locations()
    {
        var sniper = Active.Maps[0].Quests[4]; // 천국에서 태어난 저격수 — 마커 없음

        Assert.Empty(sniper.Objectives.Single().Locations);
    }

    [Fact]
    public void Anywhere_quests_carry_their_objectives()
    {
        Assert.All(Active.Anywhere.Quests, q => Assert.NotEmpty(q.Objectives));
        Assert.Equal(2, Active.Anywhere.Quests[3].Objectives.Count); // 소음기 사용법, "목표 2개"
    }

    [Fact]
    public void Nulls_and_missing_objective_fields_become_safe_defaults()
    {
        const string json = @"{""schemaVersion"":1,""maps"":[{""key"":""bigmap"",""quests"":[
            {""questId"":""a"",""objectives"":null},
            {""questId"":""b""},
            {""questId"":""c"",""objectives"":[{""conditionId"":""x"",""target"":null,""locations"":null,""future"":1},
                                            {""conditionId"":""y"",""locations"":[{""map"":""bigmap"",""points"":null,""areas"":null}]}]}]}]}";

        var quests = BriefingParser.Parse(json).Response.Maps[0].Quests;

        Assert.Empty(quests[0].Objectives);
        Assert.Empty(quests[1].Objectives);
        var x = quests[2].Objectives[0];
        Assert.Null(x.Target);
        Assert.Equal(0d, x.Current);
        Assert.False(x.InferredMap);
        Assert.Empty(x.Locations);
        var y = quests[2].Objectives[1].Locations.Single();
        Assert.Empty(y.Points);
        Assert.Empty(y.Areas);
    }
}
