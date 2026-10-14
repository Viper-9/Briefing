using System.Collections.Generic;
using System.Linq;
using Briefing.Core.Contract;

namespace Briefing.Core.Map;

// 지도를 미리 그려 둘 때 무엇을 어떤 순서로 받을지 정한다.
public static class MapPrecachePlan
{
    // 지도가 있는 맵의 폴더를 맵 칩 순서대로, 겹치지 않게. 지금 보고 있는 폴더는 뺀다.
    public static IReadOnlyList<string> Folders(BriefingResponse response, string currentFolder)
    {
        var result = new List<string>();
        if (response == null) return result;
        foreach (var map in response.Maps)
        {
            var folder = map.MapFolder;
            if (!map.HasMap || string.IsNullOrEmpty(folder) || folder == currentFolder || result.Contains(folder)) continue;
            result.Add(folder);
        }
        return result;
    }

    // 기본 층 먼저, 나머지는 층 번호 순. MapView 와 미리 그려 두기가 같은 순서를 쓴다.
    public static IReadOnlyList<MapLayerDef> LayerOrder(MapDef def) =>
        def.Layers.OrderBy(l => l.Level == def.DefaultLevel ? 0 : 1).ThenBy(l => l.Level).ToList();
}
