using System.Collections.Generic;
using Briefing.Core.Map;
using Briefing.Core.Screen;

namespace Briefing.Ui;

// 화면 상태. 게임을 끌 때까지 남아서 캐릭터 화면을 닫았다 열어도 고른 맵과 접힌 묶음이 그대로다.
// 지도에서 선택한 퀘스트와 오른쪽 열 필터 퀘스트는 펼친 줄의 퀘스트와 같다.
internal static class BriefingState
{
    internal static string SelectedMapKey;                              // null 이면 LeftColumn.DefaultMapKey
    internal static readonly HashSet<string> Collapsed = new HashSet<string>(); // 접힌 묶음 키

    // 펼친 줄은 하나뿐이다. 같은 퀘스트가 두 묶음에 있을 수 있어 묶음 키도 같이 둔다.
    internal static string ExpandedSection;
    internal static string ExpandedQuestId;

    // 층과 확대는 MapKey 지도의 것이다. 맵을 바꾸면 UseMap 이 기본 층과 원래 크기로 되돌린다.
    internal static string MapKey;
    internal static int? MapLevel;                                     // null 이면 기본 층
    internal static MapViewport MapZoom = MapProjection.FitView();
    // 레이어 체크박스는 맵을 바꿔도 그대로다.
    internal static bool ShowExitPmc = true;
    internal static bool ShowExitScav = true;
    internal static bool ShowExitTransit = true;
    internal static bool ShowDoors = true;
    internal static bool ShowAreas = true;
    // 마우스를 올린 퀘스트(맵 묶음의 줄이나 지도 마커). 지도 강조에서 선택한 퀘스트보다 먼저다.
    internal static string HotQuestId;

    // 오른쪽 열에서 고른 탭. 저절로 바뀌지 않는다.
    internal static RightTabKey RightTab = RightTabKey.Bring;
    // 열린 "외 N종" 상자(GearLineModel.Key). 맵을 바꾸면 모두 닫힌다.
    internal static readonly HashSet<string> RightOpenRows = new HashSet<string>();
    // 필터 띠의 ✕ 로 필터만 껐다. 펼침과 지도 강조는 그대로고, 펼친 줄이 바뀌면 풀린다.
    internal static bool RightFilterOff;

    // 이번 레이드에서 고른 맵을 레이드 맵으로 한 번 옮겼다(못 찾았어도 시도했으면 true). 그 뒤 다른 칩을 눌러도 다시 옮기지 않는다.
    // RaidStartPatch 가 지운다. 레이드가 끝나도 고른 맵은 되돌리지 않는다.
    internal static bool RaidMapPicked;

    internal static void UseMap(string key)
    {
        if (MapKey == key) return;
        MapKey = key;
        MapLevel = null;
        MapZoom = MapProjection.FitView();
    }

    // 같은 줄을 다시 누르면 접고, 다른 줄이면 그 줄을 펼친다. 어느 쪽이든 필터 끔은 풀린다.
    internal static void ToggleExpanded(string section, string questId)
    {
        RightFilterOff = false;
        if (ExpandedSection == section && ExpandedQuestId == questId)
        {
            ExpandedSection = null;
            ExpandedQuestId = null;
            return;
        }
        ExpandedSection = section;
        ExpandedQuestId = questId;
    }
}
