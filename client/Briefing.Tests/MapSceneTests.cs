using System.Collections.Generic;
using System.Linq;
using Briefing.Core.Contract;
using Briefing.Core.Map;
using Briefing.Core.Screen;

namespace Briefing.Tests;

// 세관 fixture: 마커 1 Provide Viewership(점 + 구역), 2 요충지 탈환(점 + 구역), 3 계획된 - 파트 2(3층 점),
// 4 Hobby Club(목표 4개가 같은 점), 마커 없는 퀘스트 3개. 탈출구 ZB-013(지하)과 Dorms V-Ex(지상), 잠긴 문 2개(지하·지상).
[Collection("Texts")]
public class MapSceneTests
{
    private const string Viewership = "675c15fbf7da9792a4059871";
    private const string Hold = "60e71b9bbd90872cb85440f3";
    private const string Planned = "5a27ba1c86f77461ea5a3c56";
    private const string Hobby = "684009026ceedc792c09b2a7";

    private static readonly BriefingResponse Active = FixtureFiles.Load(FixtureFiles.Active, "kr");
    private static readonly MapDef Customs = FixtureFiles.LoadMap("bigmap");
    private static BriefingMap Bigmap => Active.Maps[0];

    private static MapSceneModel Scene(int? level = null, string hot = null, ISet<string> shown = null,
        bool exits = true, bool doors = true, bool areas = true, BriefingMap map = null, BriefingResponse response = null,
        bool? pmc = null, bool? scav = null, bool? transit = null) =>
        MapScene.Build(new MapSceneInput
        {
            Response = response ?? Active,
            Map = map ?? Bigmap,
            Def = Customs,
            Level = level,
            EmphasisQuestId = hot,
            ShownQuestIds = shown,
            ShowExitPmc = pmc ?? exits,
            ShowExitScav = scav ?? exits,
            ShowExitTransit = transit ?? exits,
            ShowDoors = doors,
            ShowAreas = areas,
        });

    [Fact]
    public void Markers_are_map_section_quests_with_a_marker_one_per_distinct_point()
    {
        var set = MapScene.Markers(Bigmap, null);

        Assert.Equal(new[] { 1, 2, 3, 4 }, set.Markers.Select(m => m.N)); // Hobby Club 목표 4개는 같은 점 → 하나
        Assert.Equal(new[] { 1, 2 }, set.Areas.Select(a => a.N));
        Assert.Equal(Hobby, set.Markers[3].QuestId);
    }

    [Fact]
    public void Markers_skip_quests_not_shown_in_the_map_section()
    {
        var set = MapScene.Markers(Bigmap, new HashSet<string> { Viewership, Hobby });

        Assert.Equal(new[] { 1, 4 }, set.Markers.Select(m => m.N));
        Assert.Equal(new[] { 1 }, set.Areas.Select(a => a.N));
    }

    [Fact]
    public void Default_level_shows_ground_markers_with_marker_colors()
    {
        var s = Scene();

        Assert.Equal(0, s.Level);
        Assert.Equal(new[] { 1, 2, 4 }, s.Markers.Select(m => m.N)); // 3 은 3층
        Assert.Equal(new[] { "#ff8c42", "#2ec4b6", "#facc15" }, s.Markers.Select(m => m.Color));
        Assert.Equal(0.3940, s.Markers[0].At.U, 4);
        Assert.Equal(0.4814, s.Markers[0].At.V, 4);
        Assert.All(s.Markers, m => Assert.Equal(Emphasis.Normal, m.Emphasis));
    }

    [Theory]
    [InlineData(null, 0)]
    [InlineData(1, 1)]
    [InlineData(7, 0)] // 이 지도에 없는 층 → 기본 층
    public void Level_is_the_chosen_level_if_the_map_has_it(int? chosen, int expected)
    {
        Assert.Equal(expected, Scene(chosen).Level);
        Assert.Equal(expected, MapScene.LevelOf(Customs, chosen));
    }

    [Fact]
    public void Marker_levels_follow_areas_so_zone_markers_show_on_upper_floors()
    {
        Assert.Equal(new[] { 1, 2 }, Scene(1).Markers.Select(m => m.N)); // 구역이 2층까지 걸침
        Assert.Empty(Scene(-1).Markers);
    }

    [Fact]
    public void Floors_go_top_down_with_names_selection_and_dots()
    {
        var floors = Scene().Floors;

        Assert.Equal(new[] { 3, 2, 1, 0, -1 }, floors.Select(f => f.Level));
        Assert.Equal(new[] { "4층", "3층", "2층", "지상", "지하" }, floors.Select(f => f.Name));
        Assert.Equal(new[] { false, false, false, true, false }, floors.Select(f => f.Selected));
        Assert.Equal(new[] { true, true, true, false, false }, floors.Select(f => f.Dot)); // 지하엔 마커 없음
    }

    [Fact]
    public void Single_floor_map_has_no_floor_buttons()
    {
        var def = new MapDef { DefaultLevel = 0, Layers = { Customs.Layers.Single(l => l.Level == 0) } };

        var s = MapScene.Build(new MapSceneInput { Response = Active, Map = Bigmap, Def = def });

        Assert.Empty(s.Floors);
    }

    [Fact]
    public void Areas_on_the_level_are_drawn_wide_first_as_ratio_polygons()
    {
        var areas = Scene().Areas;

        Assert.Equal(new[] { 2, 1 }, areas.Select(a => a.N)); // 40×75 이 31.94×73.07 보다 넓다
        Assert.All(areas, a => Assert.Equal(4, a.Polygon.Count));
        Assert.All(areas.SelectMany(a => a.Polygon), p => Assert.InRange(p.U, 0, 1));
        Assert.Equal(MarkerPalette.Hex(2), areas[0].Color);
        Assert.Empty(Scene(-1).Areas);
        Assert.Empty(Scene(areas: false).Areas);
    }

    [Fact]
    public void Doors_are_only_on_the_current_level()
    {
        var ground = Scene().Doors.Single();
        var basement = Scene(-1).Doors.Single();

        Assert.Equal("Factory emergency exit key", ground.Label);
        Assert.Equal(0.3196, ground.At.U, 4);
        Assert.Equal(0.4664, basement.At.U, 4);
        Assert.Empty(Scene(1).Doors);
        Assert.Empty(Scene(doors: false).Doors);
    }

    [Fact]
    public void Exits_are_on_every_level_other_floors_first_with_tooltips()
    {
        try
        {
            Texts.UseLanguage("kr");
            var exits = Scene().Exits;
            Assert.Equal(new[] { "ZB-013 · PMC", "Dorms V-Ex · PMC" }, exits.Select(e => e.Title)); // ZB-013 은 지하 → 먼저
            Assert.Equal(new[] { true, false }, exits.Select(e => e.OtherFloor));
            Assert.Equal("스위치·전원 작동 필요", exits[0].Detail);
            Assert.Equal("5,000 루블 필요 · 등장 확률 50%", exits[1].Detail); // 서버 문장이 있으면 그것

            Assert.Equal(new[] { "Dorms V-Ex · PMC", "ZB-013 · PMC" }, Scene(-1).Exits.Select(e => e.Title));
            Assert.Empty(Scene(exits: false).Exits);
        }
        finally { Texts.UseLanguage("kr"); }
    }

    // shared = PMC 나 스캐브 중 하나라도 켜져 있으면 보인다. 알 수 없는 종류도 같은 규칙.
    [Theory]
    [InlineData(true, true, true, "pmc,shared,scav,transit,weird")]
    [InlineData(true, false, false, "pmc,shared,weird")]
    [InlineData(false, true, false, "shared,scav,weird")]
    [InlineData(false, false, true, "transit")]
    [InlineData(true, true, false, "pmc,shared,scav,weird")]
    [InlineData(false, false, false, "")]
    public void Exit_kinds_are_filtered_per_checkbox(bool pmc, bool scav, bool transit, string expected)
    {
        const string json = @"{""schemaVersion"":1,""maps"":[
            {""key"":""bigmap"",""locationIds"":[""bigmap""],""name"":""세관"",""exits"":[
                {""key"":""a"",""kind"":""pmc"",""position"":{""x"":100,""y"":0,""z"":0}},
                {""key"":""b"",""kind"":""shared"",""position"":{""x"":100,""y"":0,""z"":0}},
                {""key"":""c"",""kind"":""scav"",""position"":{""x"":100,""y"":0,""z"":0}},
                {""key"":""d"",""kind"":""transit"",""position"":{""x"":100,""y"":0,""z"":0},""target"":""x""},
                {""key"":""e"",""kind"":""weird"",""position"":{""x"":100,""y"":0,""z"":0}}]}]}";
        var response = BriefingParser.Parse(json).Response;

        var s = Scene(map: response.Maps[0], response: response, pmc: pmc, scav: scav, transit: transit);

        Assert.Equal(expected, string.Join(",", s.Exits.Select(e => e.Kind)));
    }

    [Fact]
    public void Exit_checkboxes_change_the_signature()
    {
        Assert.NotEqual(Scene().Signature, Scene(pmc: false).Signature);
        Assert.NotEqual(Scene(pmc: false).Signature, Scene(scav: false).Signature);
        Assert.NotEqual(Scene(scav: false).Signature, Scene(transit: false).Signature);
    }

    [Fact]
    public void Exit_and_door_tooltips_follow_the_web_rules()
    {
        const string json = @"{""schemaVersion"":1,""maps"":[
            {""key"":""bigmap"",""locationIds"":[""bigmap""],""name"":""세관"",""exits"":[
                {""key"":""9"",""name"":"""",""kind"":""transit"",""position"":{""x"":100,""y"":0,""z"":0},""target"":""factory4_night""},
                {""key"":""10"",""name"":"""",""kind"":""transit"",""position"":{""x"":100,""y"":0,""z"":0},""target"":""nowhere""},
                {""key"":""EXFIL"",""name"":"""",""kind"":""shared"",""position"":{""x"":100,""y"":0,""z"":0},""requirementKind"":""coop"",""chance"":12.5},
                {""key"":""x"",""name"":""Smugglers"",""kind"":""scav"",""position"":{""x"":100,""y"":0,""z"":0}},
                {""key"":""nopos"",""kind"":""pmc""}],
             ""lockedDoors"":[
                {""keyName"":""Red"",""kind"":""keycard"",""position"":{""x"":100,""y"":0,""z"":0}},
                {""keyName"":""x"",""kind"":""keypad"",""position"":{""x"":100,""y"":0,""z"":0},""code"":""2024""},
                {""keyName"":""x"",""kind"":""keypad"",""position"":{""x"":100,""y"":0,""z"":0}},
                {""keyName"":""C4"",""kind"":""explosive"",""position"":{""x"":100,""y"":0,""z"":0}},
                {""keyName"":""Torch"",""kind"":""hatch"",""position"":{""x"":100,""y"":0,""z"":0}},
                {""keyName"":""Key"",""kind"":""door""}]},
            {""key"":""factory4_day"",""locationIds"":[""factory4_day"",""factory4_night""],""name"":""공장""}]}";
        var response = BriefingParser.Parse(json).Response;
        try
        {
            Texts.UseLanguage("kr");
            var s = Scene(map: response.Maps[0], response: response);
            Assert.Equal(new[] { "환승 → 공장", "환승 → nowhere", "EXFIL · PMC + 스캐브", "Smugglers · 스캐브" }, s.Exits.Select(e => e.Title));
            Assert.Equal(new[] { null, null, "스캐브와 협동 필요 · 등장 확률 12.5%", null }, s.Exits.Select(e => e.Detail));
            Assert.Equal(new[] { "키카드 · Red", "키패드 잠금 · 코드 2024", "키패드 잠금", "폭파 필요: C4", "얼음을 녹여야 함: Torch" },
                s.Doors.Select(d => d.Label)); // 위치 없는 문은 빠진다

            Texts.UseLanguage("en");
            s = Scene(map: response.Maps[0], response: response);
            Assert.Equal("Transit → 공장", s.Exits[0].Title); // 맵 이름은 서버가 lang 으로 쓴 것
            Assert.Equal("EXFIL · PMC + Scav", s.Exits[2].Title);
            Assert.Equal("Co-op with a Scav · 12.5% chance", s.Exits[2].Detail);
            Assert.Equal(new[] { "Keycard · Red", "Keypad lock · code 2024", "Keypad lock", "Blow open with: C4", "Melt the ice with: Torch" },
                s.Doors.Select(d => d.Label));
        }
        finally { Texts.UseLanguage("kr"); }
    }

    [Fact]
    public void Hot_quest_keeps_its_markers_and_areas_and_dims_the_rest()
    {
        var s = Scene(hot: Hold);

        Assert.Equal(new[] { Emphasis.Dim, Emphasis.Hot, Emphasis.Dim }, s.Markers.Select(m => m.Emphasis));
        Assert.Equal(new[] { Emphasis.Hot, Emphasis.Dim }, s.Areas.Select(a => a.Emphasis));
    }

    [Theory]
    [InlineData("hot", "here", "picked", "hot")]
    [InlineData(null, "here", "picked", "picked")]
    [InlineData(null, "anywhere", "picked", null)] // "아무 맵에서나" 줄을 펼쳐도 지도 강조는 없다
    [InlineData(null, null, null, null)]
    public void Emphasis_quest_is_hover_then_the_expanded_map_section_quest(string hot, string section, string expanded, string expected)
    {
        Assert.Equal(expected, MapScene.EmphasisQuest(hot, section, expanded));
    }

    [Fact]
    public void Emphasis_quest_must_be_shown_on_this_map()
    {
        var shown = new HashSet<string> { Viewership };

        Assert.Equal(Viewership, MapScene.EmphasisQuest(null, LeftColumn.HereKey, Viewership, shown));
        Assert.Null(MapScene.EmphasisQuest(null, LeftColumn.HereKey, "other-map-quest", shown)); // 다른 맵에서 펼친 채 맵을 바꿈
    }

    [Fact]
    public void Signature_ignores_emphasis_but_not_level_or_layers()
    {
        var plain = Scene().Signature;

        Assert.Equal(plain, Scene(hot: Hold).Signature);
        Assert.NotEqual(plain, Scene(1).Signature);
        Assert.NotEqual(plain, Scene(exits: false).Signature);
        Assert.NotEqual(plain, Scene(shown: new HashSet<string> { Viewership }).Signature);
    }

    [Fact]
    public void Signature_changes_when_exits_or_doors_move_or_get_renamed()
    {
        // 같은 맵·층에 새 응답이 와서 위치·말풍선 문구만 바뀐 경우 — 개수는 같다.
        var plain = Scene().Signature;
        var moved = FixtureFiles.Load(FixtureFiles.Active, "kr");
        moved.Maps[0].Exits[0].Position.X += 50;
        var renamed = FixtureFiles.Load(FixtureFiles.Active, "kr");
        renamed.Maps[0].Exits[1].Name = "Renamed";
        var door = FixtureFiles.Load(FixtureFiles.Active, "kr");
        door.Maps[0].LockedDoors.First(d => d.Position != null).Position.X += 50;

        Assert.Equal(plain, Scene(map: FixtureFiles.Load(FixtureFiles.Active, "kr").Maps[0]).Signature); // 같은 응답을 다시 받으면 같다
        Assert.NotEqual(plain, Scene(map: moved.Maps[0], response: moved).Signature);
        Assert.NotEqual(plain, Scene(map: renamed.Maps[0], response: renamed).Signature);
        Assert.NotEqual(plain, Scene(map: door.Maps[0], response: door).Signature);
    }

    [Fact]
    public void Transit_target_matches_map_keys_regardless_of_case()
    {
        const string json = @"{""schemaVersion"":1,""maps"":[
            {""key"":""bigmap"",""locationIds"":[""bigmap""],""name"":""세관"",""exits"":[
                {""key"":""9"",""name"":"""",""kind"":""transit"",""position"":{""x"":100,""y"":0,""z"":0},""target"":""Sandbox""}]},
            {""key"":""Sandbox"",""locationIds"":[],""name"":""그라운드 제로""}]}";
        var response = BriefingParser.Parse(json).Response;
        try
        {
            Texts.UseLanguage("kr");
            Assert.Equal("환승 → 그라운드 제로", Scene(map: response.Maps[0], response: response).Exits.Single().Title);
        }
        finally { Texts.UseLanguage("kr"); }
    }

    [Fact]
    public void Credit_names_author_and_license()
    {
        Assert.Equal("Map: Shebuka (CC BY-NC-SA 4.0)", Scene().Credit);
        var def = new MapDef { DefaultLevel = 0, Layers = Customs.Layers };
        Assert.Null(MapScene.Build(new MapSceneInput { Response = Active, Map = Bigmap, Def = def }).Credit);
    }

    [Fact]
    public void Focus_picks_the_level_and_box_of_the_quest()
    {
        var set = MapScene.Markers(Bigmap, null);

        var viewership = MapScene.Focus(Customs, set, Viewership);
        Assert.Equal(0, viewership.Level);
        Assert.Equal(0.3736, viewership.MinU, 4); // 구역 꼭짓점까지 감싼다
        Assert.Equal(0.4164, viewership.MaxU, 4);
        Assert.Equal(0.4135, viewership.MinV, 4);
        Assert.Equal(0.5576, viewership.MaxV, 4);

        var planned = MapScene.Focus(Customs, set, Planned);
        Assert.Equal(2, planned.Level); // 3층에만 있는 점
        Assert.Equal(planned.MinU, planned.MaxU);

        Assert.Null(MapScene.Focus(Customs, set, "5c0bde0986f77479cf22c2f8")); // 마커 없는 퀘스트
    }

    // ── Reveal: Content 1000×500 이 칸 1200×600 가운데 — 보이는 범위는 Content 좌표로 x −100~1100, y −50~550, 여백 40.

    private static readonly MapFocus Box = new MapFocus(0, 0.5, 0.5, 0.52, 0.52); // Content 좌표 (500,250)~(520,260)

    [Fact]
    public void Reveal_leaves_the_fit_view_alone()
    {
        Assert.Equal(MapProjection.FitView(), MapScene.Reveal(MapProjection.FitView(), Box, 1000, 500, 1200, 600));
    }

    [Fact]
    public void Reveal_leaves_a_visible_box_alone()
    {
        var v = new MapViewport(2, -500, -250); // 상자 화면 위치 x 500~540, y 250~270
        Assert.Equal(v, MapScene.Reveal(v, Box, 1000, 500, 1200, 600));
    }

    [Fact]
    public void Reveal_moves_the_least_to_bring_the_box_in()
    {
        var v = new MapViewport(4, 0, 0); // 상자 화면 위치 x 2000~2080(오른쪽 밖), y 1000~1040(아래 밖)

        var r = MapScene.Reveal(v, Box, 1000, 500, 1200, 600);

        Assert.Equal(4d, r.Scale);
        Assert.Equal(1060 - 2080, r.X); // 오른쪽 끝 = 1100 − 40
        Assert.Equal(510 - 1040, r.Y);  // 아래 끝 = 550 − 40
    }

    [Fact]
    public void Reveal_zooms_out_to_fit_a_big_box_but_never_zooms_in()
    {
        var wide = new MapFocus(0, 0.1, 0.4, 0.6, 0.5); // 500×50

        var r = MapScene.Reveal(new MapViewport(4, 0, 0), wide, 1000, 500, 1200, 600);

        Assert.Equal(1120d / 500, r.Scale, 6);                // 보이는 폭 1200 − 80
        Assert.Equal(500 - 350 * r.Scale, r.X, 6);           // 상자 가운데(350) → 보이는 범위 가운데(500)
        Assert.Equal(250 - 225 * r.Scale, r.Y, 6);
        // 맞추려면 1배 이하로 가야 하면 원래대로(칸 = Content 크기, 지도 전체 상자)
        Assert.Equal(MapProjection.FitView(), MapScene.Reveal(new MapViewport(1.5, 0, 0), new MapFocus(0, 0, 0, 1, 1), 1000, 500, 1000, 500));
    }

    // 레이드 중 끝난 목표의 마커·구역은 흐리게. 마우스를 올린 퀘스트면 그래도 강조한다.
    private static BriefingResponse Live(params string[] doneConditionIds) =>
        LiveProgress.Apply(Active, doneConditionIds.ToDictionary(id => id, id => new LiveObjective(id, 1, true)));

    [Fact]
    public void Done_objectives_dim_their_markers_and_areas()
    {
        var live = Live("675c1c980fd114390c638b89"); // Provide Viewership 의 유일한 목표
        var s = Scene(map: live.Maps[0], response: live);

        Assert.Equal(Emphasis.Dim, s.Markers.Single(m => m.QuestId == Viewership).Emphasis);
        Assert.All(s.Markers.Where(m => m.QuestId != Viewership), m => Assert.Equal(Emphasis.Normal, m.Emphasis));
        Assert.Equal(Emphasis.Dim, s.Areas.Single(a => a.QuestId == Viewership).Emphasis);
        Assert.Equal(Emphasis.Normal, s.Areas.Single(a => a.QuestId == Hold).Emphasis);
        Assert.Equal(Emphasis.Hot, Scene(map: live.Maps[0], response: live, hot: Viewership).Markers.Single(m => m.QuestId == Viewership).Emphasis);
        Assert.Equal(Scene().Signature, s.Signature); // 흐림은 모양이 아니다 — 오버레이를 다시 만들지 않고 알파만 바꾼다
    }

    [Fact]
    public void Shared_point_marker_dims_only_when_all_its_objectives_are_done()
    {
        var hobby = new[] { "685e95c44b14e68996ad6590", "685e95d28ba62d57a3235675", "685e95def338135da83ff978", "685e95f0ac5f975749f6c1f3" };

        var some = Live(hobby.Take(3).ToArray());
        Assert.Equal(Emphasis.Normal, Scene(map: some.Maps[0], response: some).Markers.Single(m => m.QuestId == Hobby).Emphasis);

        var all = Live(hobby);
        Assert.Equal(Emphasis.Dim, Scene(map: all.Maps[0], response: all).Markers.Single(m => m.QuestId == Hobby).Emphasis);
    }
}
