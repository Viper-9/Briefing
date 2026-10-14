using System;
using System.Collections.Generic;
using System.Linq;
using Briefing.Core.Contract;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Briefing.Core.Map;

// QuestCodex 가 웹에 내주는 지도 정의(/questcodex/maps/{mapFolder}/map.json). 웹 mapProjection.ts 의 MapDef 와 같은 모양이다.
// 축은 이미 Unity 기준(y 가 높이)이다. 웹이 쓰지 않는 bounds·source 는 읽지 않는다.

public sealed class MapDef
{
    public string DisplayName { get; set; }
    public List<string> InternalNames { get; set; } = new List<string>();
    public double CoordinateRotation { get; set; }
    public int DefaultLevel { get; set; }
    public string FloorNames { get; set; } // null 이면 지하·지상·n층, "deck" 이면 데크 n
    public List<MapLayerDef> Layers { get; set; } = new List<MapLayerDef>();
    public MapAttribution Attribution { get; set; }
}

public sealed class MapLayerDef
{
    public string Name { get; set; }
    public int Level { get; set; }
    public string Svg { get; set; }
    public MapSize ViewBox { get; set; }
    public FlatBounds ImageBounds { get; set; }
    public List<Bounds3> GameBounds { get; set; } = new List<Bounds3>();
}

public sealed class MapSize
{
    public double Width { get; set; }
    public double Height { get; set; }
}

// 위에서 본 평면 좌표(Unity x, z)
public sealed class FlatPoint
{
    public FlatPoint() { }
    public FlatPoint(double x, double z)
    {
        X = x;
        Z = z;
    }

    public double X { get; set; }
    public double Z { get; set; }
}

public sealed class FlatBounds
{
    public FlatPoint Min { get; set; }
    public FlatPoint Max { get; set; }
}

public sealed class Bounds3
{
    public BriefingPoint Min { get; set; }
    public BriefingPoint Max { get; set; }
}

public sealed class MapAttribution
{
    public string Author { get; set; }
    public string AuthorLink { get; set; }
    public string ModifiedBy { get; set; }
    public string License { get; set; }
    public string LicenseFile { get; set; }
    public string Calibration { get; set; }
}

// svg·viewBox·imageBounds 가 없거나 폭이 0 인 층과 모양이 틀린 gameBounds 는 버린다.
// 남는 층이 없으면 null 이고 지도 칸은 불러오기 실패 문구를 띄운다. 어떤 입력에도 예외를 던지지 않는다.
public static class MapDefParser
{
    private static readonly JsonSerializer Serializer = JsonSerializer.Create(new JsonSerializerSettings
    {
        MissingMemberHandling = MissingMemberHandling.Ignore,
        NullValueHandling = NullValueHandling.Ignore,
        ObjectCreationHandling = ObjectCreationHandling.Replace,
    });

    public static MapDef Parse(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            if (!(JToken.Parse(json) is JObject root)) return null;
            var def = root.ToObject<MapDef>(Serializer);
            def.Layers = def.Layers.Where(Usable).ToList();
            foreach (var layer in def.Layers) layer.GameBounds = layer.GameBounds.Where(b => b?.Min != null && b.Max != null).ToList();
            return def.Layers.Count == 0 ? null : def;
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static bool Usable(MapLayerDef l) =>
        l != null && !string.IsNullOrEmpty(l.Svg)
        && l.ViewBox != null && l.ViewBox.Width > 0 && l.ViewBox.Height > 0
        && l.ImageBounds?.Min != null && l.ImageBounds.Max != null
        && l.ImageBounds.Max.X != l.ImageBounds.Min.X && l.ImageBounds.Max.Z != l.ImageBounds.Min.Z;
}
