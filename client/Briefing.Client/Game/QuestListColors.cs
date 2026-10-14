using System;
using EFT.UI;
using UnityEngine;

namespace Briefing.Game;

// 상인 임무 목록 줄(QuestListItem)의 색표를 읽어 왼쪽 열 줄에 같은 색을 쓴다.
// 완료 가능은 푸른 바탕, 고른 줄은 밝은 회색 바탕에 어두운 글자. 못 찾으면 스크린샷에서 잰 값을 쓰고 5초 뒤 다시 찾는다.
internal static class QuestListColors
{
    internal static Color TurnInBg { get; private set; } = new Color32(85, 156, 195, 60);
    internal static Color SelectedBg { get; private set; } = new Color32(159, 158, 145, 255);
    internal static Color SelectedText { get; private set; } = Color.black;

    private static bool _found;
    private static bool _warned;
    private static float _nextSearch;

    internal static void Refresh()
    {
        if (_found || Time.realtimeSinceStartup < _nextSearch) return;
        _nextSearch = Time.realtimeSinceStartup + 5f;
        try
        {
            foreach (var item in Resources.FindObjectsOfTypeAll<QuestListItem>())
            {
                if (item == null || item._backColors == null || item._textColors == null) continue;
                TurnInBg = Pick(item._backColors, "AvailableForFinish", TurnInBg);
                SelectedBg = Pick(item._backColors, "Selected", SelectedBg);
                SelectedText = Pick(item._textColors, "Selected", SelectedText);
                _found = true;
                return;
            }
            if (!_warned) { _warned = true; Log.Warn("briefing quest list colors not found; using defaults (will retry)"); }
        }
        catch (Exception e) { Log.Error($"briefing quest list colors lookup failed: {e}"); }
    }

    // ColorMap 인덱서는 없는 키에 검정을 돌려주므로 직접 찾는다.
    private static Color Pick(ColorMap map, string key, Color fallback)
    {
        if (map._items == null) return fallback;
        foreach (var d in map._items)
            if (d != null && d.Id == key) return d.Item;
        return fallback;
    }
}
