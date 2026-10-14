using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Briefing.Core.Contract;
using Briefing.Core.Screen;

namespace Briefing.Core.Map;

// 지도 위 위치를 Content 크기에 대한 비율로 둔다(왼쪽 위 0,0, 오른쪽 아래 1,1). 뷰는 Content 크기만 곱하면 된다.
public readonly struct MapUv
{
    public MapUv(double u, double v)
    {
        U = u;
        V = v;
    }

    public double U { get; }
    public double V { get; }
}

// 강조할 퀘스트가 없으면 전부 Normal, 있으면 그 퀘스트는 Hot 이고 나머지는 Dim.
public enum Emphasis
{
    Normal,
    Hot,
    Dim,
}

public sealed class MarkerDraw
{
    public MarkerDraw(int n, string questId, string color, MapUv at, Emphasis emphasis)
    {
        N = n;
        QuestId = questId;
        Color = color;
        At = at;
        Emphasis = emphasis;
    }

    public int N { get; }
    public string QuestId { get; }
    public string Color { get; }
    public MapUv At { get; }
    public Emphasis Emphasis { get; }
}

// 꼭짓점은 기본 층 viewBox 기준 비율이다. 웹도 구역을 기본 층 크기의 SVG 한 장에 그린다.
public sealed class AreaDraw
{
    public AreaDraw(int n, string questId, string color, IReadOnlyList<MapUv> polygon, Emphasis emphasis)
    {
        N = n;
        QuestId = questId;
        Color = color;
        Polygon = polygon;
        Emphasis = emphasis;
    }

    public int N { get; }
    public string QuestId { get; }
    public string Color { get; }
    public IReadOnlyList<MapUv> Polygon { get; }
    public Emphasis Emphasis { get; }
}

// OtherFloor 면 다른 층의 탈출구라 흐리게 그린다. Detail 은 없으면 null.
public sealed class ExitDraw
{
    public ExitDraw(string kind, MapUv at, bool otherFloor, string title, string detail)
    {
        Kind = kind;
        At = at;
        OtherFloor = otherFloor;
        Title = title;
        Detail = detail;
    }

    public string Kind { get; }
    public MapUv At { get; }
    public bool OtherFloor { get; }
    public string Title { get; }
    public string Detail { get; }
}

public sealed class DoorDraw
{
    public DoorDraw(MapUv at, string label)
    {
        At = at;
        Label = label;
    }

    public MapUv At { get; }
    public string Label { get; }
}

public sealed class FloorDraw
{
    public FloorDraw(int level, string name, bool selected, bool dot)
    {
        Level = level;
        Name = name;
        Selected = selected;
        Dot = dot;
    }

    public int Level { get; }
    public string Name { get; }
    public bool Selected { get; }
    public bool Dot { get; } // 현재 층이 아니면서 퀘스트 마커가 있는 층
}

// 선택한 퀘스트로 옮길 곳. 층과, 그 층에 보이는 그 퀘스트의 마커·구역 꼭짓점을 감싸는 상자(비율).
public sealed class MapFocus
{
    public MapFocus(int level, double minU, double minV, double maxU, double maxV)
    {
        Level = level;
        MinU = minU;
        MinV = minV;
        MaxU = maxU;
        MaxV = maxV;
    }

    public int Level { get; }
    public double MinU { get; }
    public double MinV { get; }
    public double MaxU { get; }
    public double MaxV { get; }
}

public sealed class MapSceneInput
{
    public BriefingResponse Response { get; set; }
    public BriefingMap Map { get; set; }
    public MapDef Def { get; set; }
    public ISet<string> ShownQuestIds { get; set; } // 왼쪽 맵 묶음에 보이는 퀘스트. null 이면 map.Quests 전부
    public int? Level { get; set; }                 // null 이거나 이 지도에 없는 층이면 기본 층
    public bool ShowExitPmc { get; set; } = true;     // shared 와 모르는 종류는 PMC 나 스캐브 중 하나라도 켜져 있으면 보인다
    public bool ShowExitScav { get; set; } = true;
    public bool ShowExitTransit { get; set; } = true;
    public bool ShowDoors { get; set; } = true;
    public bool ShowAreas { get; set; } = true;
    public string EmphasisQuestId { get; set; }
}

// 아래에서부터 지도, 구역, 잠긴 문, 탈출구, 퀘스트 마커 순으로 쌓인다. Signature 는 강조를 뺀 모양이다.
// 마우스를 올릴 때마다 오버레이를 새로 만들지 않도록, Signature 가 같으면 뷰는 색과 알파만 바꾼다.
public sealed class MapSceneModel
{
    public int Level { get; set; }
    public IReadOnlyList<FloorDraw> Floors { get; set; }
    public IReadOnlyList<AreaDraw> Areas { get; set; }
    public IReadOnlyList<DoorDraw> Doors { get; set; }
    public IReadOnlyList<ExitDraw> Exits { get; set; }
    public IReadOnlyList<MarkerDraw> Markers { get; set; }
    public string Credit { get; set; }
    public string Signature { get; set; }
}

// 좌표 계산은 MapProjection 이 하고, 여기서는 그릴 것을 고르고 화면용 모양으로 바꾼다.
public static class MapScene
{
    // 마우스를 올린 퀘스트, 없으면 맵 묶음에서 펼친 퀘스트. 펼친 채로 맵을 바꾸면 그 퀘스트가 이 지도에 없을 수 있는데,
    // 그때 강조하면 모든 마커가 흐려지므로 강조하지 않는다.
    public static string EmphasisQuest(string hotQuestId, string expandedSection, string expandedQuestId, ISet<string> shownQuestIds = null)
    {
        var quest = hotQuestId ?? (expandedSection == LeftColumn.HereKey ? expandedQuestId : null);
        return quest != null && (shownQuestIds == null || shownQuestIds.Contains(quest)) ? quest : null;
    }

    // 맵 묶음 퀘스트 중 마커 번호가 있는 것의 목표 위치를 마커·구역으로.
    public static MarkerSet Markers(BriefingMap map, ISet<string> shownQuestIds)
    {
        var mapIds = new HashSet<string>(map.LocationIds.Where(id => id != null).Select(id => id.ToLowerInvariant()));
        if (map.Key != null) mapIds.Add(map.Key.ToLowerInvariant());
        var items = map.Quests
            .Where(q => q.Marker.HasValue && (shownQuestIds == null || shownQuestIds.Contains(q.QuestId)))
            .SelectMany(q => q.Objectives.Select(o => new NumberedObjective(q.Marker.Value, q.QuestId, o)));
        return MapProjection.Numbered(items, mapIds);
    }

    public static int LevelOf(MapDef def, int? chosen) =>
        chosen.HasValue && def.Layers.Any(l => l.Level == chosen.Value) ? chosen.Value : def.DefaultLevel;

    public static MapSceneModel Build(MapSceneInput input)
    {
        var def = input.Def;
        var map = input.Map;
        var level = LevelOf(def, input.Level);
        var set = Markers(map, input.ShownQuestIds);
        var hot = input.EmphasisQuestId;
        // 레이드 중 끝난 목표만 가리키는 마커·구역은 흐리게. 메뉴에서는 끝난 목표가 없으니 검사를 건너뛴다.
        var anyDone = map.Quests.Any(q => q != null && q.Objectives.Any(o => o != null && o.Done));

        var markers = set.Markers
            .Where(m => MapProjection.MarkerLevels(def, m, set.Areas).Contains(level))
            .Select(m => new MarkerDraw(m.N, m.QuestId, MarkerPalette.Hex(m.N), Place(def, m.Point),
                Look(hot, m.QuestId, anyDone && AllDone(map, m.QuestId, o => o.Locations.Any(l => l.Points.Any(p => p != null && MapProjection.SamePoint(p, m.Point)))))))
            .ToList();
        var basis = MapProjection.DefaultLayer(def);
        var areas = !input.ShowAreas
            ? new List<AreaDraw>()
            : MapProjection.AreaDrawOrder(set.Areas.Where(a => MapProjection.AreaLevels(def, a.Area).Contains(level)))
                .Select(a => new AreaDraw(a.N, a.QuestId, MarkerPalette.Hex(a.N),
                    MapProjection.AreaPolygon(def, basis, a.Area).Select(p => Ratio(basis, p)).ToList(),
                    Look(hot, a.QuestId, anyDone && AllDone(map, a.QuestId, o => o.Locations.Any(l => l.Areas.Any(x => x?.Center != null && MapProjection.SameArea(x, a.Area)))))))
                .ToList();
        var doors = !input.ShowDoors
            ? new List<DoorDraw>()
            : map.LockedDoors.Where(d => d?.Position != null && MapProjection.LayerFor(def, d.Position).Level == level)
                .Select(d => new DoorDraw(Place(def, d.Position), DoorLabel(d)))
                .ToList();
        // 탈출구는 층과 상관없이 다 그린다. 다른 층 것을 먼저 그려 지금 층 것이 위에 오게 한다.
        var exits = map.Exits.Where(e => e?.Position != null && ExitShown(e.Kind, input))
            .Select(e => new ExitDraw(e.Kind, Place(def, e.Position), MapProjection.LayerFor(def, e.Position).Level != level,
                ExitTitle(e, input.Response), ExitDetail(e)))
            .OrderBy(e => e.OtherFloor ? 0 : 1)
            .ToList();

        var levels = def.Layers.Select(l => l.Level).Distinct().OrderByDescending(l => l).ToList();
        var dots = MapProjection.FloorsWithOtherMarkers(MapProjection.MarkerCountsByLevel(def, set.Markers, set.Areas), level);
        var floors = levels.Count < 2
            ? new List<FloorDraw>()
            : levels.Select(l => new FloorDraw(l, Texts.FloorName(l, def.FloorNames), l == level, dots.Contains(l))).ToList();

        var a = def.Attribution;
        return new MapSceneModel
        {
            Level = level,
            Floors = floors,
            Areas = areas,
            Doors = doors,
            Exits = exits,
            Markers = markers,
            Credit = string.IsNullOrEmpty(a?.Author) ? null : Texts.MapCredit(a.Author, a.License),
            Signature = Signature(map, level, input, markers, areas, doors, exits),
        };
    }

    // 층은 그 퀘스트 마커가 보이는 층 중 기본 층, 없으면 가장 낮은 층(웹 firstLevel 과 같다). 마커가 없으면 null.
    // set 은 Build 와 같은 필터로 만든 Markers(map, shownQuestIds) 여야 지도에 보이는 마커와 맞는다.
    public static MapFocus Focus(MapDef def, MarkerSet set, string questId)
    {
        var markers = set.Markers.Where(m => m.QuestId == questId).ToList();
        if (markers.Count == 0) return null;
        var areas = set.Areas.Where(a => a.QuestId == questId).ToList();
        var levels = new HashSet<int>(markers.SelectMany(m => MapProjection.MarkerLevels(def, m, areas)));
        var level = levels.Contains(def.DefaultLevel) ? def.DefaultLevel : levels.Min();
        var basis = MapProjection.DefaultLayer(def);
        var points = markers.Where(m => MapProjection.MarkerLevels(def, m, areas).Contains(level)).Select(m => Place(def, m.Point))
            .Concat(areas.Where(a => MapProjection.AreaLevels(def, a.Area).Contains(level))
                .SelectMany(a => MapProjection.AreaPolygon(def, basis, a.Area).Select(p => Ratio(basis, p))))
            .ToList();
        return new MapFocus(level, points.Min(p => p.U), points.Min(p => p.V), points.Max(p => p.U), points.Max(p => p.V));
    }

    // 초점 상자가 보이게 시점을 옮긴다. 확대는 하지 않는다. width·height 는 1배 지도(Content) 크기, viewport 는 칸 크기.
    // Content 가 칸 가운데에 있으므로 보이는 범위는 Content 좌표로 양쪽에 (칸 - Content)/2 만큼 넓다.
    // 상자가 여백을 뺀 보이는 범위보다 크면 축소해서 가운데에 두고, 아니면 넘친 쪽만 조금 민다. 1배까지 줄면 FitView.
    public static MapViewport Reveal(MapViewport v, MapFocus focus, double width, double height,
        double viewportWidth, double viewportHeight, double margin = 40)
    {
        if (focus == null || v.Scale <= MapProjection.MinScale) return v;
        double left = -(viewportWidth - width) / 2, top = -(viewportHeight - height) / 2;
        double right = width - left, bottom = height - top;
        double availW = right - left - 2 * margin, availH = bottom - top - 2 * margin;
        double x0 = focus.MinU * width, x1 = focus.MaxU * width, y0 = focus.MinV * height, y1 = focus.MaxV * height;
        var scale = v.Scale;
        if ((x1 - x0) * scale > availW || (y1 - y0) * scale > availH)
        {
            var fitW = x1 > x0 ? availW / (x1 - x0) : scale;
            var fitH = y1 > y0 ? availH / (y1 - y0) : scale;
            scale = Math.Min(scale, Math.Min(fitW, fitH));
            if (scale <= MapProjection.MinScale) return MapProjection.FitView();
            return new MapViewport(scale, (left + right) / 2 - (x0 + x1) / 2 * scale, (top + bottom) / 2 - (y0 + y1) / 2 * scale);
        }
        return new MapViewport(scale, Shift(v.X, x0 * scale, x1 * scale, left + margin, right - margin),
            Shift(v.Y, y0 * scale, y1 * scale, top + margin, bottom - margin));
    }

    // offset + [lo, hi] 가 [min, max] 안에 들도록 offset 을 필요한 만큼만 민다.
    private static double Shift(double offset, double lo, double hi, double min, double max)
    {
        if (offset + lo < min) return min - lo;
        if (offset + hi > max) return max - hi;
        return offset;
    }

    private static bool ExitShown(string kind, MapSceneInput input)
    {
        switch (kind)
        {
            case "pmc": return input.ShowExitPmc;
            case "scav": return input.ShowExitScav;
            case "transit": return input.ShowExitTransit;
            default: return input.ShowExitPmc || input.ShowExitScav; // shared·모르는 종류
        }
    }

    // 강조 중인 퀘스트는 끝난 목표여도 Hot. 나머지는 강조 대상이 있거나 끝난 목표면 Dim.
    private static Emphasis Look(string hot, string questId, bool done = false) =>
        hot != null && hot == questId ? Emphasis.Hot : hot != null || done ? Emphasis.Dim : Emphasis.Normal;

    // 이 마커(구역)를 쓰는 목표가 모두 끝났는지. Hobby Club 처럼 한 점을 목표 여럿이 쓰면 다 끝나야 흐린다.
    private static bool AllDone(BriefingMap map, string questId, Func<BriefingObjective, bool> uses)
    {
        var quest = map.Quests.FirstOrDefault(q => q?.QuestId == questId);
        var users = quest?.Objectives.Where(o => o != null && uses(o)).ToList();
        return users != null && users.Count > 0 && users.All(o => o.Done);
    }

    // 점이 속한 층의 SVG 기준 비율 위치(웹 MapCanvas.place)
    private static MapUv Place(MapDef def, BriefingPoint point)
    {
        var layer = MapProjection.LayerFor(def, point);
        return Ratio(layer, MapProjection.Project(def, layer, point));
    }

    private static MapUv Ratio(MapLayerDef layer, SvgPoint p) => new MapUv(p.X / layer.ViewBox.Width, p.Y / layer.ViewBox.Height);

    // "이름 · 종류". 환승이면 목적지 맵 키를 locationIds 에 가진 탭 이름으로 "환승 → 맵".
    private static string ExitTitle(BriefingExit exit, BriefingResponse response)
    {
        if (exit.Kind == "transit")
        {
            var target = exit.Target?.ToLowerInvariant();
            var name = response?.Maps.FirstOrDefault(m => m.Key?.ToLowerInvariant() == target || m.LocationIds.Any(id => id?.ToLowerInvariant() == target))?.Name;
            return Texts.TransitTo(name ?? exit.Target ?? "?");
        }
        var label = string.IsNullOrEmpty(exit.Name) ? exit.Key : exit.Name;
        return $"{label} · {Texts.ExitKind(exit.Kind)}";
    }

    // "조건 · 등장 확률 n%". 조건은 서버 문장이 있으면 그것, 없으면 requirementKind 문구. 둘 다 없으면 null.
    private static string ExitDetail(BriefingExit exit)
    {
        var parts = new List<string>();
        var requirement = !string.IsNullOrEmpty(exit.Requirement) ? exit.Requirement : Texts.ExitRequirement(exit.RequirementKind);
        if (requirement != null) parts.Add(requirement);
        if (exit.Chance.HasValue) parts.Add(Texts.ExitChance(exit.Chance.Value));
        return parts.Count == 0 ? null : string.Join(" · ", parts);
    }

    // 웹 doorLabel 과 같은 규칙.
    private static string DoorLabel(BriefingDoor door)
    {
        switch (door.Kind)
        {
            case "keycard": return $"{Texts.Keycard} · {door.KeyName}";
            case "keypad": return string.IsNullOrEmpty(door.Code) ? Texts.Keypad : $"{Texts.Keypad} · {Texts.KeypadCode(door.Code)}";
            case "explosive": return Texts.ExplosiveDoor(door.KeyName);
            case "hatch": return Texts.FrozenHatch(door.KeyName);
            default: return door.KeyName ?? "";
        }
    }

    private static string Signature(BriefingMap map, int level, MapSceneInput input, IEnumerable<MarkerDraw> markers,
        IReadOnlyCollection<AreaDraw> areas, IReadOnlyCollection<DoorDraw> doors, IReadOnlyCollection<ExitDraw> exits)
    {
        var c = CultureInfo.InvariantCulture;
        var marks = string.Join(",", markers.Select(m => string.Format(c, "{0}:{1}:{2:R}:{3:R}", m.QuestId, m.N, m.At.U, m.At.V)));
        // 구역·문·탈출구는 개수만이 아니라 위치와 말풍선 문구까지 넣는다. 새 응답에서 그것만 바뀌어도 다시 만들어야 한다.
        var zones = string.Join(",", areas.Select(a => string.Format(c, "{0}:{1}:{2}", a.QuestId, a.N,
            string.Join(";", a.Polygon.Select(p => string.Format(c, "{0:R}:{1:R}", p.U, p.V))))));
        var locks = string.Join(",", doors.Select(d => string.Format(c, "{0:R}:{1:R}:{2}", d.At.U, d.At.V, d.Label)));
        var outs = string.Join(",", exits.Select(e => string.Format(c, "{0}:{1:R}:{2:R}:{3}:{4}:{5}",
            e.Kind, e.At.U, e.At.V, e.OtherFloor ? 1 : 0, e.Title, e.Detail)));
        return string.Format(c, "{0}|{1}|{2}{3}{4}{5}{6}|{7}|{8}|{9}|{10}|{11}",
            map.Key, level, input.ShowExitPmc ? 1 : 0, input.ShowExitScav ? 1 : 0, input.ShowExitTransit ? 1 : 0, input.ShowDoors ? 1 : 0, input.ShowAreas ? 1 : 0, Texts.Language,
            zones, locks, outs, marks);
    }
}
