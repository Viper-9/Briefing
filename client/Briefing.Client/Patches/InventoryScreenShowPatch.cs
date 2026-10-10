using System;
using System.Linq;
using System.Reflection;
using Briefing.Tab;
using EFT.UI;
using HarmonyLib;

namespace Briefing.Patches;

// InventoryScreen.Show(…, EInventoryTab, …) 후처리. 오버로드가 여럿이라 EInventoryTab 인자를 가진 가장 긴 것을 고른다.
[HarmonyPatch]
internal static class InventoryScreenShowPatch
{
    private static MethodBase TargetMethod() =>
        AccessTools.GetDeclaredMethods(typeof(InventoryScreen))
            .Where(m => m.Name == "Show" && m.GetParameters().Any(p => p.ParameterType == typeof(EInventoryTab)))
            .OrderByDescending(m => m.GetParameters().Length)
            .FirstOrDefault() ?? throw new MissingMethodException(nameof(InventoryScreen), "Show(…, EInventoryTab, …)");

    [HarmonyPostfix]
    private static void Postfix(InventoryScreen __instance)
    {
        try { BriefingTabHost.AttachOrRefresh(__instance); }
        catch (Exception e) { Log.Error($"InventoryScreen.Show postfix failed: {e}"); }
    }
}
