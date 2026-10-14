using System.Collections.Generic;
using System.Linq;
using Briefing.Core.Contract;
using Briefing.Core.Map;

namespace Briefing.Tests;

// 미리 그려 둘 맵 폴더와 층 순서.
public class MapPrecachePlanTests
{
    private static BriefingMap Map(string key, bool hasMap, string folder) =>
        new BriefingMap { Key = key, HasMap = hasMap, MapFolder = folder };

    private static BriefingResponse Response(params BriefingMap[] maps) =>
        new BriefingResponse { Maps = maps.ToList() };

    [Fact]
    public void Folders_skip_maps_without_a_map_or_a_folder_in_chip_order()
    {
        var response = Response(
            Map("a", true, "bigmap"),
            Map("b", false, "shoreline"),
            Map("c", true, null),
            Map("d", true, ""),
            Map("e", true, "woods"));

        Assert.Equal(new[] { "bigmap", "woods" }, MapPrecachePlan.Folders(response, null));
    }

    [Fact]
    public void Folders_leave_out_the_current_folder_and_repeat_each_folder_once()
    {
        var response = Response(
            Map("a", true, "bigmap"),
            Map("b", true, "woods"),
            Map("c", true, "bigmap"),
            Map("d", true, "lighthouse"),
            Map("e", true, "woods"));

        Assert.Equal(new[] { "woods", "lighthouse" }, MapPrecachePlan.Folders(response, "bigmap"));
    }

    [Fact]
    public void Folders_are_empty_without_a_response_or_maps()
    {
        Assert.Empty(MapPrecachePlan.Folders(null, null));
        Assert.Empty(MapPrecachePlan.Folders(Response(), "bigmap"));
    }

    [Fact]
    public void Layers_come_default_level_first_then_by_level()
    {
        var def = new MapDef
        {
            DefaultLevel = 0,
            Layers = new List<MapLayerDef>
            {
                new MapLayerDef { Level = 1, Svg = "up.svg" },
                new MapLayerDef { Level = -1, Svg = "down.svg" },
                new MapLayerDef { Level = 0, Svg = "main.svg" },
                new MapLayerDef { Level = 2, Svg = "top.svg" },
            },
        };

        Assert.Equal(new[] { "main.svg", "down.svg", "up.svg", "top.svg" }, MapPrecachePlan.LayerOrder(def).Select(l => l.Svg));
    }
}
