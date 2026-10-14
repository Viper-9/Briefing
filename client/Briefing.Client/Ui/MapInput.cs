using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Briefing.Ui;

// 지도 칸 Viewport 에 런타임에 붙인다. 프리팹에는 우리 스크립트를 넣지 않는다.
// 휠은 확대·축소, 왼쪽 끌기는 이동, 더블클릭은 원래대로. 계산은 MapView 가 한다.
// HoverRelay 에는 휠·끌기 처리기가 없어서 마커 위에서 시작한 휠·끌기도 여기로 올라온다.
internal sealed class MapInput : MonoBehaviour, IScrollHandler, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerClickHandler
{
    internal Action Resized;
    internal Action<PointerEventData> Wheel;
    internal Action<PointerEventData> DragStarted;
    internal Action<PointerEventData> Dragged;
    internal Action DoubleClicked;

    private bool _dragging;

    private void OnRectTransformDimensionsChange()
    {
        if (Resized != null) UiKit.Guard("map resize", Resized);
    }

    public void OnScroll(PointerEventData e)
    {
        if (Wheel != null) UiKit.Guard("map wheel", () => Wheel(e));
    }

    public void OnBeginDrag(PointerEventData e)
    {
        _dragging = e.button == PointerEventData.InputButton.Left;
        if (_dragging && DragStarted != null) UiKit.Guard("map drag", () => DragStarted(e));
    }

    public void OnDrag(PointerEventData e)
    {
        if (_dragging && Dragged != null) UiKit.Guard("map drag", () => Dragged(e));
    }

    public void OnEndDrag(PointerEventData e) => _dragging = false;

    public void OnPointerClick(PointerEventData e)
    {
        if (e.button == PointerEventData.InputButton.Left && e.clickCount == 2 && DoubleClicked != null) UiKit.Guard("map reset", DoubleClicked);
    }
}
