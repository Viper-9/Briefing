using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine.EventSystems;

namespace Briefing.Patches;

// 등록한 탭은 마우스로 눌렀을 때 게임 선택 대신 등록한 동작을 한다. 임무 탭(드롭다운 열기)과 드롭다운 항목에 쓴다.
// HandlePointerClick 이 아니라 OnPointerClick 을 막는다. TasksJump 처럼 코드에서 HandlePointerClick 을 부르는 길은 그대로 둔다.
// 고른 탭은 CanHandlePointerClick 이 false 라 게임이 클릭을 버리지만, 여기서는 그 전에 받으므로 고른 상태에서도 열린다.
[HarmonyPatch(typeof(global::Tab), nameof(global::Tab.OnPointerClick))]
internal static class TabClickPatch
{
    private static readonly Dictionary<global::Tab, Action> Handlers = new();

    internal static void Register(global::Tab tab, Action onClick) => Handlers[tab] = onClick;

    internal static void Unregister(global::Tab tab)
    {
        if (!ReferenceEquals(tab, null)) Handlers.Remove(tab);
    }

    [HarmonyPrefix]
    private static bool Prefix(global::Tab __instance, PointerEventData eventData)
    {
        try
        {
            if (!Handlers.TryGetValue(__instance, out var onClick)) return true;
            if (eventData != null && eventData.button != PointerEventData.InputButton.Left) return false;
            onClick();
            return false;
        }
        catch (Exception e)
        {
            Log.Error($"tab click failed: {e}");
            return true; // 실패하면 게임 동작대로(임무 탭이면 임무 화면이 열린다)
        }
    }
}
