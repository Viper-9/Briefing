using System;
using System.Collections.Generic;
using HarmonyLib;

namespace Briefing.Patches;

// 등록한 게임 탭이 실제로 화면을 열면(Tab.Select, sendCallback=true) 알린다. 브리핑 패널을 내리는 데 쓴다.
// 클릭 이벤트(Tab.OnSelectionChanged)는 마우스로 눌렀을 때만 울린다. 다른 모드가 TabGroup.SelectionChangedHandler,
// TabGroup.Show/SelectTab, Tab.Select 를 바로 부르면 울리지 않아서 브리핑이 게임 화면 위에 남았다.
// Tab.Select 는 이 경로들이 모두 지나는 마지막 지점이다(여기서 Controller.Show 를 부른다).
// 인벤토리 탭은 Select 를 덮어쓰지 않는 Tab 이다. 덮어쓰는 하위 클래스는 은신처 셀과 필터 탭뿐이다.
[HarmonyPatch(typeof(global::Tab), nameof(global::Tab.Select))]
internal static class TabSelectPatch
{
    private static readonly Dictionary<global::Tab, Action> Handlers = new();

    internal static void Register(global::Tab tab, Action onShown) => Handlers[tab] = onShown;

    internal static void Unregister(global::Tab tab)
    {
        if (!ReferenceEquals(tab, null)) Handlers.Remove(tab);
    }

    [HarmonyPostfix]
    private static void Postfix(global::Tab __instance, bool sendCallback)
    {
        try
        {
            if (sendCallback && Handlers.TryGetValue(__instance, out var onShown)) onShown();
        }
        catch (Exception e)
        {
            Log.Error($"tab select hook failed: {e}");
        }
    }
}
