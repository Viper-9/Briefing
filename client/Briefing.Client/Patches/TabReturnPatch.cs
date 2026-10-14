using System;
using EFT.UI;
using HarmonyLib;

namespace Briefing.Patches;

// "완료하러 가기"로 연 상인 화면에서 돌아올 때 게임 탭이 잠깐 보이는 것을 막는다.
// 게임 ShowWithTab 코루틴은 2프레임 뒤 TabGroup.Show 로 기억한 탭을 여는데, 그 뒤에 브리핑 탭을 고르면 게임 탭이 한 번 깜빡인다.
// 그래서 돌아오는 화면의 탭 그룹을 Arm 해 두고 그 그룹의 첫 Show 를 건너뛰며 인자를 맡아 둔다.
// 브리핑 탭이 열리면 맡은 것을 버리고(Release), 못 열면 맡은 Show 를 그대로 부른다(Replay).
// 건너뛰어도 그룹의 _selectedTab 은 처음 탭 그대로라 닫기와 LastSelectedTab 은 깨지지 않는다.
[HarmonyPatch(typeof(TabGroup), nameof(TabGroup.Show))]
internal static class TabReturnPatch
{
    private static TabGroup _armed;
    private static TabGroup _heldGroup;
    private static global::Tab _heldTab;
    private static bool _replaying;

    internal static bool Holding => _heldGroup != null;

    // ShowWithTab 이 그룹을 Show 하기 전, 같은 프레임의 InventoryScreen.Show 후처리에서 부른다.
    internal static void Arm(TabGroup group)
    {
        Clear();
        _armed = group;
    }

    internal static void Release() => Clear();

    // 맡은 것이 없으면 Arm 만 푼다.
    internal static void Replay()
    {
        var group = _heldGroup;
        var tab = _heldTab;
        Clear();
        if (group == null) return;
        _replaying = true;
        try
        {
            group.Show(tab);
            Log.Info("turn in: held tab shown");
        }
        catch (Exception e) { Log.Warn($"turn in: held tab replay failed: {e.Message}"); }
        finally { _replaying = false; }
    }

    internal static void Clear()
    {
        _armed = null;
        _heldGroup = null;
        _heldTab = null;
    }

    [HarmonyPrefix]
    private static bool Prefix(TabGroup __instance, global::Tab tab)
    {
        try
        {
            if (_replaying || _armed == null || !ReferenceEquals(__instance, _armed)) return true;
            _armed = null;
            _heldGroup = __instance;
            _heldTab = tab;
            return false;
        }
        catch (Exception e)
        {
            Log.Warn($"turn in: tab hold failed: {e.Message}");
            return true;
        }
    }
}
