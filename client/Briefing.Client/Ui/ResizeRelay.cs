using System;
using UnityEngine;

namespace Briefing.Ui;

// 크기가 바뀐 것을 다음 프레임에 한 번 알린다. 프리팹에는 붙이지 않는다.
// OnRectTransformDimensionsChange 는 레이아웃 계산 중에도 오는데 거기서 LayoutElement 를 바꾸면 Unity 가 경고하므로 Update 로 미룬다.
// 오른쪽 열 폭이 바뀌면 격자만 다시 놓는 데 쓴다.
internal sealed class ResizeRelay : MonoBehaviour
{
    internal Action Resized;
    private bool _dirty;

    private void OnRectTransformDimensionsChange() => _dirty = true;

    private void Update()
    {
        if (!_dirty) return;
        _dirty = false;
        if (Resized != null) UiKit.Guard("right column resize", Resized);
    }
}
