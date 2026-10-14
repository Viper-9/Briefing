using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Briefing.Tab;

// 보이는 탭을 x 순(같으면 sibling 순)으로 세워 원래 간격대로 다시 놓는다.
// Operator Traits(Apache-2.0)의 탭 줄 배치 규칙을 가져와 고쳤다. 규칙이 같아야 두 모드가 같은 줄을 만져도 서로 되돌리지 않는다.
// 두 모드 모두 줄 너비만 키우는데 pivot.x 가 0.5 라 줄이 양옆으로 늘어나 첫 탭이 화면 왼쪽 밖으로 밀린다.
// 다른 모드가 얼마나 키웠는지 알 수 없어서 가장 왼쪽 탭의 실제 위치를 재고, 부모 왼쪽 끝 안으로 들어오도록 오른쪽으로만 민다.
// 왼쪽으로는 밀지 않아야 떨리지 않고 Operator Traits 와도 부딪치지 않는다.
internal sealed class TabRowLayout : MonoBehaviour
{
    private RectTransform _row;
    private float _spacing;
    private float _firstX;
    private float _baseWidth;
    private int _baseCount;
    private const float Margin = 4f;
    private bool _failed;
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
        if (!_ready || _failed) return;
        try
        {
            var tabs = VisibleTabs();
            for (var i = 0; i < tabs.Count; i++)
            {
                var p = tabs[i].anchoredPosition;
                var want = _firstX + i * _spacing;
                if (Mathf.Abs(p.x - want) > 0.5f) { p.x = want; tabs[i].anchoredPosition = p; }
            }
            var width = _baseWidth + Mathf.Max(0, tabs.Count - _baseCount) * _spacing;
            if (_row.rect.width + 0.5f < width) _row.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);

            // 줄의 anchoredPosition 은 부모 공간 값이라 모자란 만큼 그대로 더한다.
            if (tabs.Count > 0 && transform.parent is RectTransform parent)
            {
                var left = LeftEdgeX(parent, tabs);
                var minLeft = parent.rect.xMin + Margin;
                if (left < minLeft - 0.5f)
                {
                    var pos = _row.anchoredPosition;
                    pos.x += minLeft - left;
                    _row.anchoredPosition = pos;
                }
            }
        }
        catch (System.Exception e)
        {
            _failed = true; // 매 프레임 로그가 쏟아지지 않게 멈춘다
            Log.Error($"briefing tab row layout failed: {e}");
        }
    }

    private static readonly Vector3[] Corners = new Vector3[4]; // LateUpdate 마다 할당하지 않으려고 둔다(메인 스레드 전용)

    // 탭들의 가장 왼쪽 끝(부모 공간 x)
    private static float LeftEdgeX(RectTransform parent, List<RectTransform> tabs)
    {
        var result = float.PositiveInfinity;
        var corners = Corners;
        foreach (var t in tabs)
        {
            t.GetWorldCorners(corners);
            foreach (var c in corners)
            {
                var x = parent.InverseTransformPoint(c).x;
                result = Mathf.Min(result, x);
            }
        }
        return result;
    }
}
