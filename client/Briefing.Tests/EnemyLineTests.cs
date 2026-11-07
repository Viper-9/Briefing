using System.Collections.Generic;
using System.Linq;
using Briefing.Core.Contract;
using Briefing.Core.Screen;

namespace Briefing.Tests;

// 펼친 처치·명중 목표 아래 줄(EnemyLine)과 응답의 enemies 읽기.
public class EnemyLineTests
{
    private static BriefingEnemy Enemy(string target, string[] roles = null, string[] names = null, string kind = "kills") => new BriefingEnemy
    {
        Kind = kind, Target = target, TargetName = target,
        Roles = (roles ?? new string[0]).ToList(), Names = (names ?? roles ?? new string[0]).ToList(),
    };

    private static EnemySummary Build(params BriefingEnemy[] enemies) => EnemyLine.Build(enemies);

    [Fact]
    public void Reads_enemies_from_the_response_and_defaults_to_empty()
    {
        const string json = @"{""schemaVersion"":1,""maps"":[{""key"":""bigmap"",""quests"":[{""questId"":""q"",""objectives"":[
            {""conditionId"":""k"",""text"":""Kill bosses"",""current"":0,""target"":5,
             ""enemies"":[{""kind"":""kills"",""target"":""Savage"",""targetName"":""Scavs"",""roles"":[""bossKilla"",""bossKillaAgro""],""names"":[""Killa"",""Killa (Labyrinth)""]}]},
            {""conditionId"":""v"",""text"":""Visit"",""current"":0,""target"":1},
            {""conditionId"":""n"",""text"":""Null"",""current"":0,""target"":1,""enemies"":null}]}]}]}";

        var result = BriefingParser.Parse(json);

        Assert.Equal(ParseOutcome.Ok, result.Outcome);
        var objectives = result.Response.Maps.Single().Quests.Single().Objectives;
        var e = objectives[0].Enemies.Single();
        Assert.Equal("kills", e.Kind);
        Assert.Equal("Savage", e.Target);
        Assert.Equal("Scavs", e.TargetName);
        Assert.Equal(new[] { "bossKilla", "bossKillaAgro" }, e.Roles);
        Assert.Equal(new[] { "Killa", "Killa (Labyrinth)" }, e.Names);
        Assert.Empty(objectives[1].Enemies); // 예전 QuestCodex
        Assert.Empty(objectives[2].Enemies);
    }

    [Fact]
    public void Up_to_three_names_fit_on_the_line_without_a_tooltip()
    {
        var s = Build(Enemy("Savage", new[] { "bossTagilla", "followerTagilla", "infectedTagilla" }, new[] { "타길라", "타길라 경호원", "감염된 타길라" }));

        Assert.Equal("처치 대상: 타길라, 타길라 경호원, 감염된 타길라", s.Line);
        Assert.Null(s.TipTitle);
        Assert.Null(s.TipBody);
    }

    [Fact]
    public void Long_lists_are_cut_and_the_tooltip_has_every_name()
    {
        var names = new[] { "카반", "르샬라", "블랙 디비전", "글루하", "킬라" };

        var s = Build(Enemy("Savage", names.Select(n => "role_" + n).ToArray(), names));

        Assert.Equal("처치 대상: 카반, 르샬라, 블랙 디비전 외 2개", s.Line);
        Assert.Equal("처치 대상", s.TipTitle);
        Assert.Equal("카반, 르샬라, 블랙 디비전, 글루하, 킬라", s.TipBody);
    }

    [Fact]
    public void Merged_any_of_options_share_one_list_without_repeats()
    {
        var s = Build(Enemy("Savage", new[] { "bossKilla" }, new[] { "킬라" }), Enemy("Savage", new[] { "bossKilla", "bossTagilla" }, new[] { "킬라", "타길라" }));

        Assert.Equal("처치 대상: 킬라, 타길라", s.Line);
    }

    [Fact]
    public void Savage_without_roles_counts_every_scav_side_ai()
    {
        var s = Build(Enemy("Savage"));

        Assert.Equal("처치 대상: " + Texts.AnyScavSide, s.Line);
        Assert.Null(s.TipBody);
        // 다른 선택지에 역할 목록이 있어도 진영 전체가 더 넓다.
        Assert.Equal(s.Line, Build(Enemy("Savage", new[] { "bossKilla" }, new[] { "킬라" }), Enemy("savage")).Line);
    }

    [Fact]
    public void Pmc_and_any_targets_without_roles_add_nothing()
    {
        Assert.Null(Build(Enemy("AnyPmc"), Enemy("Usec"), Enemy("Bear"), Enemy("Any")));
        Assert.Null(EnemyLine.Build(null));
        Assert.Null(EnemyLine.Build(new List<BriefingEnemy>()));
    }

    [Fact]
    public void Roles_the_server_hid_are_skipped()
    {
        // 서버는 스톰트루퍼처럼 카운트되지 않는 역할의 이름을 빼서 보낸다. 역할 키를 대신 쓰지 않는다.
        Assert.Null(Build(Enemy("Savage", new[] { "bossStormtrooper" }, new string[0])));
    }

    [Fact]
    public void Names_already_in_the_objective_text_are_not_repeated()
    {
        Assert.Null(EnemyLine.Build(new[] { Enemy("Savage", new[] { "bossKilla" }, new[] { "Killa" }) }, "Locate and neutralize Killa ×1"));
        // 경호원처럼 글에 없는 이름이 하나라도 있으면 전부 적는다.
        var s = EnemyLine.Build(new[] { Enemy("Savage", new[] { "bossTagilla", "followerTagilla" }, new[] { "Tagilla", "Tagilla's guard" }) },
            "Locate and neutralize Tagilla ×1");
        Assert.Equal("처치 대상: Tagilla, Tagilla's guard", s.Line);
        // 진영 전체 문구는 글과 상관없이 적는다.
        Assert.NotNull(EnemyLine.Build(new[] { Enemy("Savage") }, "Eliminate Scavs"));
    }

    [Fact]
    public void Kills_and_shots_get_their_own_labels()
    {
        var many = new[] { "a", "b", "c", "d" };

        var s = Build(Enemy("Savage", many, many), Enemy("Savage", new[] { "x" }, new[] { "x" }, kind: "shots"));

        Assert.Equal("처치 대상: a, b, c 외 1개 · 명중 대상: x", s.Line);
        Assert.Equal(Texts.EnemyBoth, s.TipTitle);
        Assert.Equal("처치 대상: a, b, c, d", s.TipBody);
    }

    [Fact]
    public void Expanded_kill_objective_puts_the_targets_under_its_text()
    {
        var names = new[] { "a", "b", "c", "d" };
        var quest = new BriefingQuest
        {
            QuestId = "q",
            Objectives =
            {
                new BriefingObjective { ConditionId = "k", Text = "Kill bosses", Target = 5, Enemies = { Enemy("Savage", names, names) } },
                new BriefingObjective { ConditionId = "d", Text = "Done kill", Target = 5, Done = true, Enemies = { Enemy("Savage", names, names) } },
                new BriefingObjective { ConditionId = "p", Text = "Kill PMC", Target = 5, Enemies = { Enemy("AnyPmc") } },
            },
        };

        var lines = QuestDetail.Build("s", "q", quest, null).Lines;

        Assert.Equal("처치 대상: a, b, c 외 1개", lines[0].Items);
        Assert.Equal("a, b, c, d", lines[0].TipBody);
        Assert.Null(lines[1].Items); // 끝난 줄
        Assert.Null(lines[1].TipBody);
        Assert.Null(lines[2].Items);
    }
}
