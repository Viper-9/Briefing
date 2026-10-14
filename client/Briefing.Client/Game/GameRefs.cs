using System;
using System.Reflection;
using EFT;
using EFT.InventoryLogic;
using EFT.Quests;
using EFT.UI;
using HarmonyLib;

namespace Briefing.Game;

// InventoryScreen 의 private 필드. 이름은 ilspycmd 로 확인했다. 게임 업데이트로 이름이 바뀌면 null 이 되고,
// 쓰는 쪽은 상인 얼굴, 건네주기, 임무 탭 이동 같은 그 기능만 끈다.
internal static class GameRefs
{
    private static readonly FieldInfo ProfileField = AccessTools.Field(typeof(InventoryScreen), "_profile");
    private static readonly FieldInfo QuestControllerField = AccessTools.Field(typeof(InventoryScreen), "_questController");
    private static readonly FieldInfo InventoryControllerField = AccessTools.Field(typeof(InventoryScreen), "_inventoryController");

    internal static Profile ProfileOf(InventoryScreen screen) => ProfileField?.GetValue(screen) as Profile;

    // 메뉴에서는 QuestControllerClientBackend(제출이 서버로 간다), 레이드에서는 레이드용 컨트롤러다.
    internal static QuestController QuestControllerOf(InventoryScreen screen) => QuestControllerField?.GetValue(screen) as QuestController;

    internal static InventoryController InventoryControllerOf(InventoryScreen screen) =>
        InventoryControllerField?.GetValue(screen) as InventoryController;

    // ScreenController 는 BaseScreen<,,> 의 protected 필드라 상속을 따라 올라가며 찾는다.
    internal static object ScreenControllerOf(InventoryScreen screen)
    {
        try
        {
            if (screen == null) return null;
            for (var type = screen.GetType(); type != null; type = type.BaseType)
            {
                var field = AccessTools.Field(type, "ScreenController");
                if (field == null || field.DeclaringType != type) continue;
                return field.GetValue(screen);
            }
            return null;
        }
        catch (Exception) { return null; }
    }

    // 상인 화면을 열 때 필요한 세션·체력·업적 컨트롤러가 여기 public 필드로 있다.
    internal static InventoryScreen.InventoryScreenController InventoryScreenControllerOf(InventoryScreen screen) =>
        ScreenControllerOf(screen) as InventoryScreen.InventoryScreenController;
}
