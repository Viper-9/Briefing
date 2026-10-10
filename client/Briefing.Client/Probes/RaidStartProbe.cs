using System;
using EFT;
using HarmonyLib;

namespace Briefing.Probes;

// 0단계 실측: 메뉴에서 레이드로 넘어가는 시점. 4단계(레이드 진입 시 갱신)의 훅 후보.
[HarmonyPatch(typeof(TarkovApplication), nameof(TarkovApplication.LocalGameMatching))]
internal static class RaidStartProbe
{
    [HarmonyPrefix]
    private static void Prefix()
    {
        try { Log.Info($"[probe] LocalGameMatching entered at {DateTime.Now:HH:mm:ss}"); }
        catch { /* 실측 로그 실패가 레이드 진입을 막으면 안 된다 */ }
    }
}
