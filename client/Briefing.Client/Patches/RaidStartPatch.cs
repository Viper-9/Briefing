using System;
using Briefing.Net;
using Briefing.Ui;
using EFT;
using HarmonyLib;

namespace Briefing.Patches;

// LocalGameMatching 은 메뉴에서 레이드를 불러오기 전에 불린다. 맵 사이 환승으로 다음 레이드를 불러올 때도 불린다.
// 여기서 브리핑을 한 번 더 요청하되 기다리지 않아서 레이드 진입을 늦추지 않는다.
// 이번 레이드 맵을 다시 고르도록 RaidMapPicked 를 지운다. 예외는 삼켜 레이드 진입을 막지 않는다.
[HarmonyPatch(typeof(TarkovApplication), nameof(TarkovApplication.LocalGameMatching))]
internal static class RaidStartPatch
{
    [HarmonyPrefix]
    private static void Prefix()
    {
        try
        {
            BriefingState.RaidMapPicked = false;
            _ = BriefingClient.RefreshAsync();
            Log.Info("raid loading: briefing refresh sent");
        }
        catch (Exception e)
        {
            Log.Warn($"raid loading refresh failed: {e.Message}");
        }
    }
}
