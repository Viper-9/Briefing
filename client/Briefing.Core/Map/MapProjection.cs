using System;
using System.Collections.Generic;
using System.Linq;
using Briefing.Core.Contract;

namespace Briefing.Core.Map;

// 층 SVG 의 viewBox 좌표(왼쪽 위 원점, y 아래로 증가)
public readonly struct SvgPoint
{
    public SvgPoint(double x, double y)
    {
        X = x;
        Y = y;
    }

    public double X { get; }
    public double Y { get; }
}

// 층 SVG 를 겹쳐 그릴 때 한 층의 모습. Brightness 는 RGB 에 곱하고 Opacity 는 알파.
public readonly struct LayerLook
{
    public LayerLook(bool visible, double brightness, double opacity)
    {
        Visible = visible;
        Brightness = brightness;
        Opacity = opacity;
    }

    public bool Visible { get; }
    public double Brightness { get; }
    public double Opacity { get; }
}

// N 은 서버가 준 퀘스트 마커 번호. 한 퀘스트의 점은 모두 같은 N 을 쓴다.
public sealed class QuestMarker
{
    public QuestMarker(int n, string questId, string conditionId, BriefingPoint point)
    {
        N = n;
        QuestId = questId;
        ConditionId = conditionId;
        Point = point;
    }

    public int N { get; }
    public string QuestId { get; }
    public string ConditionId { get; }
    public BriefingPoint Point { get; }
}

// 퀘스트 구역. N 은 같은 퀘스트의 마커 번호.
public sealed class AreaMarker
{
    public AreaMarker(int n, string questId, string conditionId, BriefingArea area)
    {
        N = n;
        QuestId = questId;
        ConditionId = conditionId;
        Area = area;
    }

    public int N { get; }
    public string QuestId { get; }
    public string ConditionId { get; }
    public BriefingArea Area { get; }
}

public sealed class NumberedObjective
{
    public NumberedObjective(int n, string questId, BriefingObjective objective)
    {
        N = n;
        QuestId = questId;
        Objective = objective;
    }

    public int N { get; }
    public string QuestId { get; }
    public BriefingObjective Objective { get; }
}

// 지도 탭 하나(응답의 maps[] 하나)의 마커와 구역.
public sealed class MarkerSet
{
    public List<QuestMarker> Markers { get; } = new List<QuestMarker>();
    public List<AreaMarker> Areas { get; } = new List<AreaMarker>();
}

// 확대·이동 상태. 지도 좌표 c(왼쪽 위 원점, y 아래로)는 화면의 c * Scale + (X, Y) 에 그려진다.
public readonly struct MapViewport
{
    public MapViewport(double scale, double x, double y)
    {
        Scale = scale;
        X = x;
        Y = y;
    }

    public double Scale { get; }
    public double X { get; }
    public double Y { get; }
}

// QuestCodex 웹 web/src/wiki/mapProjection.ts 를 그대로 옮겼다. 계산은 DynamicMaps 의 MapView/MapLayer 와 같다.
// 웹에서 하던 맵 폴더 고르기와 탭별 탈출구·문 합치기는 서버가 응답에 담아 주므로 여기 없다.
public static class MapProjection
{
    public const double MinScale = 1;
    public const double MaxScale = 12;

    // Unity (x, z) 를 반시계로 deg 만큼 돌린다.
    private static void Rotate(double x, double z, double deg, out double rx, out double rz)
    {
        var r = deg * Math.PI / 180;
        var cos = Math.Cos(r);
        var sin = Math.Sin(r);
        rx = x * cos - z * sin;
        rz = x * sin + z * cos;
    }

    // Unity 좌표를 층 SVG 좌표로. 회전한 imageBounds 네 꼭짓점의 범위를 SVG 전체에 비례로 맞추고 세로는 뒤집는다.
    public static SvgPoint Project(MapDef def, MapLayerDef layer, BriefingPoint p)
    {
        var min = layer.ImageBounds.Min;
        var max = layer.ImageBounds.Max;
        double minX = double.PositiveInfinity, maxX = double.NegativeInfinity, minY = double.PositiveInfinity, maxY = double.NegativeInfinity;
        foreach (var (x, z) in new[] { (min.X, min.Z), (min.X, max.Z), (max.X, min.Z), (max.X, max.Z) })
        {
            Rotate(x, z, def.CoordinateRotation, out var cx, out var cy);
            minX = Math.Min(minX, cx);
            maxX = Math.Max(maxX, cx);
            minY = Math.Min(minY, cy);
            maxY = Math.Max(maxY, cy);
        }
        Rotate(p.X, p.Z, def.CoordinateRotation, out var rx, out var ry);
        return new SvgPoint((rx - minX) / (maxX - minX) * layer.ViewBox.Width, (maxY - ry) / (maxY - minY) * layer.ViewBox.Height);
    }

    private static bool Contains(Bounds3 b, BriefingPoint p) =>
        p.X >= b.Min.X && p.X <= b.Max.X && p.Y >= b.Min.Y && p.Y <= b.Max.Y && p.Z >= b.Min.Z && p.Z <= b.Max.Z;

    private static double Volume(Bounds3 b) => (b.Max.X - b.Min.X) * (b.Max.Y - b.Min.Y) * (b.Max.Z - b.Min.Z);

    // 점을 포함하는 gameBounds 중 부피가 가장 작은 것의 층. 어디에도 없으면 기본 층.
    public static MapLayerDef LayerFor(MapDef def, BriefingPoint p)
    {
        MapLayerDef best = null;
        var bestVolume = double.PositiveInfinity;
        foreach (var layer in def.Layers)
        {
            foreach (var b in layer.GameBounds)
            {
                if (!Contains(b, p) || !(Volume(b) < bestVolume)) continue;
                best = layer;
                bestVolume = Volume(b);
            }
        }
        return best ?? DefaultLayer(def);
    }

    public static MapLayerDef DefaultLayer(MapDef def) => def.Layers.FirstOrDefault(l => l.Level == def.DefaultLevel) ?? def.Layers[0];

    // DynamicMaps MapLayer.OnTopLevelSelected 와 같다. 선택 층 이하는 보이되 한 층 내려갈 때마다 밝기 절반,
    // 위층은 숨기고 기본 층만 0.1 투명도로 남긴다.
    public static LayerLook LayerStyle(int level, int selected, int defaultLevel)
    {
        var brightness = Math.Min(1, Math.Pow(0.5, selected - level));
        if (level <= selected) return new LayerLook(true, brightness, 1);
        if (level == defaultLevel) return new LayerLook(true, brightness, 0.1);
        return new LayerLook(false, brightness, 1);
    }

    // 웹 buildNumberedTabs 와 같다. 응답의 위치는 이미 그 탭 것만 오지만 mapIds 를 주면 다른 맵 위치는 버린다.
    // 짝 맵에서 같은 점·구역이 두 번 올 수 있어 같은 번호 안에서는 한 번만 넣는다.
    public static MarkerSet Numbered(IEnumerable<NumberedObjective> items, ISet<string> mapIds = null)
    {
        var set = new MarkerSet();
        foreach (var item in items)
        {
            var o = item.Objective;
            if (o == null) continue;
            foreach (var loc in o.Locations)
            {
                if (mapIds != null && loc.Map != null && !mapIds.Contains(loc.Map.ToLowerInvariant())) continue;
                foreach (var point in loc.Points)
                {
                    if (point == null) continue;
                    if (!set.Markers.Any(m => m.N == item.N && SamePoint(m.Point, point)))
                        set.Markers.Add(new QuestMarker(item.N, item.QuestId, o.ConditionId, point));
                }
                foreach (var area in loc.Areas)
                {
                    if (area?.Center == null) continue;
                    if (!set.Areas.Any(a => a.N == item.N && SameArea(a.Area, area)))
                        set.Areas.Add(new AreaMarker(item.N, item.QuestId, o.ConditionId, area));
                }
            }
        }
        return set;
    }

    // MapScene 이 끝난 목표의 마커·구역을 찾을 때도 같은 기준을 쓴다.
    internal static bool SamePoint(BriefingPoint a, BriefingPoint b) => a.X == b.X && a.Y == b.Y && a.Z == b.Z;

    internal static bool SameArea(BriefingArea a, BriefingArea b) =>
        SamePoint(a.Center, b.Center) && a.SizeX == b.SizeX && a.SizeZ == b.SizeZ && a.Yaw == b.Yaw;

    // 구역 사각형의 네 꼭짓점(Unity x, z, 둘레 순). Unity 규약대로 위에서 볼 때 양수 yaw 가 시계 방향이다.
    // 로컬 (lx, lz) 는 월드 (lx cos + lz sin, -lx sin + lz cos) 가 된다.
    public static IReadOnlyList<FlatPoint> AreaCorners(BriefingArea area)
    {
        var r = area.Yaw * Math.PI / 180;
        var cos = Math.Cos(r);
        var sin = Math.Sin(r);
        var hx = area.SizeX / 2;
        var hz = area.SizeZ / 2;
        return new[] { (-hx, -hz), (hx, -hz), (hx, hz), (-hx, hz) }
            .Select(c => new FlatPoint(area.Center.X + c.Item1 * cos + c.Item2 * sin, area.Center.Z - c.Item1 * sin + c.Item2 * cos))
            .ToList();
    }

    // 맵 회전은 Project 가 처리한다.
    public static IReadOnlyList<SvgPoint> AreaPolygon(MapDef def, MapLayerDef layer, BriefingArea area) =>
        AreaCorners(area).Select(c => Project(def, layer, new BriefingPoint { X = c.X, Y = area.Center.Y, Z = c.Z })).ToList();

    // 층 버튼에 점을 찍을 층: 지금 층이 아니면서 퀘스트 마커가 있는 층.
    public static ISet<int> FloorsWithOtherMarkers(IReadOnlyDictionary<int, int> counts, int level) =>
        new HashSet<int>(counts.Where(kv => kv.Key != level && kv.Value > 0).Select(kv => kv.Key));

    // 층마다 찍힐 퀘스트 마커 수.
    public static Dictionary<int, int> MarkerCountsByLevel(MapDef def, IEnumerable<QuestMarker> markers, IReadOnlyList<AreaMarker> areas = null)
    {
        var counts = new Dictionary<int, int>();
        foreach (var m in markers)
        {
            foreach (var level in MarkerLevels(def, m, areas ?? Array.Empty<AreaMarker>()))
                counts[level] = (counts.TryGetValue(level, out var n) ? n : 0) + 1;
        }
        return counts;
    }

    // 구역 중심에서 minY~maxY 를 1m 간격으로 짚어 LayerFor 의 층을 모은다. 범위가 없으면 center.y 한 점.
    public static ISet<int> AreaLevels(MapDef def, BriefingArea area)
    {
        var lo = area.MinY ?? area.Center.Y;
        var hi = area.MaxY ?? area.Center.Y;
        var levels = new HashSet<int>();
        void At(double y) => levels.Add(LayerFor(def, new BriefingPoint { X = area.Center.X, Y = y, Z = area.Center.Z }).Level);
        for (var y = lo; y < hi; y += 1) At(y);
        At(hi);
        return levels;
    }

    // 넓은 구역부터 그려서 작은 구역이 위에 보이게 한다. 같은 넓이는 원래 순서.
    public static IReadOnlyList<AreaMarker> AreaDrawOrder(IEnumerable<AreaMarker> areas) =>
        areas.OrderByDescending(a => a.Area.SizeX * a.Area.SizeZ).ToList();

    // 처음 보여 줄 층: 첫 마커가 보이는 층 중 기본 층이 있으면 그것, 없으면 가장 낮은 층. 마커가 없으면 기본 층.
    public static int FirstLevel(MapDef def, IReadOnlyList<QuestMarker> markers, IReadOnlyList<AreaMarker> areas)
    {
        if (markers.Count == 0) return def.DefaultLevel;
        var levels = MarkerLevels(def, markers[0], areas);
        return levels.Contains(def.DefaultLevel) ? def.DefaultLevel : levels.Min();
    }

    // 같은 번호의 구역이 있으면 그 구역들이 걸친 층 전부(점 높이는 무시), 없으면 점이 있는 층 하나.
    public static ISet<int> MarkerLevels(MapDef def, QuestMarker marker, IReadOnlyList<AreaMarker> areas)
    {
        var own = areas.Where(a => a.N == marker.N).ToList();
        if (own.Count == 0) return new HashSet<int> { LayerFor(def, marker.Point).Level };
        var levels = new HashSet<int>();
        foreach (var a in own) levels.UnionWith(AreaLevels(def, a.Area));
        return levels;
    }

    public static MapViewport FitView() => new MapViewport(1, 0, 0);

    // 화면 좌표 (cx, cy) 아래의 지점을 고정한 채 factor 배 확대·축소한다(배율 1~12). 1배로 돌아오면 이동도 초기화한다.
    public static MapViewport ZoomAt(MapViewport v, double factor, double cx, double cy)
    {
        var scale = Math.Min(MaxScale, Math.Max(MinScale, v.Scale * factor));
        if (scale == MinScale) return FitView();
        var k = scale / v.Scale;
        return new MapViewport(scale, cx - (cx - v.X) * k, cy - (cy - v.Y) * k);
    }
}
