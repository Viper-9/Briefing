using System;
using Comfort.Common;
using EFT;
using EFT.UI;

namespace Briefing.Game;

// TasksPanel.InRaid() 는 지금 게임이 LocalGame 인지 본다. 메뉴와 은신처에서는 거짓.
// 못 읽으면 레이드 밖으로 본다. 요청과 미리 그려 두기가 도는 편이 화면이 비는 것보다 낫다.
internal static class RaidInfo
{
    internal static bool InRaid()
    {
        try { return TasksPanel.InRaid(); }
        catch (Exception) { return false; }
    }

    // MainPlayer.Location 은 GameWorld.LocationId 와 같다(예: factory4_day). 은신처는 "hideout".
    internal static string LocationId()
    {
        try { return Singleton<GameWorld>.Instance?.MainPlayer?.Location; }
        catch (Exception) { return null; }
    }
}
