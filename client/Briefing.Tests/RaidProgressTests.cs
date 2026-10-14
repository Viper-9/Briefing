using System.Collections.Generic;
using System.Linq;
using Briefing.Core.Contract;
using Briefing.Core.Screen;

namespace Briefing.Tests;

// 레이드 맵 찾기(RaidMap)와 실시간 진행 덮어쓰기(LiveProgress).
public class RaidProgressTests
{
    private static BriefingResponse Active() => FixtureFiles.Load(FixtureFiles.Active, "kr");

    private static Dictionary<string, LiveObjective> Live(params LiveObjective[] values) =>
        values.ToDictionary(v => v.ConditionId);

    [Fact]
    public void Raid_map_matches_location_ids_ignoring_case_and_variants()
    {
        var r = Active();

        Assert.Equal("bigmap", RaidMap.Match(r, "bigmap"));
        Assert.Equal("bigmap", RaidMap.Match(r, "BigMap"));
        Assert.Equal("factory4_day", RaidMap.Match(r, "factory4_night")); // 야간 공장은 같은 탭의 locationIds 에 있다
        Assert.Equal("labyrinth", RaidMap.Match(r, " labyrinth "));
    }

    [Fact]
    public void Raid_map_is_null_for_hideout_blank_and_unknown()
    {
        var r = Active();

        Assert.Null(RaidMap.Match(r, "hideout"));
        Assert.Null(RaidMap.Match(r, "Hideout"));
        Assert.Null(RaidMap.Match(r, null));
        Assert.Null(RaidMap.Match(r, ""));
        Assert.Null(RaidMap.Match(r, "woods")); // 이 응답(잘린 사본)에 없는 맵
        Assert.Null(RaidMap.Match(null, "bigmap"));
    }

    [Fact]
    public void Totals_reproduce_the_server_quest_numbers()
    {
        var r = Active();
        var quests = r.Maps.SelectMany(m => m.Quests).Concat(r.Anywhere.Quests).ToList();

        Assert.NotEmpty(quests);
        foreach (var q in quests)
        {
            var (current, target) = LiveProgress.Totals(q.Objectives);
            Assert.Equal(q.Target, target);
            Assert.Equal(q.Current, current);
        }
    }

    [Fact]
    public void Unchanged_values_return_the_same_response()
    {
        var r = Active();

        Assert.Same(r, LiveProgress.Apply(r, null));
        Assert.Same(r, LiveProgress.Apply(r, Live()));
        Assert.Same(r, LiveProgress.Apply(r, Live(
            new LiveObjective("675c1c980fd114390c638b89", 6, false),     // Provide Viewership 그대로
            new LiveObjective("60e740b8b567ff641b129573", 5, false),     // 영점 조정 그대로
            new LiveObjective("no-such-condition", 3, true))));
    }

    [Fact]
    public void Changed_objective_rewrites_only_its_quest_map_and_response()
    {
        var r = Active();
        var bigmap = r.Maps.Single(m => m.Key == "bigmap");
        var factory = r.Maps.Single(m => m.Key == "factory4_day");
        var viewership = bigmap.Quests.Single(q => q.QuestId == "675c15fbf7da9792a4059871");
        var other = bigmap.Quests.Single(q => q.QuestId == "60e71b9bbd90872cb85440f3");

        var applied = LiveProgress.Apply(r, Live(new LiveObjective("675c1c980fd114390c638b89", 9, false)));

        Assert.NotSame(r, applied);
        var appliedBigmap = applied.Maps.Single(m => m.Key == "bigmap");
        var appliedQuest = appliedBigmap.Quests.Single(q => q.QuestId == viewership.QuestId);
        Assert.Equal(9, appliedQuest.Objectives.Single().Current);
        Assert.False(appliedQuest.Objectives.Single().Done);
        Assert.Equal(9, appliedQuest.Current);
        Assert.Equal(15, appliedQuest.Target);
        Assert.Same(viewership.Objectives.Single().Locations, appliedQuest.Objectives.Single().Locations); // 위치는 공유
        // 바뀌지 않은 것은 같은 객체(깊은 복사 없음).
        Assert.Same(other, appliedBigmap.Quests.Single(q => q.QuestId == other.QuestId));
        Assert.Same(factory, applied.Maps.Single(m => m.Key == "factory4_day"));
        Assert.Same(r.Anywhere, applied.Anywhere);
        Assert.Same(r.Traders, applied.Traders);
        Assert.Same(bigmap.Bring, appliedBigmap.Bring);
        Assert.Same(bigmap.Exits, appliedBigmap.Exits);
        // 원래 응답은 그대로(다음 그리기는 다시 원래 응답에서 시작한다).
        Assert.Equal(6, viewership.Objectives.Single().Current);
        Assert.Equal(6, viewership.Current);
        Assert.Same(viewership, bigmap.Quests.Single(q => q.QuestId == viewership.QuestId));
    }

    [Fact]
    public void Done_objective_counts_as_full_and_sums_with_the_others()
    {
        var r = Active();

        // Hobby Club(세관 목표 4개 0/1) 중 둘을 끝냄, 하나는 값만 오르고 끝나지 않음. anywhere 쪽 Hobby Club 은 다른 conditionId 라 그대로.
        var applied = LiveProgress.Apply(r, Live(
            new LiveObjective("685e95c44b14e68996ad6590", 1, true),
            new LiveObjective("685e95d28ba62d57a3235675", 0, true),  // 게임이 끝났다고 하면 수치가 덜 차도 꽉 찬 것으로
            new LiveObjective("685e95def338135da83ff978", 0, false)));

        var quest = applied.Maps.Single(m => m.Key == "bigmap").Quests.Single(q => q.QuestId == "684009026ceedc792c09b2a7");
        Assert.Equal(new[] { true, true, false, false }, quest.Objectives.Select(o => o.Done));
        Assert.Equal(2, quest.Current);
        Assert.Equal(4, quest.Target);
        Assert.Same(r.Anywhere, applied.Anywhere);
    }

    [Fact]
    public void Anywhere_quests_are_rewritten_too_and_null_targets_stay_null()
    {
        var json = @"{""schemaVersion"":1,""maps"":[],""anywhere"":{""quests"":[
            {""questId"":""q1"",""name"":""A"",""current"":null,""target"":null,""objectives"":[{""conditionId"":""c1"",""text"":""t"",""current"":0,""target"":null}]},
            {""questId"":""q2"",""name"":""B"",""current"":1,""target"":10,""objectives"":[{""conditionId"":""c2"",""text"":""t"",""current"":1,""target"":10}]}]}}";
        var r = BriefingParser.Parse(json).Response;

        var applied = LiveProgress.Apply(r, Live(new LiveObjective("c1", 1, true), new LiveObjective("c2", 12, false)));

        var a = applied.Anywhere.Quests[0];
        Assert.True(a.Objectives[0].Done);
        Assert.Null(a.Current);
        Assert.Null(a.Target);
        var b = applied.Anywhere.Quests[1];
        Assert.Equal(12, b.Objectives[0].Current); // 목표 current 는 원값
        Assert.Equal(10, b.Current);               // 퀘스트 줄은 target 까지
        Assert.Same(r.Anywhere.Bring, applied.Anywhere.Bring);
    }

    [Fact]
    public void Live_targets_are_the_selected_map_and_anywhere_only()
    {
        var r = Active();
        var targets = LiveProgress.Targets(r.Maps.Single(m => m.Key == "bigmap"), r.Anywhere);
        var ids = targets.ConditionIds;

        Assert.Contains("675c1c980fd114390c638b89", ids);  // 세관 묶음
        Assert.Contains("685e95c44b14e68996ad6590", ids);  // 세관 Hobby Club
        Assert.Contains("6840714469caa738cc1225c3", ids);  // 아무 맵에서나 Hobby Club
        Assert.Contains("60e740b8b567ff641b129573", ids);  // 아무 맵에서나
        Assert.DoesNotContain("669fafa09a13dcb8bf66e6bd", ids); // 공장 묶음은 읽지 않는다
        Assert.Equal(20, ids.Count); // 세관 목표 10개 + anywhere 목표 10개
        Assert.Equal(14, targets.QuestIds.Count); // 세관 7 + anywhere 8 - 둘 다에 있는 Hobby Club 1
        Assert.DoesNotContain("669fa394e0c9f9fafa082897", targets.QuestIds); // 공장 Forklift Certified
        Assert.Empty(LiveProgress.Targets(null, null).ConditionIds);
    }
}
