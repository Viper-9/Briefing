using EFT.UI;
using UnityEngine;

namespace Briefing.Ui;

// briefing.bundle 의 프리팹을 화면으로 쓰는 뷰. 지금은 켜짐/꺼짐 때 할 일이 없다.
internal sealed class BundleBriefingView : IBriefingView
{
    public GameObject Build(InventoryScreen screen, RectTransform parent) => BundleLoader.CreateScreen(screen, parent);
    public void OnShown() { }
    public void OnHidden() { }
}
