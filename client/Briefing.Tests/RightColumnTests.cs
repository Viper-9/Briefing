using System;
using System.Linq;
using Briefing.Core.Contract;
using Briefing.Core.Screen;

namespace Briefing.Tests;

// 준비물, 수집, 필터, 탭 숫자, 말풍선. Texts 를 읽으므로 Texts 컬렉션에 둔다.
[Collection("Texts")]
public class RightColumnTests
{
    private const string HobbyClub = "684009026ceedc792c09b2a7";
    private const string DripOut = "66151401efb0539ae10875ae";
    private const string DuctTapeQuest = "668aacd1dee3de3ce276fdef"; // 계약 퀘스트 목록에 없는 출처
    private static readonly BriefingResponse Active = FixtureFiles.Load(FixtureFiles.Active, "kr");

    private static RightColumnModel Build(string mapKey, string filter = null, Func<string, string> game = null) =>
        RightColumn.Build(Active, Active.Maps.Single(m => m.Key == mapKey), filter, null, game);

    [Fact]
    public void Bring_has_this_map_section_with_short_note()
    {
        var m = Build("bigmap");

        var section = m.Bring.Single(); // anywhere.bring 은 비어 묶음째 빠진다
        Assert.Equal("레이드에 가져갈 퀘스트 아이템", section.Title);
        Assert.Equal("부족 1", section.Note);
        Assert.Equal(new[] { "MS2000 마커", "TheAKGuy AK-50 .50 BMG sniper rifle" }, section.Cells.Select(c => c.Name)); // 뒤 공백 다듬음
        Assert.Equal(new[] { "1/1", "0/1" }, section.Cells.Select(c => c.Count));
        Assert.Equal(new[] { false, true }, section.Cells.Select(c => c.Short));
        Assert.Equal("67d0576f29f580ebc10efd08", section.Cells[1].Tpl);
        Assert.Null(m.BringEmpty);
    }

    [Fact]
    public void Bring_without_rows_says_nothing_to_bring()
    {
        var m = Build("labyrinth");

        Assert.Empty(m.Bring);
        Assert.Equal("준비물 없음", m.BringEmpty);
    }

    [Fact]
    public void Find_has_here_then_any_map()
    {
        var m = Build("factory4_day");

        Assert.Equal(new[] { "공장 획득처", "모든 맵" }, m.Find.Select(s => s.Title));
        Assert.Equal("Blood sample", m.Find[0].Cells.Single().Name);
        var wartech = m.Find[1].Cells.Single();
        Assert.Equal("WARTECH TV-115 plate carrier (Olive Drab)", wartech.Name);
        Assert.Equal(4, wartech.Alternatives);
        Assert.True(wartech.Fir);
        Assert.Equal("3/50", wartech.Count);
        Assert.Equal("또는: WARTECH Berkut BB-102 배낭 (A-TACS FG), WARTECH TV-110 플레이트 캐리어 (Coyote), " +
            "WARTECH MK3 TV-104 체스트 리그 (MultiCam), WARTECH TV-109 + TV-106 체스트 리그 (A-TACS FG)", wartech.TipAlternatives);
        Assert.Equal("보유 3 / 필요 50 · FIR · 부족 47", wartech.TipCount);
        Assert.Equal("Drip-Out - Part 1", wartech.TipQuests);
        Assert.Null(m.FindEmpty);
    }

    [Fact]
    public void Empty_here_section_keeps_its_title()
    {
        var m = Build("bigmap");

        Assert.Equal(new[] { "세관 획득처", "모든 맵" }, m.Find.Select(s => s.Title));
        Assert.Empty(m.Find[0].Cells);
        Assert.Equal("없음", m.Find[0].EmptyText);
        Assert.Equal(2, m.Find[1].Cells.Count);
    }

    [Fact]
    public void Elsewhere_rows_are_not_drawn_and_empty_any_map_is_omitted()
    {
        var r = BriefingParser.Parse(@"{""schemaVersion"":1,""maps"":[{""key"":""a"",""name"":""A"",
            ""find"":[{""place"":""elsewhere"",""items"":[{""tpl"":""t"",""name"":""x""}],""need"":1,""missing"":1}]}],""anywhere"":{}}").Response;

        var m = RightColumn.Build(r, r.Maps[0], null, null, null);

        Assert.Equal("A 획득처", m.Find.Single().Title);
        Assert.Empty(m.Find.Single().Cells);
        Assert.Equal(0, m.Tabs[1].Count);
    }

    [Fact]
    public void Tabs_count_cells()
    {
        var m = Build("factory4_day");

        Assert.Equal(new[] { RightTabKey.Bring, RightTabKey.Find, RightTabKey.Gear }, m.Tabs.Select(t => t.Key));
        Assert.Equal(new[] { "준비물", "수집", "장비" }, m.Tabs.Select(t => t.Label));
        Assert.Equal(1, m.Tabs[0].Count);
        Assert.Equal(2, m.Tabs[1].Count);
        Assert.Null(m.FilterText);
    }

    [Fact]
    public void Filter_keeps_rows_of_the_selected_quest()
    {
        var m = Build("bigmap", HobbyClub);

        Assert.Equal("TheAKGuy AK-50 .50 BMG sniper rifle", m.Bring.Single().Cells.Single().Name);
        Assert.Equal("부족 1", m.Bring.Single().Note);
        Assert.Empty(m.Find);
        Assert.Equal("이 퀘스트는 찾을 것이 없습니다", m.FindEmpty);
        Assert.Equal(new[] { 1, 0 }, m.Tabs.Take(2).Select(t => t.Count));
        Assert.Equal("선택한 퀘스트만: Hobby Club", m.FilterText);
    }

    [Fact]
    public void Filter_without_rows_says_so_per_tab()
    {
        var m = Build("factory4_day", DripOut);

        Assert.Empty(m.Bring);
        Assert.Equal("이 퀘스트는 챙길 것이 없습니다", m.BringEmpty);
        Assert.Equal("모든 맵", m.Find.Single(s => s.Cells.Count > 0).Title);
        Assert.Equal("없음", m.Find[0].EmptyText); // 이 맵 묶음은 제목 + 없음
        Assert.Equal("선택한 퀘스트만: Drip-Out - Part 1", m.FilterText);
    }

    [Fact]
    public void Filter_text_uses_the_given_name()
    {
        var m = RightColumn.Build(Active, Active.Maps[0], HobbyClub, "취미 모임", null);

        Assert.Equal("선택한 퀘스트만: 취미 모임", m.FilterText);
    }

    [Fact]
    public void Quest_names_fall_back_to_game_locale_then_id()
    {
        string Tip(Func<string, string> game) =>
            Build("bigmap", null, game).Find[1].Cells.Single(c => c.Name.StartsWith("Duct")).TipQuests;

        Assert.Equal("Gunsmith - Old Friend's Request", Tip(id => id == DuctTapeQuest ? "Gunsmith - Old Friend's Request" : null));
        Assert.Equal(DuctTapeQuest, Tip(null));
        Assert.Equal(DuctTapeQuest, Tip(_ => " "));
        Assert.Equal(DuctTapeQuest, Tip(_ => throw new InvalidOperationException()));
        Assert.Equal("Hobby Club", Build("bigmap").Bring.Single().Cells[1].TipQuests); // 계약에 있으면 그 이름
    }

    [Fact]
    public void No_map_uses_anywhere_only()
    {
        var r = BriefingParser.Parse(@"{""schemaVersion"":1,""maps"":[],""anywhere"":{
            ""bring"":[{""items"":[{""tpl"":""t"",""name"":""Thing""}],""need"":2,""have"":2,""sources"":[{""questId"":""q"",""count"":2}]}]}}").Response;

        var m = RightColumn.Build(r, null, null, null, null);

        Assert.Equal("아무 맵에서나", m.Bring.Single().Title);
        Assert.Null(m.Bring.Single().Note);
        Assert.False(m.Bring.Single().Cells.Single().Short);
        Assert.Equal("보유 2 / 필요 2", m.Bring.Single().Cells.Single().TipCount);
        Assert.Empty(m.Find);
        Assert.Equal("없음", m.FindEmpty);
    }

    [Fact]
    public void Null_rows_and_null_sources_are_skipped_not_thrown()
    {
        var r = BriefingParser.Parse(@"{""schemaVersion"":1,""maps"":[{""key"":""a"",""name"":""A"",
            ""bring"":[null,{""items"":[{""tpl"":""t"",""name"":""B""}],""need"":1,""have"":1,""sources"":[null,{""questId"":""q"",""count"":1}]}],
            ""find"":[null,{""place"":""here"",""items"":[{""tpl"":""f"",""name"":""F""}],""need"":1,""missing"":1,""sources"":[null,{""questId"":""q"",""count"":1}]}]}],
            ""anywhere"":{""bring"":[null]}}").Response;

        var plain = RightColumn.Build(r, r.Maps[0], null, null, null);
        var filtered = RightColumn.Build(r, r.Maps[0], "q", null, null);
        var other = RightColumn.Build(r, r.Maps[0], "zzz", null, null);

        Assert.Equal(1, plain.Tabs[0].Count);
        Assert.Equal(1, plain.Tabs[1].Count);
        Assert.Equal(1, filtered.Tabs[0].Count);
        Assert.Equal(1, filtered.Tabs[1].Count);
        Assert.Equal(0, other.Tabs[0].Count);
        Assert.Equal(0, other.Tabs[1].Count);
    }

    [Fact]
    public void Filter_follows_the_expanded_row_until_turned_off()
    {
        var here = LeftColumn.Build(Active, "bigmap", null, LeftColumn.HereKey, HobbyClub);
        Assert.Equal((HobbyClub, "Hobby Club"), RightColumn.Filter(here, false));
        Assert.Equal(((string)null, (string)null), RightColumn.Filter(here, true)); // × = 필터만 끔

        var anywhere = LeftColumn.Build(Active, "factory4_day", null, LeftColumn.AnywhereKey, DripOut);
        Assert.Equal((DripOut, "Drip-Out - Part 1"), RightColumn.Filter(anywhere, false));

        // 펼친 줄이 지금 보이지 않으면(다른 맵의 맵 묶음 줄) 필터 없음
        var hidden = LeftColumn.Build(Active, "factory4_day", null, LeftColumn.HereKey, HobbyClub);
        Assert.Equal(((string)null, (string)null), RightColumn.Filter(hidden, false));
        Assert.Equal(((string)null, (string)null), RightColumn.Filter(LeftColumn.Build(Active, "bigmap"), false));
        Assert.Equal(((string)null, (string)null), RightColumn.Filter(null, false));
    }

    // "완료 가능"과 "제출" 묶음의 펼친 줄도 필터가 된다.
    [Fact]
    public void Filter_follows_turn_in_and_hand_in_rows()
    {
        const string prapor = "54cb50c76803fa8b248b4571";
        var turnIn = LeftColumn.Build(Active, "bigmap", null, LeftColumn.TurnInKey, "done-quest",
            turnIns: new[] { new TurnInQuest("done-quest", "Done Quest", prapor) });
        Assert.Equal(("done-quest", "Done Quest"), RightColumn.Filter(turnIn, false));

        var goals = new[] { new HandoverGoal("give-quest", "Give Quest", prapor, "c1", "Hand over", 0, 1, true, null) };
        var handIn = LeftColumn.Build(Active, "bigmap", goals, LeftColumn.ReadyKey, "give-quest");
        Assert.Equal(("give-quest", "Give Quest"), RightColumn.Filter(handIn, false));
    }

    [Fact]
    public void Item_kind_texts()
    {
        try
        {
            Texts.UseLanguage("kr");
            Assert.Equal("퀘스트 전용 아이템 · 퀘스트를 받으면 레이드에 생김", Texts.QuestItemKind);
            Assert.Equal("일반 아이템 · 레이드에서 언제든 파밍", Texts.LootItemKind);
            Texts.UseLanguage("en");
            Assert.Equal("Quest item · spawns only while the quest is active", Texts.QuestItemKind);
            Assert.Equal("Regular item · lootable in any raid", Texts.LootItemKind);
        }
        finally { Texts.UseLanguage("kr"); }
    }

    [Fact]
    public void English_texts()
    {
        try
        {
            Texts.UseLanguage("en");
            var en = FixtureFiles.Load(FixtureFiles.Active, "en");
            var m = RightColumn.Build(en, en.Maps[1], null, null, null);

            Assert.Equal(new[] { "Supplies", "Collect", "Gear" }, m.Tabs.Select(t => t.Label));
            Assert.Equal("Quest items to bring", m.Bring.Single().Title);
            Assert.Equal("1 short", m.Bring.Single().Note);
            Assert.Equal(new[] { "Factory sources", "Any map" }, m.Find.Select(s => s.Title));
            Assert.Equal("Have 3 / Need 50 · FIR · Short 47", m.Find[1].Cells.Single().TipCount);
            Assert.StartsWith("Or: WARTECH Berkut BB-102 backpack", m.Find[1].Cells.Single().TipAlternatives);
            Assert.Equal("Selected quest only: Q", RightColumn.Build(en, en.Maps[1], "x", "Q", null).FilterText);
            Assert.Equal("Show all ×", Texts.ShowAll);
            Assert.Equal("No supplies", RightColumn.Build(en, en.Maps[2], null, null, null).BringEmpty);
        }
        finally { Texts.UseLanguage("kr"); }
    }
}
