using System.Collections.Generic;
using System.Linq;
using Briefing.Core.Contract;
using Briefing.Core.Screen;

namespace Briefing.Tests;

public class QuestExpandTests
{
    private static readonly BriefingResponse Active = FixtureFiles.Load(FixtureFiles.Active, "kr");

    private const string Viewership = "675c15fbf7da9792a4059871"; // bigmap 마커 1, 목표 1개(점·구역)
    private const string HobbyClub = "684009026ceedc792c09b2a7";  // bigmap 마커 4 목표 4개 + 아무 맵에서나 목표 1개
    private const string Sniper = "5c0bde0986f77479cf22c2f8";     // bigmap 마커 없음, 좌표 없음
    private const string Zeroing = "60e71d23c1bfa3050473b8e6";    // 아무 맵에서나 첫 줄(영점 조정)
    private const string Prapor = "54cb50c76803fa8b248b4571";

    private static HandoverGoal Goal(string questId, string conditionId, bool ready, string name = "Q", string text = "Hand over",
        double current = 0, double? target = 1, string trader = Prapor, string[] items = null) =>
        new HandoverGoal(questId, name, trader, conditionId, text, current, target, ready, items);

    // ── 펼침 목표 줄 ──

    [Fact]
    public void Expanded_here_quest_lists_its_map_objectives_with_marker()
    {
        var d = LeftColumn.Build(Active, "bigmap", null, LeftColumn.HereKey, Viewership).Expanded;

        Assert.Equal(LeftColumn.HereKey, d.SectionKey);
        Assert.Equal(Viewership, d.QuestId);
        var line = d.Lines.Single();
        Assert.Equal(DetailLineKind.Objective, line.Kind);
        Assert.Equal("Eliminate any target inside the industrial plant on Customs ×15", line.Text);
        Assert.Equal("6 / 15", line.Progress);
        Assert.Equal(1, line.Marker);
        Assert.Equal("#ff8c42", line.MarkerColor);
        Assert.Null(line.Items);
    }

    [Fact]
    public void Inferred_objectives_keep_the_plain_text()
    {
        var d = LeftColumn.Build(Active, "bigmap", null, LeftColumn.HereKey, HobbyClub).Expanded;

        Assert.Equal(4, d.Lines.Count);
        Assert.Equal("Stash the AK-50 body at the specified spot on Customs ×1", d.Lines[0].Text);
        Assert.All(d.Lines, o => Assert.Equal(4, o.Marker));
        Assert.All(d.Lines, o => Assert.Equal("#facc15", o.MarkerColor));
        Assert.All(d.Lines, o => Assert.Equal("0 / 1", o.Progress));
    }

    [Fact]
    public void Same_quest_expands_per_section_scope()
    {
        var d = LeftColumn.Build(Active, "bigmap", null, LeftColumn.AnywhereKey, HobbyClub).Expanded;

        Assert.Equal(LeftColumn.AnywhereKey, d.SectionKey);
        var line = d.Lines.Single();          // 아무 맵에서나 쪽 목표 1개
        Assert.Null(line.Marker);             // 아무 맵에서나는 마커가 없다
        Assert.Null(line.MarkerColor);
    }

    [Fact]
    public void Objective_without_coordinates_has_no_marker_circle()
    {
        var line = LeftColumn.Build(Active, "bigmap", null, LeftColumn.HereKey, Sniper).Expanded.Lines.Single();

        Assert.Null(line.Marker);
        Assert.Equal("4 / 5", line.Progress);
    }

    [Fact]
    public void Marked_quest_objective_without_coordinates_has_no_circle()
    {
        var quest = new BriefingQuest
        {
            QuestId = "q", Marker = 2,
            Objectives = new List<BriefingObjective>
            {
                new BriefingObjective { Text = "placed", Target = null, Locations = { new BriefingLocation { Map = "bigmap", Points = { new BriefingPoint() } } } },
                new BriefingObjective { Text = "area", Target = 3, Current = 5, Locations = { new BriefingLocation { Map = "bigmap", Areas = { new BriefingArea() } } } },
                new BriefingObjective { Text = "nowhere", Target = 1, Locations = { new BriefingLocation { Map = "bigmap" } } },
            },
        };

        var lines = QuestDetail.Build(LeftColumn.HereKey, "q", quest, null).Lines;

        Assert.Equal(new int?[] { 2, 2, null }, lines.Select(l => l.Marker));
        Assert.Equal(new[] { "#2ec4b6", "#2ec4b6", null }, lines.Select(l => l.MarkerColor));
        Assert.Null(lines[0].Progress);       // target null → 수치 숨김
        Assert.Equal("5 / 3", lines[1].Progress); // 목표 current 는 원값
    }

    [Fact]
    public void Expanded_is_null_when_the_row_is_not_visible()
    {
        Assert.Null(LeftColumn.Build(Active, "factory4_day", null, LeftColumn.HereKey, Viewership).Expanded); // 다른 맵
        Assert.Null(LeftColumn.Build(Active, "bigmap", null, LeftColumn.AnywhereKey, Viewership).Expanded); // 그 묶음에 없음
        Assert.Null(LeftColumn.Build(Active, "bigmap", null, LeftColumn.ReadyKey, Viewership).Expanded);    // 묶음 없음(goals null)
        Assert.Null(LeftColumn.Build(Active, "bigmap", null, null, null).Expanded);
        Assert.Null(LeftColumn.Build(Active, "bigmap").Expanded);
    }

    // ── 건네주기 줄 ──

    [Fact]
    public void Expanded_quest_shows_its_handover_goals_ready_or_not()
    {
        var goals = new[]
        {
            Goal(Viewership, "c1", ready: true, text: "Hand over 2 flash drives", current: 1, target: 2),
            Goal(Viewership, "c2", ready: false, text: "Hand over the case", target: null),
            Goal("other", "c3", ready: true),
        };

        var d = LeftColumn.Build(Active, "bigmap", goals, LeftColumn.HereKey, Viewership).Expanded;

        var handovers = d.Lines.Where(l => l.Kind == DetailLineKind.Handover).ToList();
        Assert.Equal(new[] { "c1", "c2" }, handovers.Select(h => h.ConditionId));
        Assert.Equal(Viewership, handovers[0].QuestId);
        Assert.Equal("Hand over 2 flash drives", handovers[0].Text);
        Assert.Equal("1 / 2", handovers[0].Progress);
        Assert.True(handovers[0].Ready);
        Assert.True(handovers[0].ShowButton);
        Assert.Null(handovers[1].Progress);
        Assert.False(handovers[1].Ready);
        Assert.Equal(DetailLineKind.Objective, d.Lines[0].Kind); // 대체 경로: 목표 줄 먼저, 그다음 건네주기 줄
        Assert.Single(d.Lines, l => l.Kind == DetailLineKind.Objective);
    }

    [Fact]
    public void No_goals_means_no_handover_lines()
    {
        Assert.Empty(QuestDetail.Build(LeftColumn.HereKey, "q", null, null).Lines);
        Assert.Empty(QuestDetail.Build(LeftColumn.HereKey, "q", null, new HandoverGoal[0]).Lines);
    }

    // ── "제출" 묶음 ──

    [Fact]
    public void Ready_section_lists_pending_quests_not_shown_elsewhere()
    {
        var goals = new[]
        {
            Goal("a", "a1", ready: true, name: "Bravo", text: "Hand over A1"),
            Goal("a", "a2", ready: true, name: "Bravo", text: "Hand over A2"),
            Goal("a", "a3", ready: false, name: "Bravo"),
            Goal("b", "b1", ready: true, name: "Alpha", text: "Hand over the toolset"),
            Goal("b", "b2", ready: false, name: "Alpha", text: "Hand over the rest"),
            Goal("c", "c1", ready: false, name: "Charlie"),         // 낼 수 있는 것은 없지만 남은 목표 → 포함(맨 뒤)
            Goal(Viewership, "v1", ready: true, name: "Viewership"), // 세관 묶음에 이미 있음 → 제외
            Goal(Zeroing, "z1", ready: true, name: "Zeroing"),       // 아무 맵에서나에 이미 있음 → 제외
        };

        var ready = LeftColumn.Build(Active, "bigmap", goals).Ready;

        Assert.Equal(LeftColumn.ReadyKey, ready.Key);
        Assert.Equal("제출", ready.Title);
        Assert.Equal(new[] { "b", "a", "c" }, ready.Rows.Select(r => r.QuestId)); // 낼 수 있는 것이 있는 퀘스트 먼저, 각각 이름순
        Assert.Equal("건네줄 목표 2개", ready.Rows[0].Summary);                  // 남은 목표 b1·b2 → 개수
        Assert.Equal("건네줄 목표 3개", ready.Rows[1].Summary);                  // a1·a2·a3 전부 센다
        Assert.Equal("Hand over", ready.Rows[2].Summary);                        // 남은 목표 1개 → 그 문장
        Assert.Equal("프라퍼", ready.Rows[0].TraderName);
        Assert.Equal("프", ready.Rows[0].TraderInitial);
        Assert.All(ready.Rows, r => { Assert.Null(r.Marker); Assert.Null(r.Progress); Assert.Null(r.Fraction); });
        Assert.Null(ready.EmptyText);
    }

    [Fact]
    public void Ready_section_follows_the_selected_map()
    {
        var goals = new[] { Goal(Viewership, "v1", ready: true, name: "Provide Viewership") };

        Assert.Null(LeftColumn.Build(Active, "bigmap", goals).Ready);
        Assert.Equal(Viewership, LeftColumn.Build(Active, "factory4_day", goals).Ready.Rows.Single().QuestId);
    }

    [Fact]
    public void Ready_section_includes_quests_without_ready_goals_after_ready_ones()
    {
        var goals = new[]
        {
            Goal("a", "a1", ready: false, name: "Alpha", text: "Hand over the case"),
            Goal("z", "z1", ready: true, name: "Zeta"),
        };

        var rows = LeftColumn.Build(Active, "bigmap", goals).Ready.Rows;

        Assert.Equal(new[] { "z", "a" }, rows.Select(r => r.QuestId));
        Assert.Equal("Hand over the case", rows[1].Summary);
    }

    [Fact]
    public void Ready_section_is_absent_without_goals_or_pending_goals()
    {
        Assert.Null(LeftColumn.Build(Active, "bigmap").Ready);
        Assert.Null(LeftColumn.Build(Active, "bigmap", new HandoverGoal[0]).Ready);
        Assert.Null(LeftColumn.Build(Active, "bigmap", new[] { Goal(Viewership, "v1", ready: false) }).Ready); // 이미 보이는 묶음에 있는 퀘스트뿐
    }

    private static FindGoal Find(string questId, string conditionId, string name = "Q", string text = "Find", string trader = Prapor) =>
        new FindGoal(questId, name, trader, conditionId, text);

    [Fact]
    public void Ready_section_lists_quests_with_only_finds_left()
    {
        var goals = new[] { Goal("z", "z1", ready: false, name: "Zeta") };
        var finds = new[]
        {
            Find("a", "a1", name: "Alpha", text: "Find the letter"),
            Find("b", "b1", name: "Bravo"), Find("b", "b2", name: "Bravo"),
            Find(Viewership, "v1", name: "Viewership"), // 세관 묶음에 이미 있음 → 제외
        };

        var rows = LeftColumn.Build(Active, "bigmap", goals, finds: finds).Ready.Rows;

        Assert.Equal(new[] { "a", "b", "z" }, rows.Select(r => r.QuestId)); // 낼 수 있는 게 없으니 전부 이름순
        Assert.Equal("Find the letter", rows[0].Summary);
        Assert.Equal("찾을 목표 2개", rows[1].Summary);
        Assert.All(rows, r => Assert.False(r.HandInReady));
    }

    [Fact]
    public void Finds_do_not_change_the_summary_of_quests_with_handovers()
    {
        var goals = new[] { Goal("a", "a1", ready: true, name: "Alpha", text: "Hand over the drive") };
        var finds = new[] { Find("a", "a2", name: "Alpha") };

        var row = LeftColumn.Build(Active, "bigmap", goals, finds: finds).Ready.Rows.Single();

        Assert.Equal("Hand over the drive", row.Summary);
        Assert.True(row.HandInReady);
    }

    [Fact]
    public void Finds_without_goals_make_no_ready_section()
    {
        // 레이드 중에는 goals 가 null 이라 찾기만 있어도 "제출" 묶음을 만들지 않는다.
        Assert.Null(LeftColumn.Build(Active, "bigmap", null, finds: new[] { Find("a", "a1") }).Ready);
        Assert.Equal("a", LeftColumn.Build(Active, "bigmap", new HandoverGoal[0], finds: new[] { Find("a", "a1") }).Ready.Rows.Single().QuestId);
    }

    [Fact]
    public void Ready_names_sort_in_the_response_language()
    {
        var goals = new[]
        {
            Goal("z", "1", true, name: "Zeta"), Goal("k", "1", true, name: "가방"), Goal("a", "1", true, name: "Alpha"),
        };

        Assert.Equal(new[] { "가방", "Alpha", "Zeta" }, LeftColumn.Build(Active, "bigmap", goals).Ready.Rows.Select(r => r.Name));
    }

    [Fact]
    public void Expanded_ready_quest_has_only_handover_lines()
    {
        var goals = new[] { Goal("a", "a1", ready: true, name: "Bravo"), Goal("a", "a2", ready: false, name: "Bravo") };

        var d = LeftColumn.Build(Active, "bigmap", goals, LeftColumn.ReadyKey, "a").Expanded;

        Assert.Equal(LeftColumn.ReadyKey, d.SectionKey);
        Assert.All(d.Lines, l => Assert.Equal(DetailLineKind.Handover, l.Kind));
        Assert.Equal(new[] { "a1", "a2" }, d.Lines.Select(h => h.ConditionId));
    }

    [Fact]
    public void Existing_sections_are_unchanged_by_goals()
    {
        var goals = new[] { Goal("a", "a1", ready: true) };

        var with = LeftColumn.Build(Active, "bigmap", goals);
        var without = LeftColumn.Build(Active, "bigmap");

        Assert.Equal(without.Here.Rows.Select(r => r.QuestId), with.Here.Rows.Select(r => r.QuestId));
        Assert.Equal(without.Anywhere.Rows.Select(r => r.QuestId), with.Anywhere.Rows.Select(r => r.QuestId));
    }

    // ── 게임 목표 목록(임무 탭과 같은 목록) ──

    private static GameObjective Game(string conditionId, GameObjectiveKind kind, bool done = false, string text = null,
        double current = 0, double? target = 1, string questId = "q", string[] items = null) =>
        new GameObjective(questId, conditionId, text ?? conditionId, current, target, done, kind, items);

    private static BriefingQuest Contract(params string[] conditionIds) => new BriefingQuest
    {
        QuestId = "q", Marker = 3,
        Objectives = conditionIds.Select(id => new BriefingObjective
        {
            ConditionId = id, Text = "contract " + id, Current = 1, Target = 4,
            Locations = { new BriefingLocation { Map = "bigmap", Points = { new BriefingPoint() } } },
        }).ToList(),
    };

    [Fact]
    public void Game_list_merges_in_game_order()
    {
        var objectives = new[]
        {
            Game("find", GameObjectiveKind.Other, text: "Find flash drives", current: 1, target: 2),
            Game("kill", GameObjectiveKind.Raid),
            Game("give", GameObjectiveKind.Handover, text: "Hand over flash drives", current: 0, target: 2),
            Game("skill", GameObjectiveKind.Other, text: "Reach level 5", target: null),
        };
        var goals = new[] { Goal("q", "give", ready: true) };

        var lines = QuestDetail.Build(LeftColumn.HereKey, "q", Contract("kill"), goals, objectives).Lines;

        Assert.Equal(new[] { "find", "kill", "give", "skill" }, lines.Select(l => l.ConditionId));
        Assert.Equal(new[] { DetailLineKind.Plain, DetailLineKind.Objective, DetailLineKind.Handover, DetailLineKind.Plain },
            lines.Select(l => l.Kind));
        // 오늘 목표 줄은 계약 문구·마커·계약 수치 그대로
        Assert.Equal("contract kill", lines[1].Text);
        Assert.Equal("1 / 4", lines[1].Progress);
        Assert.Equal(3, lines[1].Marker);
        Assert.Equal(MarkerPalette.Hex(3), lines[1].MarkerColor);
        // 그 밖의 줄은 게임 문구·게임 수치, 마커 없음
        Assert.Equal("Find flash drives", lines[0].Text);
        Assert.Equal("1 / 2", lines[0].Progress);
        Assert.Null(lines[0].Marker);
        Assert.Null(lines[3].Progress);
        Assert.Equal("Hand over flash drives", lines[2].Text);
        Assert.Equal("0 / 2", lines[2].Progress);
        Assert.True(lines[2].Ready);
        Assert.True(lines[2].ShowButton);
        Assert.Equal("q", lines[2].QuestId);
        Assert.All(new[] { lines[0], lines[1], lines[3] }, l => Assert.False(l.ShowButton));
    }

    [Fact]
    public void Done_objectives_of_any_type_are_done_lines()
    {
        var objectives = new[]
        {
            Game("kill", GameObjectiveKind.Raid, done: true, text: "Kill 5", current: 5, target: 5),
            Game("give", GameObjectiveKind.Handover, done: true, text: "Hand over", current: 2, target: 2),
            Game("hidden", GameObjectiveKind.Other, done: true, text: "Survive", current: 1, target: null),
        };

        var lines = QuestDetail.Build(LeftColumn.HereKey, "q", Contract(), new[] { Goal("q", "give", ready: true) }, objectives).Lines;

        Assert.All(lines, l => Assert.Equal(DetailLineKind.Done, l.Kind));
        Assert.Equal(new[] { "Kill 5", "Hand over", "Survive" }, lines.Select(l => l.Text));
        Assert.Equal(new[] { "5 / 5", "2 / 2", null }, lines.Select(l => l.Progress));
        Assert.All(lines, l => { Assert.Null(l.Marker); Assert.False(l.ShowButton); Assert.False(l.Ready); });
    }

    [Fact]
    public void Unfinished_raid_objective_of_another_map_is_hidden()
    {
        var objectives = new[] { Game("here", GameObjectiveKind.Raid), Game("elsewhere", GameObjectiveKind.Raid) };

        var lines = QuestDetail.Build(LeftColumn.HereKey, "q", Contract("here"), null, objectives).Lines;

        Assert.Equal(new[] { "here" }, lines.Select(l => l.ConditionId));
    }

    [Fact]
    public void Find_sent_by_the_server_is_an_objective_line_with_marker()
    {
        // 정해진 자리에 생기는 퀘스트 아이템 찾기는 서버가 레이드 목표로 보낸다.
        var objectives = new[] { Game("doc", GameObjectiveKind.Other, text: "Find the document"), Game("bolts", GameObjectiveKind.Other) };

        var lines = QuestDetail.Build(LeftColumn.HereKey, "q", Contract("doc"), null, objectives).Lines;

        Assert.Equal(new[] { DetailLineKind.Objective, DetailLineKind.Plain }, lines.Select(l => l.Kind));
        Assert.Equal("contract doc", lines[0].Text);
        Assert.Equal(3, lines[0].Marker);
        Assert.Null(lines[1].Marker);
    }

    [Fact]
    public void Find_line_is_plain_with_item_names()
    {
        var objectives = new[] { Game("find", GameObjectiveKind.Other, text: "Find in raid", target: 3, items: new[] { "Fleece fabric" }) };

        var line = QuestDetail.Build(LeftColumn.HereKey, "q", null, null, objectives).Lines.Single();

        Assert.Equal(DetailLineKind.Plain, line.Kind);
        Assert.Equal("Fleece fabric", line.Items);
        Assert.Null(line.Marker);
        Assert.False(line.ShowButton);
    }

    [Fact]
    public void Handover_in_raid_is_a_plain_line_without_button()
    {
        var objectives = new[] { Game("give", GameObjectiveKind.Handover, text: "Hand over", current: 1, target: 2, items: new[] { "Toolset" }) };

        var line = QuestDetail.Build(LeftColumn.HereKey, "q", null, null, objectives).Lines.Single(); // goals null = 레이드 중

        Assert.Equal(DetailLineKind.Plain, line.Kind);
        Assert.False(line.ShowButton);
        Assert.Equal("1 / 2", line.Progress);
        Assert.Equal("Toolset", line.Items);
    }

    [Fact]
    public void Handover_without_a_matching_goal_has_no_button()
    {
        // 메뉴에서 받지 않는 상인의 퀘스트·게임 읽기 실패(빈 목록)는 goals 에 없다 → 버튼 없이 보통 줄
        var objectives = new[] { Game("give", GameObjectiveKind.Handover) };

        var line = QuestDetail.Build(LeftColumn.HereKey, "q", null, new HandoverGoal[0], objectives).Lines.Single();

        Assert.Equal(DetailLineKind.Plain, line.Kind);
        Assert.False(line.ShowButton);
    }

    [Fact]
    public void Objectives_of_other_quests_are_ignored()
    {
        var objectives = new[] { Game("mine", GameObjectiveKind.Other), Game("theirs", GameObjectiveKind.Other, questId: "other") };

        Assert.Equal(new[] { "mine" }, QuestDetail.Build(LeftColumn.HereKey, "q", null, null, objectives).Lines.Select(l => l.ConditionId));
    }

    [Fact]
    public void Contract_objectives_missing_from_the_game_list_are_appended()
    {
        var objectives = new[] { Game("find", GameObjectiveKind.Other) };

        var lines = QuestDetail.Build(LeftColumn.HereKey, "q", Contract("lost"), null, objectives).Lines;

        Assert.Equal(new[] { "find", "lost" }, lines.Select(l => l.ConditionId));
        Assert.Equal(DetailLineKind.Objective, lines[1].Kind);
        Assert.Equal("contract lost", lines[1].Text);
        Assert.Equal(3, lines[1].Marker);
    }

    [Fact]
    public void Null_objectives_fall_back_to_contract_then_handover_goals()
    {
        var goals = new[] { Goal("q", "give", ready: false, text: "Hand over", items: new[] { "Toolset" }) };

        var lines = QuestDetail.Build(LeftColumn.HereKey, "q", Contract("a", "b"), goals).Lines;

        Assert.Equal(new[] { "a", "b", "give" }, lines.Select(l => l.ConditionId));
        Assert.Equal(new[] { DetailLineKind.Objective, DetailLineKind.Objective, DetailLineKind.Handover }, lines.Select(l => l.Kind));
        Assert.Equal("Toolset", lines[2].Items);
        Assert.True(lines[2].ShowButton);
        Assert.False(lines[2].Ready);
    }

    // 레이드 중 LiveProgress 가 끝났다고 표시한 목표는 Done 줄, 나머지는 실시간 수치.
    [Fact]
    public void Live_done_contract_objective_is_a_done_line_without_marker()
    {
        var quest = Contract("a", "b");
        quest.Objectives[0].Done = true;

        var lines = QuestDetail.Build(LeftColumn.HereKey, "q", quest, null).Lines;

        Assert.Equal(new[] { DetailLineKind.Done, DetailLineKind.Objective }, lines.Select(l => l.Kind));
        Assert.Null(lines[0].Marker);
        Assert.Equal("1 / 4", lines[0].Progress);
        Assert.Equal(3, lines[1].Marker);
    }

    [Fact]
    public void Live_numbers_reach_the_quest_row_and_the_expanded_line()
    {
        var live = LiveProgress.Apply(Active, new Dictionary<string, LiveObjective>
        {
            ["675c1c980fd114390c638b89"] = new LiveObjective("675c1c980fd114390c638b89", 9, false),
        });

        Assert.Equal("9 / 15", LeftColumn.Build(live, "bigmap").Here.Rows.Single(r => r.QuestId == Viewership).Progress);
        Assert.Equal("9 / 15", LeftColumn.Build(live, "bigmap", null, LeftColumn.HereKey, Viewership).Expanded.Lines.Single().Progress);
        Assert.Equal("6 / 15", LeftColumn.Build(Active, "bigmap").Here.Rows.Single(r => r.QuestId == Viewership).Progress); // 원래 응답은 그대로
    }

    // 읽는 도중 예외를 던지는 목록 — 게임 쪽 값이 이상해 합치기가 실패하는 경우를 흉내 낸다.
    private sealed class ThrowingList : IReadOnlyList<GameObjective>
    {
        public GameObjective this[int index] => throw new System.InvalidOperationException("broken");
        public int Count => throw new System.InvalidOperationException("broken");
        public IEnumerator<GameObjective> GetEnumerator() => throw new System.InvalidOperationException("broken");
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }

    [Fact]
    public void Game_merge_failure_falls_back_instead_of_dropping_the_block()
    {
        var goals = new[] { Goal("q", "give", ready: true) };

        var d = QuestDetail.Build(LeftColumn.HereKey, "q", Contract("a"), goals, new ThrowingList());

        Assert.Equal(new[] { "a", "give" }, d.Lines.Select(l => l.ConditionId));
        Assert.Equal(new[] { DetailLineKind.Objective, DetailLineKind.Handover }, d.Lines.Select(l => l.Kind));
    }

    [Fact]
    public void Left_column_passes_game_objectives_to_the_expanded_quest()
    {
        var objectives = new[]
        {
            Game("675c1c980fd114390c638b89", GameObjectiveKind.Raid, questId: Viewership),
            Game("find", GameObjectiveKind.Other, done: true, text: "Found it", questId: Viewership),
        };

        var d = LeftColumn.Build(Active, "bigmap", null, LeftColumn.HereKey, Viewership, objectives).Expanded;

        Assert.Equal(new[] { DetailLineKind.Objective, DetailLineKind.Done }, d.Lines.Select(l => l.Kind));
        Assert.Equal("Eliminate any target inside the industrial plant on Customs ×15", d.Lines[0].Text);
        Assert.Equal(1, d.Lines[0].Marker);
    }

    [Fact]
    public void Ready_section_expand_shows_only_game_lines()
    {
        var goals = new[] { Goal("a", "a1", ready: true, name: "Bravo") };
        var objectives = new[]
        {
            Game("raid", GameObjectiveKind.Raid, questId: "a"),
            Game("a1", GameObjectiveKind.Handover, questId: "a"),
        };

        var d = LeftColumn.Build(Active, "bigmap", goals, LeftColumn.ReadyKey, "a", objectives).Expanded;

        Assert.Equal(new[] { "a1" }, d.Lines.Select(l => l.ConditionId));
        Assert.True(d.Lines[0].ShowButton);
    }

    // ── 손 아이콘(모든 묶음의 줄에 붙는다) ──

    [Fact]
    public void Ready_rows_with_a_ready_goal_show_the_hand_icon()
    {
        var goals = new[]
        {
            Goal("a", "a1", ready: false, name: "Alpha"), Goal("a", "a2", ready: true, name: "Alpha"), // 하나라도 지금 가능 → 손
            Goal("b", "b1", ready: false, name: "Bravo"),                                               // 지금 가능한 것 없음
        };

        var rows = LeftColumn.Build(Active, "bigmap", goals).Ready.Rows;

        Assert.Equal(new[] { "a", "b" }, rows.Select(r => r.QuestId));
        Assert.True(rows[0].HandInReady);
        Assert.False(rows[1].HandInReady);
    }

    [Fact]
    public void Map_and_anywhere_rows_with_a_ready_goal_show_the_hand_icon()
    {
        var goals = new[]
        {
            Goal(Viewership, "v1", ready: true), Goal(Zeroing, "z1", ready: true), Goal("r", "r1", ready: true),
            Goal(HobbyClub, "h1", ready: false),                    // 남은 제출 목표는 있지만 지금은 못 냄 → 손 없음
        };
        var model = LeftColumn.Build(Active, "bigmap", goals, turnIns: new[] { new TurnInQuest("t", "Turn", Prapor) });

        Assert.True(model.Here.Rows.Single(r => r.QuestId == Viewership).HandInReady);
        Assert.True(model.Anywhere.Rows.Single(r => r.QuestId == Zeroing).HandInReady);
        Assert.False(model.Here.Rows.Single(r => r.QuestId == HobbyClub).HandInReady);
        Assert.False(model.Anywhere.Rows.Single(r => r.QuestId == HobbyClub).HandInReady);
        Assert.All(model.Here.Rows.Where(r => r.QuestId != Viewership), r => Assert.False(r.HandInReady));
        Assert.All(model.Anywhere.Rows.Where(r => r.QuestId != Zeroing), r => Assert.False(r.HandInReady));
        Assert.All(model.TurnIn.Rows, r => Assert.False(r.HandInReady)); // 완료 가능 퀘스트는 제출 목표가 없다
        Assert.True(model.Ready.Rows.Single().HandInReady);
    }

    [Fact]
    public void No_goals_means_no_hand_icon_anywhere()
    {
        var model = LeftColumn.Build(Active, "bigmap"); // goals null = 레이드 중

        Assert.All(model.Here.Rows.Concat(model.Anywhere.Rows), r => Assert.False(r.HandInReady));
    }

    // ── "완료 가능" 묶음(상인에게 보고만 하면 끝나는 퀘스트) ──

    private static TurnInQuest TurnIn(string questId, string name, string trader = Prapor) => new TurnInQuest(questId, name, trader);

    [Fact]
    public void Turn_in_section_lists_quests_by_name_with_the_turn_in_summary()
    {
        var turnIns = new[] { TurnIn("z", "Zeta"), TurnIn("k", "가방"), TurnIn("a", "Alpha"), TurnIn("b", "Alpha") };

        var model = LeftColumn.Build(Active, "bigmap", turnIns: turnIns);

        var section = model.TurnIn;
        Assert.Equal(LeftColumn.TurnInKey, section.Key);
        Assert.Equal("완료 가능", section.Title);
        Assert.Equal(new[] { "k", "a", "b", "z" }, section.Rows.Select(r => r.QuestId)); // 이름순(응답 언어), 같은 이름은 id 순
        Assert.Equal(4, section.Count);
        Assert.Null(section.EmptyText);
        Assert.All(section.Rows, r =>
        {
            Assert.Equal("상인에게 보고하면 완료", r.Summary);
            Assert.Null(r.Marker);
            Assert.Null(r.MarkerColor);
            Assert.Null(r.Progress);
            Assert.Null(r.Fraction);
        });
        Assert.Equal("프라퍼", section.Rows[0].TraderName);
        Assert.Equal("프", section.Rows[0].TraderInitial);
        Assert.Equal("가방", section.Rows[0].Name);
    }

    [Fact]
    public void Turn_in_section_is_absent_when_null_or_empty()
    {
        Assert.Null(LeftColumn.Build(Active, "bigmap").TurnIn);
        Assert.Null(LeftColumn.Build(Active, "bigmap", turnIns: new TurnInQuest[0]).TurnIn);
    }

    [Fact]
    public void Turn_in_quests_appear_in_no_other_section()
    {
        // 건네줄 목표가 남은 진행 중 퀘스트는 완료 가능일 수 없지만(게임 상태가 다르다), 겹쳐 들어와도 한 곳에만 보인다.
        var goals = new[] { Goal("t", "t1", ready: true, name: "Turn"), Goal("r", "r1", ready: true, name: "Ready") };
        var turnIns = new[] { TurnIn("t", "Turn"), TurnIn(Viewership, "Provide Viewership"), TurnIn(Zeroing, "Zeroing") };

        var model = LeftColumn.Build(Active, "bigmap", goals, turnIns: turnIns);

        Assert.Equal(new[] { "r" }, model.Ready.Rows.Select(r => r.QuestId));
        Assert.DoesNotContain(model.Here.Rows, r => r.QuestId == Viewership);
        Assert.DoesNotContain(model.Anywhere.Rows, r => r.QuestId == Zeroing);
        Assert.Equal(3, model.TurnIn.Count);
    }

    [Fact]
    public void Other_sections_are_unchanged_without_overlap()
    {
        var with = LeftColumn.Build(Active, "bigmap", turnIns: new[] { TurnIn("x", "X") });
        var without = LeftColumn.Build(Active, "bigmap");

        Assert.Equal(without.Here.Rows.Select(r => r.QuestId), with.Here.Rows.Select(r => r.QuestId));
        Assert.Equal(without.Anywhere.Rows.Select(r => r.QuestId), with.Anywhere.Rows.Select(r => r.QuestId));
    }

    [Fact]
    public void Expanded_turn_in_quest_shows_game_objectives_and_can_turn_in()
    {
        var turnIns = new[] { TurnIn("t", "Turn") };
        var objectives = new[]
        {
            Game("kill", GameObjectiveKind.Raid, done: true, text: "Kill 5", current: 5, target: 5, questId: "t"),
            Game("give", GameObjectiveKind.Handover, done: true, text: "Hand over", current: 2, target: 2, questId: "t"),
        };

        var d = LeftColumn.Build(Active, "bigmap", null, LeftColumn.TurnInKey, "t", objectives, turnIns).Expanded;

        Assert.Equal(LeftColumn.TurnInKey, d.SectionKey);
        Assert.Equal("t", d.QuestId);
        Assert.True(d.CanTurnIn);
        Assert.Equal(new[] { "Kill 5", "Hand over" }, d.Lines.Select(l => l.Text));
        Assert.All(d.Lines, l => Assert.Equal(DetailLineKind.Done, l.Kind));
    }

    [Fact]
    public void Expanded_turn_in_quest_without_game_objectives_still_can_turn_in()
    {
        var d = LeftColumn.Build(Active, "bigmap", null, LeftColumn.TurnInKey, "t", null, new[] { TurnIn("t", "Turn") }).Expanded;

        Assert.True(d.CanTurnIn);
        Assert.Empty(d.Lines);
    }

    [Fact]
    public void Only_the_turn_in_section_can_turn_in()
    {
        var goals = new[] { Goal("a", "a1", ready: true, name: "Bravo") };
        var turnIns = new[] { TurnIn("t", "Turn") };

        Assert.False(LeftColumn.Build(Active, "bigmap", goals, LeftColumn.HereKey, Viewership, null, turnIns).Expanded.CanTurnIn);
        Assert.False(LeftColumn.Build(Active, "bigmap", goals, LeftColumn.AnywhereKey, Zeroing, null, turnIns).Expanded.CanTurnIn);
        Assert.False(LeftColumn.Build(Active, "bigmap", goals, LeftColumn.ReadyKey, "a", null, turnIns).Expanded.CanTurnIn);
        Assert.Null(LeftColumn.Build(Active, "bigmap", goals, LeftColumn.TurnInKey, "a", null, turnIns).Expanded); // 그 묶음에 없음
        Assert.Null(LeftColumn.Build(Active, "bigmap", goals, LeftColumn.TurnInKey, "t").Expanded);                // 묶음 없음(turnIns null)
    }

    // ── 아이템 이름 줄 ──

    [Fact]
    public void Item_names_show_at_most_three_then_the_rest_count()
    {
        Assert.Equal("A, B, C 외 2개", QuestDetail.ItemsText(new[] { "A", "B", "A", "C", "D", "E" }, "Hand over"));
        Assert.Equal("A, B, C", QuestDetail.ItemsText(new[] { "A", "B", "C" }, "Hand over"));
        // 영어 문구는 TextsLanguageTests(정적 언어 상태라 병렬 금지 컬렉션)에서 본다.
    }

    [Fact]
    public void No_items_line_when_empty_or_already_in_the_text()
    {
        Assert.Null(QuestDetail.ItemsText(new string[0], "Hand over"));
        Assert.Null(QuestDetail.ItemsText(new[] { "", null }, "Hand over"));
        Assert.Null(QuestDetail.ItemsText(new[] { "Fleece fabric" }, "Hand over 2 Fleece fabric"));
        Assert.Equal("Fleece fabric, Ripstop", QuestDetail.ItemsText(new[] { "Fleece fabric", "Ripstop" }, "Hand over Fleece fabric"));
        Assert.Equal("Toolset", QuestDetail.ItemsText(new[] { "Toolset" }, null));
    }

    [Fact]
    public void Item_names_are_never_null()
    {
        Assert.Empty(new HandoverGoal("q", "Q", "t", "c", "x", 0, 1, true).ItemNames);
        Assert.Empty(new GameObjective("q", "c", "x", 0, 1, false, GameObjectiveKind.Other, null).ItemNames);
    }

    [Theory]
    [InlineData("kr", "가", "A", -1)]
    [InlineData("en", "a", "B", -1)]
    [InlineData("ge", "b", "a", 1)]
    public void Name_order_uses_the_language_culture(string lang, string x, string y, int sign)
    {
        Assert.Equal(sign, System.Math.Sign(LeftColumn.NameOrder(lang).Compare(x, y)));
    }
}
