using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Briefing.Tab;

// 탭 줄 간격 재배치. 규칙: 보이는 Tab 을 anchoredPosition.x(동률이면 sibling 순)로 정렬 → x = 첫 x + i × 원래 간격.
// Operator Traits(Apache-2.0)의 탭 줄 배치 규칙(간격 중앙값·첫 x·줄 너비)을 가져와 고친 것이다. 같은 규칙이어야 두 모드가 같은 줄을 만질 때 서로 되돌리지 않는다.
internal sealed class TabRowLayout : MonoBehaviour
{
    private RectTransform _row;
    private float _spacing;
    private float _firstX;
    private float _baseWidth;
    private int _baseCount;
    private bool _ready;

    internal bool IsReady => _ready;

    internal bool Configure(IEnumerable<global::Tab> nativeTabs)
    {
        if (_ready) return true;
        _row = (RectTransform)transform;
        var xs = nativeTabs.Where(t => t != null && t.transform.parent == transform && t.gameObject.activeSelf)
            .Select(t => ((RectTransform)t.transform).anchoredPosition.x).OrderBy(x => x).ToList();
        if (xs.Count < 2) return false;
        var gaps = xs.Zip(xs.Skip(1), (a, b) => b - a).Where(g => g > 0.5f).OrderBy(g => g).ToList();
        if (gaps.Count == 0) return false;
        _spacing = gaps[gaps.Count / 2];
        _firstX = xs[0];
        _baseWidth = _row.rect.width;
        _baseCount = xs.Count;
        _ready = true;
        return true;
    }

    private List<RectTransform> VisibleTabs()
    {
        var list = new List<RectTransform>();
        for (var i = 0; i < transform.childCount; i++)
        {
            var c = transform.GetChild(i);
            if (c.gameObject.activeSelf && c.GetComponent<global::Tab>() != null && c is RectTransform r) list.Add(r);
        }
        return list.OrderBy(r => r.anchoredPosition.x).ThenBy(r => r.GetSiblingIndex()).ToList();
    }

    private void LateUpdate()
    {
        if (!_ready) return;
        var tabs = VisibleTabs();
        for (var i = 0; i < tabs.Count; i++)
        {
            var p = tabs[i].anchoredPosition;
            var want = _firstX + i * _spacing;
            if (Mathf.Abs(p.x - want) > 0.5f) { p.x = want; tabs[i].anchoredPosition = p; }
        }
        var width = _baseWidth + Mathf.Max(0, tabs.Count - _baseCount) * _spacing;
        if (_row.rect.width + 0.5f < width) _row.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
    }
}
