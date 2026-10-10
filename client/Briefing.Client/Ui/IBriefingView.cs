using EFT.UI;
using UnityEngine;

namespace Briefing.Ui;

// 브리핑 탭 안에 보이는 화면. 탭이 만들어질 때 Build, 탭이 켜지고 꺼질 때 OnShown/OnHidden 이 불린다.
// Build 가 실패(예외)하거나 null 을 돌려주면 탭을 만들지 않는다.
internal interface IBriefingView
{
    GameObject Build(InventoryScreen screen, RectTransform parent);
    void OnShown();
    void OnHidden();
}
