using System;
using System.Linq;
using Briefing.Core.Contract;

namespace Briefing.Core.Screen;

// MainPlayer.Location(GameWorld.LocationId, 예: factory4_day)을 응답의 maps[].locationIds 와 대소문자 없이 맞춰 맵 키를 찾는다.
// 야간 공장 같은 변형 맵은 서버가 같은 탭의 locationIds 에 넣어 준다.
// 은신처에도 GameWorld 가 있지만 Location 이 hideout 이라 레이드로 보지 않는다. 못 찾으면 null.
public static class RaidMap
{
    public const string Hideout = "hideout";

    public static string Match(BriefingResponse response, string locationId)
    {
        if (response == null || string.IsNullOrWhiteSpace(locationId)) return null;
        var id = locationId.Trim();
        if (string.Equals(id, Hideout, StringComparison.OrdinalIgnoreCase)) return null;
        return response.Maps.FirstOrDefault(m => m != null
            && m.LocationIds.Any(l => string.Equals(l, id, StringComparison.OrdinalIgnoreCase)))?.Key;
    }
}
