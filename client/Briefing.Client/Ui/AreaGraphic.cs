using System.Collections.Generic;
using Briefing.Core.Map;
using UnityEngine;
using UnityEngine.UI;

namespace Briefing.Ui;

// 퀘스트 구역 사각형. 마커와 같은 톤의 옅은 채움과 테두리를 그리고, 강조되면 선택 줄 색으로 진하게 그린다.
// 테두리는 확대해도 화면에서 같은 두께로 보이게 배율로 나눈다. 웹의 점선 대신 실선이고, 마우스는 받지 않는다.
internal sealed class AreaGraphic : MaskableGraphic
{
    private readonly List<Vector2> _corners = new List<Vector2>();
    private Emphasis _look;
    private float _scale = 1f;

    internal void Set(IReadOnlyList<MapUv> polygon)
    {
        _corners.Clear();
        foreach (var p in polygon) _corners.Add(new Vector2((float)p.U, (float)p.V));
        raycastTarget = false;
        SetVerticesDirty();
    }

    internal void SetLook(Emphasis look)
    {
        if (_look == look) return;
        _look = look;
        SetVerticesDirty();
    }

    internal void SetScale(double scale)
    {
        _scale = Mathf.Max(1f, (float)scale);
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        if (_corners.Count < 3) return;
        var r = rectTransform.rect;
        var points = new List<Vector2>(_corners.Count);
        foreach (var c in _corners) points.Add(new Vector2(r.xMin + c.x * r.width, r.yMax - c.y * r.height));

        var hot = _look == Emphasis.Hot;
        var alpha = _look == Emphasis.Dim ? 0.25f : 1f;
        var tone = hot ? MarkerTone.LitFill : MarkerTone.Ink;
        var fill = tone;
        fill.a = (hot ? 0.3f : 0.08f) * alpha;
        var stroke = tone;
        stroke.a = (hot ? 1f : 0.8f) * alpha;

        // 볼록 사각형이라 한 꼭짓점에서 부채꼴로 나누면 된다.
        foreach (var p in points) vh.AddVert(p, fill, Vector2.zero);
        for (var i = 1; i < points.Count - 1; i++) vh.AddTriangle(0, i, i + 1);

        // 테두리는 변마다 얇은 사각형으로.
        var half = (hot ? 3f : 2f) / 2f / _scale;
        for (var i = 0; i < points.Count; i++)
        {
            var a = points[i];
            var b = points[(i + 1) % points.Count];
            var d = b - a;
            if (d.sqrMagnitude < 1e-6f) continue;
            var n = new Vector2(-d.y, d.x).normalized * half;
            var start = vh.currentVertCount;
            vh.AddVert(a + n, stroke, Vector2.zero);
            vh.AddVert(a - n, stroke, Vector2.zero);
            vh.AddVert(b - n, stroke, Vector2.zero);
            vh.AddVert(b + n, stroke, Vector2.zero);
            vh.AddTriangle(start, start + 1, start + 2);
            vh.AddTriangle(start, start + 2, start + 3);
        }
    }
}
