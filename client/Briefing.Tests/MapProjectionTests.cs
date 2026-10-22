using System.Collections.Generic;
using System.Linq;
using Briefing.Core.Contract;
using Briefing.Core.Map;

namespace Briefing.Tests;

// QuestCodex web/src/wiki/mapProjection.test.ts 의 사례를 같은 기대값으로 옮겼다. describe 이름을 주석으로 남긴다.
// 맵 정의 묶기, internalNames 대조, exitsForTab·doorsForTab, applyMapVariants 앞 세 사례는 서버가 하는 일이라 옮기지 않았다.
// objectiveColor 는 옮기지 않았다. 마커는 퀘스트마다 색을 나누지 않고 한 가지 톤으로 그린다.
public class MapProjectionTests
{
    private static readonly MapDef Customs = FixtureFiles.LoadMap("bigmap");
    private static readonly MapDef Expanded = FixtureFiles.LoadMap("interchange-manimal");
    private static readonly MapLayerDef Ground = Customs.Layers.Single(l => l.Level == 0);

    private static BriefingPoint P(double x, double y, double z) => new BriefingPoint { X = x, Y = y, Z = z };

    private static BriefingObjective Obj(string id, params BriefingLocation[] locations)
    {
        var o = new BriefingObjective { ConditionId = id, Text = id };
        o.Locations.AddRange(locations);
        return o;
    }

    private static BriefingLocation At(string map, params BriefingPoint[] points)
    {
        var l = new BriefingLocation { Map = map };
        l.Points.AddRange(points);
        return l;
    }

    // ── map.json 파서 ──

    [Fact]
    public void Parses_map_definition()
    {
        Assert.Equal(180d, Customs.CoordinateRotation);
        Assert.Equal(0, Customs.DefaultLevel);
        Assert.Null(Customs.FloorNames);
        Assert.Equal(new[] { -1, 0, 1, 2, 3 }, Customs.Layers.Select(l => l.Level));
        Assert.Equal("Customs-Ground_Level.svg", Ground.Svg);
        Assert.Equal(1062.48, Ground.ViewBox.Width);
        Assert.Equal(-372d, Ground.ImageBounds.Min.X);
        Assert.Equal(235d, Ground.ImageBounds.Max.Z);
        Assert.Equal(7, Customs.Layers[0].GameBounds.Count);
        Assert.Equal("Shebuka", Customs.Attribution.Author);
        Assert.Equal("CC BY-NC-SA 4.0", Customs.Attribution.License);
        Assert.Equal("tarkov.dev (MIT)", Expanded.Attribution.Calibration);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not json")]
    [InlineData("[]")]
    [InlineData(@"{""layers"":[]}")]
    [InlineData(@"{""layers"":[{""level"":0,""svg"":""a.svg"",""viewBox"":{""width"":0,""height"":1},""imageBounds"":{""min"":{""x"":0,""z"":0},""max"":{""x"":1,""z"":1}}}]}")]
    [InlineData(@"{""layers"":[{""level"":0,""viewBox"":{""width"":1,""height"":1},""imageBounds"":{""min"":{""x"":0,""z"":0},""max"":{""x"":1,""z"":1}}}]}")]
    [InlineData(@"{""layers"":""x""}")]
    public void Unusable_map_json_is_null(string json)
    {
        Assert.Null(MapDefParser.Parse(json));
    }

    [Fact]
    public void Drops_only_the_unusable_layer_and_bad_bounds()
    {
        const string json = @"{""defaultLevel"":0,""floorNames"":""deck"",""layers"":[
            {""level"":0,""svg"":""a.svg"",""viewBox"":{""width"":10,""height"":5},""imageBounds"":{""min"":{""x"":0,""z"":0},""max"":{""x"":2,""z"":1}},
             ""gameBounds"":[{""min"":{""x"":0,""y"":0,""z"":0},""max"":{""x"":1,""y"":1,""z"":1}},{""min"":null},null]},
            {""level"":1,""svg"":""b.svg""}]}";

        var def = MapDefParser.Parse(json);

        Assert.Equal("deck", def.FloorNames);
        Assert.Single(def.Layers);
        Assert.Single(def.Layers[0].GameBounds);
    }

    // ── describe('project') ──

    [Fact]
    public void Project_sends_customs_fuel3_to_the_measured_preview_spot()
    {
        var p = MapProjection.Project(Customs, Ground, P(334.96, 3.04, -189.97));

        Assert.Equal(360.5, p.X, 1);
        Assert.Equal(114.8, p.Y, 1);
    }

    [Fact]
    public void Project_without_rotation_maps_top_left_and_bottom_right_corners()
    {
        var def = new MapDef { CoordinateRotation = 0, Layers = Customs.Layers };
        var layer = new MapLayerDef
        {
            ViewBox = new MapSize { Width = 100, Height = 50 },
            ImageBounds = new FlatBounds { Min = new FlatPoint(0, 0), Max = new FlatPoint(200, 100) },
        };

        var topLeft = MapProjection.Project(def, layer, P(0, 0, 100));
        var bottomRight = MapProjection.Project(def, layer, P(200, 0, 0));

        Assert.Equal(0d, topLeft.X, 9);
        Assert.Equal(0d, topLeft.Y, 9);
        Assert.Equal(100d, bottomRight.X, 9);
        Assert.Equal(50d, bottomRight.Y, 9);
    }

    // ── describe('layerFor') ──

    [Fact]
    public void LayerFor_ground_point_is_ground()
    {
        Assert.Equal(0, MapProjection.LayerFor(Customs, P(334.96, 3.04, -189.97)).Level);
    }

    [Fact]
    public void LayerFor_picks_the_smallest_containing_bounds()
    {
        // "switch basement": x 323~349, 높이 -100~0.5, z -88~-32 (지상층 영역에도 들어 있지만)
        Assert.Equal(-1, MapProjection.LayerFor(Customs, P(330, -2, -60)).Level);
    }

    [Fact]
    public void LayerFor_outside_every_layer_is_the_default_level()
    {
        Assert.Equal(Customs.DefaultLevel, MapProjection.LayerFor(Customs, P(9999, 0, 9999)).Level);
    }

    // ── describe('layerStyle') ──

    [Fact]
    public void LayerStyle_halves_brightness_per_level_below()
    {
        AssertLook(true, 1, 1, MapProjection.LayerStyle(1, 1, 0));
        AssertLook(true, 0.5, 1, MapProjection.LayerStyle(0, 1, 0));
        AssertLook(true, 0.25, 1, MapProjection.LayerStyle(-1, 1, 0));
    }

    [Fact]
    public void LayerStyle_hides_upper_levels_but_keeps_the_default_level_faint()
    {
        Assert.False(MapProjection.LayerStyle(1, -1, 0).Visible);
        AssertLook(true, 1, 0.1, MapProjection.LayerStyle(0, -1, 0));
    }

    private static void AssertLook(bool visible, double brightness, double opacity, LayerLook look)
    {
        Assert.Equal(visible, look.Visible);
        Assert.Equal(brightness, look.Brightness, 9);
        Assert.Equal(opacity, look.Opacity, 9);
    }

    // ── describe('buildTabs') · describe('buildNumberedTabs') · describe('buildTabs areas') ──

    [Fact]
    public void Numbered_marks_the_same_point_of_the_same_number_once()
    {
        // 공장 주간·야간의 같은 존: 서버가 한 탭으로 합쳐도 같은 점이 두 번 올 수 있다
        var set = MapProjection.Numbered(new[]
        {
            new NumberedObjective(1, "q1", Obj("a", At("factory4_day", P(66.67, -1.68, -28.88)), At("factory4_day", P(66.67, -1.68, -28.88)))),
            new NumberedObjective(2, "q2", Obj("b", At("factory4_day", P(1, 0, 1)), At("factory4_day", P(2, 0, 2)))),
        });

        Assert.Equal(new[] { 1, 2, 2 }, set.Markers.Select(m => m.N));
    }

    [Fact]
    public void Numbered_repeats_the_number_for_every_point()
    {
        var set = MapProjection.Numbered(new[]
        {
            new NumberedObjective(1, "q1", Obj("kill")),
            new NumberedObjective(1, "q1", Obj("a", At("bigmap", P(1, 0, 1), P(2, 0, 2)))),
        });

        Assert.Equal(new[] { (1, "a"), (1, "a") }, set.Markers.Select(m => (m.N, m.ConditionId)));
        Assert.All(set.Markers, m => Assert.Equal("q1", m.QuestId));
    }

    [Fact]
    public void Numbered_keeps_the_given_numbers_and_skips_objectives_without_locations()
    {
        var set = MapProjection.Numbered(new[]
        {
            new NumberedObjective(2, "q2", Obj("a", At("bigmap", P(1, 0, 1)))),
            new NumberedObjective(2, "q2", Obj("b", At("bigmap", P(2, 0, 2)))),
            new NumberedObjective(5, "q5", Obj("c", At("bigmap", P(3, 0, 3)))),
            new NumberedObjective(6, "q6", Obj("d")),
        });

        Assert.Equal(new[] { (2, "a"), (2, "b"), (5, "c") }, set.Markers.Select(m => (m.N, m.ConditionId)));
    }

    [Fact]
    public void Numbered_skips_locations_of_other_maps_when_map_ids_are_given()
    {
        var set = MapProjection.Numbered(new[] { new NumberedObjective(1, "q", Obj("a", At("terminal", P(1, 0, 1)), At("Bigmap", P(2, 0, 2)))) },
            new HashSet<string> { "bigmap" });

        Assert.Equal(2d, set.Markers.Single().Point.X);
    }

    [Fact]
    public void Numbered_numbers_areas_and_marks_the_same_area_once()
    {
        var zone = new BriefingArea { Center = P(1, 0, 1), SizeX = 50, SizeZ = 50, Yaw = 0 };
        var day = At("factory4_day", P(1, 0, 1));
        day.Areas.Add(zone);
        var night = At("factory4_day", P(1, 0, 1));
        night.Areas.Add(new BriefingArea { Center = P(1, 0, 1), SizeX = 50, SizeZ = 50, Yaw = 0 });

        var set = MapProjection.Numbered(new[] { new NumberedObjective(1, "q", Obj("kill", day, night)) });

        var area = set.Areas.Single();
        Assert.Equal((1, "q", "kill"), (area.N, area.QuestId, area.ConditionId));
        Assert.Same(zone, area.Area);
    }

    [Fact]
    public void Numbered_without_areas_still_works()
    {
        Assert.Empty(MapProjection.Numbered(new[] { new NumberedObjective(1, "q", Obj("a", At("bigmap", P(1, 0, 1)))) }).Areas);
    }

    // ── describe('areaCorners / areaPolygon') ──

    private static BriefingArea Area(double yaw) => new BriefingArea { Center = P(10, 0, 20), SizeX = 4, SizeZ = 2, Yaw = yaw };

    private static (double, double)[] Round(IEnumerable<FlatPoint> ps) =>
        ps.Select(p => (System.Math.Round(p.X, 2), System.Math.Round(p.Z, 2))).ToArray();

    [Fact]
    public void AreaCorners_without_rotation_is_an_axis_aligned_box()
    {
        Assert.Equal(new[] { (8d, 19d), (12d, 19d), (12d, 21d), (8d, 21d) }, Round(MapProjection.AreaCorners(Area(0))));
    }

    [Fact]
    public void AreaCorners_yaw_90_sends_local_x_to_world_minus_z()
    {
        var corners = Round(MapProjection.AreaCorners(Area(90)));

        // 로컬 (−2, −1) → 월드 오프셋 (−1, 2), 로컬 (+2, −1) → (−1, −2)
        Assert.Equal((9d, 22d), corners[0]);
        Assert.Equal((9d, 18d), corners[1]);
    }

    [Fact]
    public void AreaPolygon_projects_four_corners_on_customs()
    {
        var fuel3 = new BriefingArea { Center = P(334.96, 3.04, -189.97), SizeX = 0, SizeZ = 0, Yaw = 0 };

        var corners = MapProjection.AreaPolygon(Customs, Ground, fuel3);

        Assert.Equal(4, corners.Count);
        Assert.Equal(360.5, corners[0].X, 1);
        Assert.Equal(114.8, corners[0].Y, 1);
    }

    // ── describe('areaLevels / markerLevels') ──
    // Customs "switch basement": x 323~349, z -88~-32, 지하층 높이 -100~0.5. 지상층은 맵 전체(-100~100).

    private static BriefingArea Basement(double minY, double maxY) =>
        new BriefingArea { Center = P(330, 0, -60), SizeX = 10, SizeZ = 10, Yaw = 0, MinY = minY, MaxY = maxY };

    private static readonly QuestMarker BasementMarker = new QuestMarker(1, "q", "a", P(330, 1, -60)); // 점만 보면 지상

    [Fact]
    public void AreaLevels_collects_every_level_the_height_range_crosses()
    {
        Assert.Equal(new[] { -1, 0 }, MapProjection.AreaLevels(Customs, Basement(-3, 5)).OrderBy(l => l));
    }

    [Fact]
    public void AreaLevels_single_height_is_one_level()
    {
        Assert.Equal(new[] { 0 }, MapProjection.AreaLevels(Customs, Basement(3, 3)));
    }

    [Fact]
    public void AreaLevels_without_range_uses_center_y()
    {
        var old = new BriefingArea { Center = P(330, -2, -60), SizeX = 10, SizeZ = 10, Yaw = 0 };

        Assert.Equal(new[] { -1 }, MapProjection.AreaLevels(Customs, old));
    }

    [Fact]
    public void MarkerLevels_follow_the_areas_of_the_same_number()
    {
        var areas = new[] { new AreaMarker(1, "q", "a", Basement(-3, -1)) };

        Assert.Equal(new[] { -1 }, MapProjection.MarkerLevels(Customs, BasementMarker, areas));
        Assert.Equal(new[] { 0 }, MapProjection.MarkerLevels(Customs, BasementMarker, new AreaMarker[0]));
    }

    [Fact]
    public void FirstLevel_prefers_the_default_level_then_the_lowest()
    {
        int First(double minY, double maxY) =>
            MapProjection.FirstLevel(Customs, new[] { BasementMarker }, new[] { new AreaMarker(1, "q", "a", Basement(minY, maxY)) });

        Assert.Equal(0, First(-3, 5));
        Assert.Equal(-1, First(-3, -1));
        Assert.Equal(Customs.DefaultLevel, MapProjection.FirstLevel(Customs, new QuestMarker[0], new AreaMarker[0]));
    }

    [Fact]
    public void MarkerCountsByLevel_follows_area_levels()
    {
        var counts = MapProjection.MarkerCountsByLevel(Customs, new[] { BasementMarker }, new[] { new AreaMarker(1, "q", "a", Basement(-3, 5)) });

        Assert.Equal(1, counts[-1]);
        Assert.Equal(1, counts[0]);
    }

    // ── describe('objectiveColor / areaDrawOrder') ──

    [Fact]
    public void AreaDrawOrder_draws_wide_areas_first()
    {
        AreaMarker Am(int n, double sizeX, double sizeZ) =>
            new AreaMarker(n, "q" + n, n.ToString(), new BriefingArea { Center = P(0, 0, 0), SizeX = sizeX, SizeZ = sizeZ });
        var hotel = Am(1, 160, 170);
        var courtyard = Am(2, 66, 38);
        var street = Am(3, 224, 104);

        Assert.Equal(new[] { 1, 3, 2 }, MapProjection.AreaDrawOrder(new[] { courtyard, hotel, street }).Select(a => a.N));
    }

    // ── describe('floorsWithOtherMarkers') · describe('markerCountsByLevel') ──

    [Fact]
    public void FloorsWithOtherMarkers_are_marked_levels_other_than_the_current()
    {
        var counts = new Dictionary<int, int> { [0] = 3, [-1] = 1 };

        Assert.Equal(new[] { -1 }, MapProjection.FloorsWithOtherMarkers(counts, 0));
        Assert.Equal(new[] { -1, 0 }, MapProjection.FloorsWithOtherMarkers(counts, 1).OrderBy(l => l));
    }

    [Fact]
    public void MarkerCountsByLevel_counts_markers_per_level()
    {
        var counts = MapProjection.MarkerCountsByLevel(Customs, new[]
        {
            new QuestMarker(1, "q1", "a", P(334.96, 3.04, -189.97)),
            new QuestMarker(2, "q2", "b", P(330, -2, -60)),
            new QuestMarker(3, "q3", "c", P(101.45, 3.06, -14.1)),
        });

        Assert.Equal(2, counts[0]);
        Assert.Equal(1, counts[-1]);
        Assert.False(counts.ContainsKey(1));
    }

    // ── describe('zoomAt / fitView') ──

    [Fact]
    public void ZoomAt_keeps_the_point_under_the_cursor()
    {
        var v = MapProjection.ZoomAt(MapProjection.FitView(), 2, 100, 50);

        Assert.Equal((2d, -100d, -50d), (v.Scale, v.X, v.Y));
    }

    [Fact]
    public void ZoomAt_clamps_scale_to_1_through_12()
    {
        Assert.Equal(MapProjection.FitView(), MapProjection.ZoomAt(MapProjection.FitView(), 0.5, 10, 10));
        Assert.Equal(12d, MapProjection.ZoomAt(new MapViewport(10, 0, 0), 2, 0, 0).Scale);
    }

    // ── describe('applyMapVariants') 마지막 사례 ──

    [Fact]
    public void Expanded_interchange_contains_the_west_door_and_east_zone()
    {
        var ground = Expanded.Layers[0];
        foreach (var p in new[] { P(-379.33, 2, -207.43), P(530.33, 32.86, 82.37) })
        {
            var q = MapProjection.Project(Expanded, ground, p);
            Assert.InRange(q.X, 0.0001, ground.ViewBox.Width - 0.0001);
            Assert.InRange(q.Y, 0.0001, ground.ViewBox.Height - 0.0001);
        }
    }
}
