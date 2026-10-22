using System.Linq;
using Briefing.Core.Contract;
using Briefing.Core.Screen;

namespace Briefing.Tests;

// 카파·등대지기 해금 선행 표시. 서버가 퀘스트마다 kappa·lightkeeper 를 보내고, 게임에서 만든 줄은 같은 id 의 서버 퀘스트에서 가져온다.
public class QuestGoalFlagsTests
{
    private const string Json = @"{""schemaVersion"":1,""maps"":[
        {""key"":""a"",""name"":""A"",""count"":3,""quests"":[
            {""questId"":""k"",""traderId"":""t"",""name"":""K"",""kappa"":true},
            {""questId"":""l"",""traderId"":""t"",""name"":""L"",""lightkeeper"":true},
            {""questId"":""n"",""traderId"":""t"",""name"":""N""}]},
        {""key"":""b"",""name"":""B"",""count"":1,""quests"":[
            {""questId"":""x"",""traderId"":""t"",""name"":""X"",""kappa"":true}]}],
        ""anywhere"":{""quests"":[{""questId"":""w"",""traderId"":""t"",""name"":""W"",""kappa"":true,""lightkeeper"":true}]},
        ""kappaQuests"":[""h"",""k"",""w"",""x""],""lightkeeperQuests"":[""h"",""l"",""w""]}";

    private static readonly BriefingResponse Response = BriefingParser.Parse(Json).Response;

    [Fact]
    public void Parser_reads_the_flags_and_missing_ones_are_false()
    {
        var a = Response.Maps[0].Quests;
        Assert.Equal(new[] { true, false, false }, a.Select(q => q.Kappa));
        Assert.Equal(new[] { false, true, false }, a.Select(q => q.Lightkeeper));
    }

    [Fact]
    public void Here_and_anywhere_rows_carry_the_flags()
    {
        var m = LeftColumn.Build(Response, "a");

        Assert.Equal(new[] { true, false, false }, m.Here.Rows.Select(r => r.Kappa));
        Assert.Equal(new[] { false, true, false }, m.Here.Rows.Select(r => r.Lightkeeper));
        var w = Assert.Single(m.Anywhere.Rows);
        Assert.True(w.Kappa);
        Assert.True(w.Lightkeeper);
    }

    [Fact]
    public void Hand_in_and_turn_in_rows_take_the_flags_of_the_server_quest_with_the_same_id()
    {
        var goals = new[]
        {
            new HandoverGoal("x", "X", "t", "c1", "give", 0, 1, true), // 다른 맵(b)에만 있는 퀘스트
            new HandoverGoal("z", "Z", "t", "c2", "give", 0, 1, true), // 응답에 없는 퀘스트
        };
        var m = LeftColumn.Build(Response, "a", goals, turnIns: new[] { new TurnInQuest("l", "L", "t") });

        Assert.Equal(new[] { "x", "z" }, m.Ready.Rows.Select(r => r.QuestId));
        Assert.Equal(new[] { true, false }, m.Ready.Rows.Select(r => r.Kappa));
        Assert.True(m.Ready.Rows.All(r => r.HandInReady)); // 손 아이콘을 붙여도 표시는 그대로
        var l = Assert.Single(m.TurnIn.Rows);
        Assert.True(l.Lightkeeper);
        Assert.False(l.Kappa);
    }

    [Fact]
    public void Quests_missing_from_the_response_take_the_flags_from_the_top_level_lists()
    {
        Assert.Equal(new[] { "h", "k", "w", "x" }, Response.KappaQuests);
        Assert.Equal(new[] { "h", "l", "w" }, Response.LightkeeperQuests);

        var goals = new[] { new HandoverGoal("h", "H", "t", "c1", "give", 0, 1, true) }; // 건네주기만 남아 응답에 없는 퀘스트
        var m = LeftColumn.Build(Response, "a", goals, turnIns: new[] { new TurnInQuest("h", "H", "t") },
            expandedSection: LeftColumn.TurnInKey, expandedQuestId: "h");

        var h = Assert.Single(m.TurnIn.Rows);
        Assert.True(h.Kappa);
        Assert.True(h.Lightkeeper);
        Assert.True(m.Expanded.Kappa);
        Assert.True(m.Expanded.Lightkeeper);
    }

    [Fact]
    public void Old_quest_codex_without_the_lists_still_parses()
    {
        var r = BriefingParser.Parse(@"{""schemaVersion"":1,""maps"":[]}").Response;
        Assert.Empty(r.KappaQuests);
        Assert.Empty(r.LightkeeperQuests);
    }

    [Fact]
    public void Expanded_quest_carries_the_flags_of_its_row()
    {
        var k = LeftColumn.Build(Response, "a", expandedSection: LeftColumn.HereKey, expandedQuestId: "k").Expanded;
        Assert.True(k.Kappa);
        Assert.False(k.Lightkeeper);

        var w = LeftColumn.Build(Response, "a", expandedSection: LeftColumn.AnywhereKey, expandedQuestId: "w").Expanded;
        Assert.True(w.Kappa);
        Assert.True(w.Lightkeeper);

        var n = LeftColumn.Build(Response, "a", expandedSection: LeftColumn.HereKey, expandedQuestId: "n").Expanded;
        Assert.False(n.Kappa);
        Assert.False(n.Lightkeeper);
    }
}
