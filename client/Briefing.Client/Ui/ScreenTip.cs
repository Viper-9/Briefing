using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Briefing.Ui;

// 프리팹 RightTooltip 말풍선 하나를 3열 아이템 칸과 1열 처치 대상 줄이 같이 쓴다.
// 줄 글은 부른 쪽이 SetLine 으로 채우고(값이 없으면 그 줄을 끈다) Show 가 폭을 맞춰 대상 옆에 띄운다.
// 화면 루트의 마지막 자식이라 스크롤 마스크에 잘리지 않는다. 숨긴 칸·지운 줄에는 마우스 내림 알림이 오지 않아 부른 쪽이 Hide 를 챙긴다.
internal sealed class ScreenTip
{
    private const float Gap = 6f;       // 대상 끝과 말풍선 사이
    private const float MinWidth = 140f;
    private const float MaxWidth = 320f;
    private const float Padding = 20f;  // 프리팹 RightTooltip 좌우 여백 10 + 10

    private readonly RectTransform _screen;
    private readonly RectTransform _tip;

    internal ScreenTip(Transform screenRoot)
    {
        _screen = (RectTransform)screenRoot;
        _tip = (RectTransform)UiKit.Child(screenRoot, UiPaths.RightTooltip);
        Title = UiKit.Get<TMP_Text>(_tip, UiPaths.RightTipTitle);
        Kind = UiKit.Get<TMP_Text>(_tip, UiPaths.RightTipKind);
        Alt = UiKit.Get<TMP_Text>(_tip, UiPaths.RightTipAlt);
        Dim = Alt.color;
        Count = UiKit.Get<TMP_Text>(_tip, UiPaths.RightTipCount);
        Quests = UiKit.Get<TMP_Text>(_tip, UiPaths.RightTipQuests);
        _tip.gameObject.SetActive(false);
    }

    internal TMP_Text Title { get; }
    internal TMP_Text Kind { get; }
    internal TMP_Text Alt { get; }
    internal TMP_Text Count { get; }
    internal TMP_Text Quests { get; }
    internal Color Dim { get; } // 프리팹 Alt 줄의 흐린 글자색

    internal static void SetLine(TMP_Text text, string value)
    {
        text.gameObject.SetActive(!string.IsNullOrEmpty(value));
        text.text = value ?? "";
    }

    // 폭은 가장 긴 줄에 맞추되 140~320 이고 넘으면 줄을 바꾼다. 대상 위쪽에 맞춰 왼쪽(rightSide false)이나 오른쪽에 띄우고
    // 화면 밖으로 나가면 안으로 민다. 프리팹 말풍선의 기준점은 오른쪽 위다.
    internal void Show(RectTransform target, bool rightSide)
    {
        var width = MinWidth;
        foreach (var t in new[] { Title, Kind, Alt, Count, Quests })
            if (t.gameObject.activeSelf) width = Mathf.Max(width, t.GetPreferredValues(t.text, 9999f, 9999f).x + Padding);
        _tip.sizeDelta = new Vector2(Mathf.Min(width, MaxWidth), _tip.sizeDelta.y);
        _tip.gameObject.SetActive(true);
        _tip.SetAsLastSibling();
        LayoutRebuilder.ForceRebuildLayoutImmediate(_tip);

        var corners = new Vector3[4];
        target.GetWorldCorners(corners); // 1 은 왼쪽 위, 2 는 오른쪽 위
        var at = (Vector2)_screen.InverseTransformPoint(corners[rightSide ? 2 : 1]);
        var size = _tip.rect.size;
        var area = _screen.rect;
        var right = rightSide ? at.x + Gap + size.x : at.x - Gap;
        var x = Mathf.Clamp(right, area.xMin + size.x, Mathf.Max(area.xMin + size.x, area.xMax));
        var y = Mathf.Clamp(at.y, area.yMin + size.y, Mathf.Max(area.yMin + size.y, area.yMax));
        // 앵커가 루트 가운데라 루트 로컬 좌표가 곧 anchoredPosition 이다.
        _tip.anchoredPosition = new Vector2(x - area.center.x, y - area.center.y);
    }

    internal void Hide()
    {
        if (_tip != null) _tip.gameObject.SetActive(false);
    }
}
