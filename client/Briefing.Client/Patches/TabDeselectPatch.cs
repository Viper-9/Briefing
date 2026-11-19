using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using HarmonyLib;

namespace Briefing.Patches;

// 등록한 탭의 Deselect 에서, 조건이 맞으면 컨트롤러 TryHide 를 건너뛰고 탭 모양만 내린다(Tab.Deselect 에서 컨트롤러를 뺀 것과 같다).
// 브리핑을 보는 동안 그룹은 임무 탭을 고른 것으로 두는데, 다음 탭 전환 때 그룹이 임무 탭을 Deselect 하면 TasksScreen.Close 가 불린다.
// TasksScreen.Close 는 Show 에서 채우는 _tooltip 등을 그대로 써서, 한 번도 열리지 않은 임무 화면이면 NullReferenceException 이 난다.
// 그러면 TabGroup 의 _isChangingSelection 이 true 로 남아 화면을 다시 열 때까지 탭이 바뀌지 않는다.
// 꺼진 화면은 이미 닫혔거나 열린 적이 없으니 닫기를 건너뛰어도 놓치는 것이 없다.
[HarmonyPatch(typeof(global::Tab), nameof(global::Tab.Deselect))]
internal static class TabDeselectPatch
{
    private static readonly Dictionary<global::Tab, Func<bool>> SkipControllerWhen = new();

    internal static void Register(global::Tab tab, Func<bool> skipController) => SkipControllerWhen[tab] = skipController;

    internal static void Unregister(global::Tab tab)
    {
        if (!ReferenceEquals(tab, null)) SkipControllerWhen.Remove(tab);
    }

    [HarmonyPrefix]
    private static bool Prefix(global::Tab __instance, ref Task<bool> __result)
    {
        try
        {
            if (!SkipControllerWhen.TryGetValue(__instance, out var skip) || !skip()) return true;
            __instance.UpdateVisual(false);
            __result = Task.FromResult(true);
            return false;
        }
        catch (Exception e)
        {
            Log.Error($"tab deselect hook failed: {e}");
            return true;
        }
    }
}
