using System.Collections.Generic;
using System.Linq;
using Briefing.Core.Contract;
using Briefing.Core.Screen;

namespace Briefing.Tests;

public class LeftColumnTests
{
    private static readonly BriefingResponse Active = FixtureFiles.Load(FixtureFiles.Active, "kr");

    [Fact]
    public void Default_map_is_the_first_with_objectives()
    {
        Assert.Equal("bigmap", LeftColumn.DefaultMapKey(Active));
        Assert.Equal("bigmap", LeftColumn.DefaultMapKey(FixtureFiles.Load(FixtureFiles.Empty, "kr"))); // 전부 0 → 첫 맵
        Assert.Null(LeftColumn.DefaultMapKey(new BriefingResponse()));

        var r = BriefingParser.Parse(@"{""schemaVersion"":1,""maps"":[{""key"":""a"",""count"":0},{""key"":""b"",""count"":3}]}").Response;
        Assert.Equal("b", LeftColumn.DefaultMapKey(r));
    }

    [Fact]
    public void Chips_follow_server_order_and_dim_empty_maps()
    {
        var m = LeftColumn.Build(Active, null);

        Assert.Equal("bigmap", m.SelectedKey);
        Assert.Equal(new[] { "bigmap", "factory4_day", "labyrinth" }, m.Chips.Select(c => c.Key));
        Assert.Equal(new[] { "세관", "공장", "The Labyrinth" }, m.Chips.Select(c => c.Name));
        Assert.Equal(new[] { 10, 14, 0 }, m.Chips.Select(c => c.Count));
        Assert.Equal(new[] { false, false, true }, m.Chips.Select(c => c.Dim));
        Assert.Equal(new[] { true, false, false }, m.Chips.Select(c => c.Selected));
    }

    [Fact]
    public void Raid_map_chip_is_flagged_independently_of_the_selection()
    {
        Assert.All(LeftColumn.Build(Active, null).Chips, c => Assert.False(c.InRaid));

        var m = LeftColumn.Build(Active, "bigmap", raidMapKey: "factory4_day"); // 레이드 중 다른 칩을 눌러 세관을 보는 중

        Assert.Equal(new[] { false, true, false }, m.Chips.Select(c => c.InRaid));
        Assert.Equal(new[] { true, false, false }, m.Chips.Select(c => c.Selected));
    }

    [Fact]
    public void Here_section_lists_the_selected_map_in_server_order()
    {
        var m = LeftColumn.Build(Active, "factory4_day");

        Assert.Equal("factory4_day", m.SelectedKey);
        Assert.Equal(new[] { false, true, false }, m.Chips.Select(c => c.Selected));
        Assert.Equal(LeftColumn.HereKey, m.Here.Key);
        Assert.Equal("공장에서 진행", m.Here.Title);
        Assert.Equal(2, m.Here.Count);
        Assert.Equal(new[] { "Forklift Certified", "The Walls Have Eyes" }, m.Here.Rows.Select(r => r.Name));
        Assert.Null(m.Here.EmptyText);
    }

    [Fact]
    public void Unknown_preferred_map_falls_back_to_default()
    {
        Assert.Equal("bigmap", LeftColumn.Build(Active, "nope").SelectedKey);
    }

    [Fact]
    public void Selected_map_is_the_preferred_map_or_the_default()
    {
        Assert.Equal("factory4_day", LeftColumn.SelectedMap(Active, "factory4_day").Key);
        Assert.Equal("bigmap", LeftColumn.SelectedMap(Active, "nope").Key);
        Assert.Equal("bigmap", LeftColumn.SelectedMap(Active, null).Key);
        Assert.Null(LeftColumn.SelectedMap(new BriefingResponse(), "bigmap"));
    }

    [Fact]
    public void Map_without_quests_shows_empty_text()
    {
        var m = LeftColumn.Build(Active, "labyrinth");

        Assert.Equal("The Labyrinth에서 진행", m.Here.Title);
        Assert.Equal(0, m.Here.Count);
        Assert.Equal("이 맵에서 진행할 퀘스트가 없습니다", m.Here.EmptyText);
    }

    [Fact]
    public void Quest_row_carries_marker_trader_and_progress()
    {
        var rows = LeftColumn.Build(Active, "bigmap").Here.Rows;

        var first = rows[0];
        Assert.Equal("675c15fbf7da9792a4059871", first.QuestId);
        Assert.Equal("Provide Viewership", first.Name);
        Assert.Equal("Eliminate any target inside the industrial plant on Customs ×15", first.Summary);
        Assert.Equal(1, first.Marker);
        Assert.Equal("#ff8c42", first.MarkerColor);
        Assert.Equal("6617beeaa9cfa777ca915b7c", first.TraderId);
        Assert.Equal("레프", first.TraderName);
        Assert.Equal("레", first.TraderInitial);
        Assert.Equal("6 / 15", first.Progress);
        Assert.Equal(0.4f, first.Fraction);

        Assert.Equal("#2ec4b6", rows[1].MarkerColor);

        var unmarked = rows[4]; // 천국에서 태어난 저격수
        Assert.Null(unmarked.Marker);
        Assert.Null(unmarked.MarkerColor);
        Assert.Equal("4 / 5", unmarked.Progress);
        Assert.Equal(0.8f, unmarked.Fraction);
    }

    [Fact]
    public void Anywhere_section_lists_all_anywhere_quests()
    {
        var any = LeftColumn.Build(Active, "factory4_day").Anywhere;

        Assert.Equal(LeftColumn.AnywhereKey, any.Key);
        Assert.Equal("아무 맵에서나", any.Title);
        Assert.Equal(8, any.Count);
        Assert.Equal("영점 조정", any.Rows[0].Name);
        Assert.Equal("메카닉", any.Rows[0].TraderName);
        Assert.Equal("5 / 20", any.Rows[0].Progress);
        Assert.Equal(0.25f, any.Rows[0].Fraction);
        Assert.Equal("목표 2개", any.Rows[3].Summary);
        Assert.Equal("0 / 30", any.Rows[3].Progress);
        Assert.Equal(0f, any.Rows[3].Fraction);
        Assert.All(any.Rows, r => Assert.Null(r.MarkerColor));
    }

    [Fact]
    public void Empty_profile_has_no_anywhere_section()
    {
        var m = LeftColumn.Build(FixtureFiles.Load(FixtureFiles.Empty, "kr"), null);

        Assert.Null(m.Anywhere);
        Assert.Equal(0, m.Here.Count);
        Assert.Equal(new[] { true, true, true }, m.Chips.Select(c => c.Dim));
    }

    [Fact]
    public void No_maps_builds_an_empty_model()
    {
        var m = LeftColumn.Build(new BriefingResponse(), "bigmap");

        Assert.Empty(m.Chips);
        Assert.Null(m.SelectedKey);
        Assert.Null(m.Here);
        Assert.Null(m.Anywhere);
    }

    [Fact]
    public void Unknown_trader_falls_back_to_question_mark()
    {
        var traders = new Dictionary<string, BriefingTrader>();

        var row = LeftColumn.Row(new BriefingQuest { QuestId = "q", TraderId = "nope", Name = "x" }, traders);
        Assert.Null(row.TraderName);
        Assert.Equal("?", row.TraderInitial);

        Assert.Equal("?", LeftColumn.Row(new BriefingQuest { QuestId = "q" }, traders).TraderInitial);
    }

    [Theory]
    [InlineData(6d, 15d, "6 / 15")]
    [InlineData(2.5d, 10d, "2.5 / 10")]
    [InlineData(1.333d, 4d, "1.33 / 4")]
    [InlineData(null, 3d, "0 / 3")]
    [InlineData(null, null, null)]
    public void Progress_text(double? current, double? target, string expected)
    {
        Assert.Equal(expected, LeftColumn.ProgressText(current, target));
    }

    [Theory]
    [InlineData(6d, 15d, 0.4f)]
    [InlineData(20d, 15d, 1f)]
    [InlineData(-1d, 5d, 0f)]
    [InlineData(0d, 0d, null)]
    [InlineData(null, null, null)]
    public void Progress_fraction_is_clamped(double? current, double? target, float? expected)
    {
        Assert.Equal(expected, LeftColumn.Fraction(current, target));
    }

    [Theory]
    [InlineData("프라퍼", "프")]
    [InlineData("BTR 운전병", "B")]
    [InlineData("lotus", "L")]
    [InlineData("  Artem", "A")]
    [InlineData("", "?")]
    [InlineData((string)null, "?")]
    public void Trader_initial(string name, string expected)
    {
        Assert.Equal(expected, LeftColumn.Initial(name));
    }

    [Theory]
    [InlineData(1, "#ff8c42")]
    [InlineData(2, "#2ec4b6")]
    [InlineData(10, "#94a3b8")]
    [InlineData(11, "#ff8c42")]
    [InlineData(23, "#a78bfa")]
    [InlineData(0, null)]
    public void Marker_palette_cycles_the_web_colors(int marker, string expected)
    {
        Assert.Equal(expected, MarkerPalette.Hex(marker));
    }
}
