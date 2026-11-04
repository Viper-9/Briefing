using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Briefing.Ui;

// 오른쪽 클릭만 콜백으로 넘긴다. 같은 GameObject 의 Button 은 왼쪽 클릭만 처리하므로 겹치지 않는다.
internal sealed class RightClickRelay : MonoBehaviour, IPointerClickHandler
{
    internal Action Click;

    public void OnPointerClick(PointerEventData e)
    {
        if (e.button == PointerEventData.InputButton.Right && Click != null) UiKit.Guard("right click", Click);
    }
}
