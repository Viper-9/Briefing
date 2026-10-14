namespace Briefing.Core.Screen;

public enum CheckState
{
    None,
    Some,
    All,
}

// 지도 칸 오른쪽 위 체크박스 줄을 세로로 쌓을지 정한다. 단위는 캔버스 기준 해상도.
// 여백, 층 버튼(있으면 여백 포함), 체크박스 줄, 여백을 더해 칸 폭을 넘으면 쌓는다.
// 칸 폭을 모르면 가로 그대로 둔다.
public static class MapControls
{
    public const float Margin = 14f;

    public static bool Stack(float columnWidth, float floorsWidth, float togglesRowWidth, float margin)
    {
        if (float.IsNaN(columnWidth) || float.IsInfinity(columnWidth) || columnWidth <= 0) return false;
        var floors = Known(floorsWidth) ? floorsWidth : 0f;
        var row = Known(togglesRowWidth) ? togglesRowWidth : 0f;
        var used = margin + (floors > 0 ? floors + margin : 0f) + row + margin;
        return used > columnWidth;
    }

    // 탈출구 드롭다운 버튼의 체크 모양. 일부만 켜져 있으면 Some(흐린 체크).
    public static CheckState ExitsCheck(bool pmc, bool scav, bool transit)
    {
        var on = (pmc ? 1 : 0) + (scav ? 1 : 0) + (transit ? 1 : 0);
        return on == 3 ? CheckState.All : on == 0 ? CheckState.None : CheckState.Some;
    }

    private static bool Known(float v) => !float.IsNaN(v) && !float.IsInfinity(v) && v > 0;
}
