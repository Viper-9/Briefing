using System.Collections.Generic;
using System.Linq;
using Briefing.Core.Contract;
using Briefing.Core.Screen;

namespace Briefing.Tests;

// 장비 탭: 맵과 anywhere 합치기, 줄 중복 제거, 덩어리 판정, 외 N종, 특수 조건 문구, 필터와 빈 문구, 탭 숫자.
[Collection("Texts")]
public class GearTabTests
{
    private const string Sniper = "5c0bde0986f77479cf22c2f8";  // 천국에서 태어난 저격수(세관 맵 gear)
    private const string Silent = "5c0bc91486f7746ab41857a2";  // 소음기 사용법(anywhere gear)
    private const string GoodTimes = "666314bafd5ca9577902e03a"; // 좋았던 시절 - 파트 2(anywhere gear, TT-33 하나)
    private const string Seizing = "675c1d6d59b0575973008fc7"; // Seizing the Initiative(세관 special)
    private const string GoldenTt = "5b3b713c5acfc4330140bd8d";
    private static readonly BriefingResponse Active = FixtureFiles.Load(FixtureFiles.Active, "kr");

    private static GearSnapshot Equipped(params string[] weapons) => new GearSnapshot
    {
        EquipmentRead = true,
        StashRead = true,
        Weapons = weapons.Select(w => new GearWeapon(w, null, null)).ToList(),
    };

    private static GearTabModel Tab(string mapKey, string filter = null, GearSnapshot gear = null) =>
        RightColumn.Build(Active, Active.Maps.Single(m => m.Key == mapKey), filter, null, null, gear).Gear;

    [Fact]
    public void Map_gear_comes_before_anywhere_gear()
    {
        var tab = Tab("bigmap", gear: Equipped());

        Assert.Equal(new[] { Sniper, Silent, GoodTimes }, tab.Gear.Select(b => b.QuestId));
        Assert.Equal(new[] { "천국에서 태어난 저격수", "소음기 사용법", "좋았던 시절 - 파트 2" }, tab.Gear.Select(b => b.Title));
        Assert.Equal(new[] { "무기", "필수 부착물", "무기" }, tab.Gear[1].Rules.Select(r => r.Label)); // 이름 순서가 다른 두 무기 줄은 따로
        Assert.Null(tab.LoadoutError);
        Assert.Null(tab.Empty);
        Assert.Equal(5, tab.Count); // 장비 3 + 특수 2
    }

    [Fact]
    public void Lines_carry_marks_more_text_and_keys()
    {
        var tab = Tab("bigmap", gear: Equipped(GoldenTt));

        var sniper = tab.Gear[0].Rules.Single();
        Assert.Equal(Sniper + "/0", sniper.Key);
        Assert.Equal(GearMark.Missing, sniper.Mark);
        Assert.Equal("Accuracy International AXMC .338 LM bolt-action sniper rifle", sniper.Lead);
        Assert.Equal("외 20종", sniper.More);
        Assert.Equal(21, sniper.Alternatives.Count);
        Assert.False(sniper.Warn);
        Assert.Equal(GearMark.Missing, tab.Gear[0].Verdict);

        var tt = tab.Gear[2].Rules.Single();
        Assert.Equal(GearMark.Equipped, tt.Mark);
        Assert.Equal("TT-33 7.62x25 TT 권총 (황금색)", tt.Lead);
        Assert.Equal("장착 중", tt.Tag);
        Assert.Null(tt.More); // 대안 하나뿐
        Assert.Equal(GearMark.Equipped, tab.Gear[2].Verdict);
    }

    [Fact]
    public void Same_rows_from_map_and_anywhere_appear_once()
    {
        var r = BriefingParser.Parse(@"{""schemaVersion"":1,""maps"":[{""key"":""a"",""name"":""A"",
            ""quests"":[{""questId"":""q"",""name"":""Quest""}],
            ""gear"":[{""questId"":""q"",""rows"":[{""label"":""weapon"",""names"":[""X"",""Y""],""tpls"":[[""x""],[""y""]]}]}],
            ""special"":[{""questId"":""q"",""rows"":[{""kind"":""oneRaid"",""exitStatuses"":[]}]}]}],
            ""anywhere"":{
            ""gear"":[{""questId"":""q"",""rows"":[{""label"":""weapon"",""names"":[""X"",""Y""],""tpls"":[[""x""],[""y""]]},
                                                 {""label"":""forbidden"",""names"":[""V""],""tpls"":[[""v""]]}]}],
            ""special"":[{""questId"":""q"",""rows"":[{""kind"":""oneRaid"",""exitStatuses"":[]},
                                                    {""kind"":""exit"",""exitStatuses"":[""Survived"",""Weird""],""exitName"":""Klimov Street""}]}]}}").Response;

        var tab = RightColumn.Build(r, r.Maps[0], null, null, null).Gear;

        var block = tab.Gear.Single();
        Assert.Equal(new[] { "무기", "착용 금지" }, block.Rules.Select(l => l.Label));
        Assert.True(block.Rules[1].Warn);
        Assert.Equal("목록 1종", block.Rules[1].More);
        Assert.Equal("외 1종", block.Rules[0].More);
        Assert.Equal(GearMark.Unknown, block.Verdict); // 장착을 못 읽음(gear null)
        Assert.Equal("장착 정보를 읽지 못했습니다", tab.LoadoutError);
        var special = tab.Special.Single();
        Assert.Equal("Quest", special.Title);
        Assert.Equal(new[] { "레이드", "탈출" }, special.Specials.Select(s => s.Label));
        Assert.Equal(new[] { "한 레이드 안에 완료", "생존 · Weird · Klimov Street 탈출구" }, special.Specials.Select(s => s.Text));
    }

    [Fact]
    public void Special_lines_follow_web_exit_text()
    {
        var tab = Tab("bigmap");

        Assert.Equal(new[] { "안내자", "Seizing the Initiative" }, tab.Special.Select(b => b.Title));
        Assert.Equal("생존 · 환승", tab.Special[0].Specials.Single().Text);
        Assert.Equal(new[] { "한 레이드 안에 완료", "환승" }, tab.Special[1].Specials.Select(s => s.Text));
        Assert.Null(GearTab.SpecialLine(new BriefingSpecialRow { Kind = "exit" })); // 상태·이름 둘 다 없으면 줄 없음
        Assert.Null(GearTab.SpecialLine(new BriefingSpecialRow { Kind = "other" }));
        Assert.Equal("Klimov Street 탈출구", GearTab.SpecialLine(new BriefingSpecialRow { Kind = "exit", ExitName = "Klimov Street" }).Text);
    }

    [Fact]
    public void Filter_keeps_blocks_of_the_selected_quest()
    {
        var gear = Tab("bigmap", GoodTimes);
        Assert.Equal(GoodTimes, gear.Gear.Single().QuestId);
        Assert.Empty(gear.Special);
        Assert.Equal(1, gear.Count);

        var special = Tab("bigmap", Seizing);
        Assert.Empty(special.Gear);
        Assert.Equal(Seizing, special.Special.Single().QuestId);

        var none = Tab("bigmap", "684009026ceedc792c09b2a7"); // Hobby Club — 장비 조건 없음
        Assert.Equal(0, none.Count);
        Assert.Equal("이 퀘스트는 장비 조건이 없습니다", none.Empty);
    }

    [Fact]
    public void Map_without_gear_still_shows_anywhere_gear_and_counts_tabs()
    {
        var m = RightColumn.Build(Active, Active.Maps[1], null, null, null, Equipped());

        Assert.Equal(new[] { Silent, GoodTimes }, m.Gear.Gear.Select(b => b.QuestId));
        Assert.Empty(m.Gear.Special);
        Assert.Equal(2, m.Tabs[2].Count);

        var empty = FixtureFiles.Load(FixtureFiles.Empty, "kr");
        var none = RightColumn.Build(empty, empty.Maps[0], null, null, null, Equipped());
        Assert.Equal("장비 조건 없음", none.Gear.Empty);
        Assert.Equal(0, none.Tabs[2].Count);
    }

    [Fact]
    public void English_gear_texts()
    {
        try
        {
            Texts.UseLanguage("en");
            var en = FixtureFiles.Load(FixtureFiles.Active, "en");
            var tab = RightColumn.Build(en, en.Maps[0], null, null, null, Equipped(GoldenTt)).Gear;

            Assert.Equal("A Shooter Born in Heaven", tab.Gear[0].Title);
            Assert.Equal("Weapon", tab.Gear[0].Rules[0].Label);
            Assert.Equal("+20 more", tab.Gear[0].Rules[0].More);
            Assert.Equal("equipped", tab.Gear[2].Rules[0].Tag);
            Assert.Equal("Weapon mods", tab.Gear[1].Rules[1].Label);
            Assert.Equal("Survived · Transit", tab.Special[0].Specials[0].Text);
            Assert.Equal("Exit", tab.Special[0].Specials[0].Label);
            Assert.Equal("Complete in a single raid", tab.Special[1].Specials[0].Text);
            Assert.Equal("via Klimov Street", Texts.ExitVia("Klimov Street"));
            Assert.Equal("Don't wear", Texts.GearLabel("forbidden"));
            Assert.Equal("List of 169", Texts.ListCount(169));
            Assert.Equal("Wearing: X", Texts.WearingNow("X"));
            Assert.Equal("Any caliber", GearCheck.DisplayName("caliber", "all"));
            Assert.Equal("MIA", Texts.ExitStatus("MissingInAction"));
            Assert.Equal("Couldn't read your equipment", RightColumn.Build(en, en.Maps[0], null, null, null).Gear.LoadoutError);
        }
        finally { Texts.UseLanguage("kr"); }
    }

    [Fact]
    public void Legend_lists_marks_only_when_rule_blocks_and_equipment_are_read()
    {
        Assert.Empty(Tab("bigmap").Legend); // gear null
        Assert.Equal(new[] { GearMark.Equipped, GearMark.Stash, GearMark.Missing }, Tab("bigmap", gear: Equipped()).Legend);

        var noStash = new GearSnapshot { EquipmentRead = true, StashRead = false, Weapons = new List<GearWeapon>() };
        Assert.Equal(new[] { GearMark.Equipped, GearMark.Missing }, Tab("bigmap", gear: noStash).Legend);

        Assert.Empty(Tab("bigmap", Seizing, Equipped()).Legend); // 규칙 덩어리가 없는 필터

        Assert.Equal(new[] { "장착", "보관함", "없음", "" },
            new[] { Texts.LegendMark(GearMark.Equipped), Texts.LegendMark(GearMark.Stash), Texts.LegendMark(GearMark.Missing), Texts.LegendMark(GearMark.Unknown) });
        try
        {
            Texts.UseLanguage("en");
            Assert.Equal(new[] { "Equipped", "Stash", "Missing" },
                new[] { Texts.LegendMark(GearMark.Equipped), Texts.LegendMark(GearMark.Stash), Texts.LegendMark(GearMark.Missing) });
        }
        finally { Texts.UseLanguage("kr"); }
    }

    [Fact]
    public void Korean_slot_and_section_texts()
    {
        Assert.Equal(new[] { "주무기 1", "주무기 2", "권총", "헬멧", "방탄복", "조끼" },
            new[] { Texts.SlotPrimary1, Texts.SlotPrimary2, Texts.SlotHolster, Texts.SlotHelmet, Texts.SlotArmor, Texts.SlotRig });
        Assert.Equal(new[] { "현재 장비", "무기·장비 조건", "특수 조건" }, new[] { Texts.GearLoadoutTitle, Texts.GearRulesTitle, Texts.GearSpecialTitle });
        Assert.Equal("실종(MIA)", Texts.ExitStatus("MissingInAction"));
        Assert.Equal("접기", Texts.ShowLess);
    }

    // 범례 없이 만들어도 화면이 null 을 받지 않는다.
    [Fact]
    public void Legend_is_never_null()
    {
        var model = new GearTabModel(null, new List<GearBlockModel>(), new List<GearBlockModel>(), null, null);

        Assert.NotNull(model.Legend);
        Assert.Empty(model.Legend);
    }
}
