using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Briefing.Ui;

// 마우스 올림, 내림, 왼쪽 클릭을 콜백으로 넘긴다(지도 마커·아이콘, 퀘스트 줄). 프리팹에는 붙이지 않는다.
// 같은 GameObject 에 Button 이 있으면 클릭을 둘 다 받으므로 퀘스트 줄에는 Click 을 넣지 않는다.
internal sealed class HoverRelay : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    internal Action Enter;
    internal Action Exit;
    internal Action Click;

    public void OnPointerEnter(PointerEventData e)
    {
        if (Enter != null) UiKit.Guard("hover enter", Enter);
    }

    public void OnPointerExit(PointerEventData e)
    {
        if (Exit != null) UiKit.Guard("hover exit", Exit);
    }

    public void OnPointerClick(PointerEventData e)
    {
        if (e.button == PointerEventData.InputButton.Left && Click != null) UiKit.Guard("hover click", Click);
    }
}
