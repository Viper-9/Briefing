using System;

namespace Briefing.Core.Screen;

// 열 폭 조절 계산. 단위는 캔버스 기준 해상도.
// total 은 세 열과 간격 두 개를 합친 폭이다. 0 이하나 NaN 이면 아직 모르는 것으로 보고 지도 칸 최소 폭 제한을 건너뛴다.
public static class ColumnWidths
{
    public const float DefaultLeft = 540f;
    public const float DefaultRight = 470f;
    public const float MinLeft = 360f;
    public const float MinRight = 300f;
    public const float MinMap = 400f;
    public const float Gap = 14f; // 프리팹 HorizontalLayoutGroup 의 spacing

    // 저장된 값이 이상하면 기본 폭, 최소 폭보다 작으면 최소 폭.
    // 지도 칸 최소 폭이 안 남으면 오른쪽 열부터, 그래도 모자라면 왼쪽 열을 줄인다. 둘 다 최소인데도 모자라면 지도 칸이 좁아진다.
    public static (float Left, float Right) Resolve(float savedLeft, float savedRight, float total)
    {
        var left = Sane(savedLeft, DefaultLeft, MinLeft);
        var right = Sane(savedRight, DefaultRight, MinRight);
        if (!Known(total)) return (left, right);
        var room = total - 2 * Gap - MinMap;
        var excess = left + right - room;
        if (excess > 0)
        {
            right = Math.Max(MinRight, right - excess);
            excess = left + right - room;
            if (excess > 0) left = Math.Max(MinLeft, left - excess);
        }
        return (left, right);
    }

    // 왼쪽 최소 폭과 지도 칸 최소 폭에서 멈춘다.
    public static float DragLeft(float left, float right, float total, float dx)
    {
        var max = Known(total) ? total - 2 * Gap - right - MinMap : float.PositiveInfinity;
        return Clamp(left + dx, MinLeft, max);
    }

    // 오른쪽 손잡이는 오른쪽으로 끌수록 열이 좁아진다.
    public static float DragRight(float left, float right, float total, float dx)
    {
        var max = Known(total) ? total - 2 * Gap - left - MinMap : float.PositiveInfinity;
        return Clamp(right - dx, MinRight, max);
    }

    private static bool Known(float total) => !float.IsNaN(total) && !float.IsInfinity(total) && total > 0;

    private static float Sane(float saved, float fallback, float min)
    {
        if (float.IsNaN(saved) || float.IsInfinity(saved) || saved <= 0) return fallback;
        return Math.Max(saved, min);
    }

    // 자리가 없어 상한이 최소보다 작아도 최소 폭은 지킨다.
    private static float Clamp(float value, float min, float max) => Math.Max(min, Math.Min(value, max));
}
