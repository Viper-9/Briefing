using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using EFT.Quests;
using EFT.Trading;
using EFT.UI;
using EFT.UI.Screens;
using HarmonyLib;
using UnityEngine;

namespace Briefing.Game;

// "완료하러 가기"(Task 8). 게임 메뉴가 상인 화면을 여는 것과 같은 호출(MainMenu method_41: 메뉴 상인 목록 +
// new TraderScreensGroup.TraderScreenController(...).ShowScreen(Queued))로 그 퀘스트의 상인 화면을 열고, 화면이 뜨면
// 임무 모드로 바꾼 뒤(SetMode(Tasks) = 사용자가 "임무" 탭을 누름) 상인 임무 목록에서 그 퀘스트 줄을 고른다(OnQuestSelected =
// 사용자가 줄을 누름). 완료 처리는 하지 않는다 — 게임 QuestView 의 "완료" 버튼을 사용자가 누른다. 못 찾으면 열린 화면 그대로 로그만.
// 캐릭터 화면은 상인 화면이 뜨면 꺼지므로 코루틴은 늘 켜져 있는 플러그인 객체에서 돌린다(TasksJump 는 캐릭터 화면 안이라 화면에서 돌린다).
internal static class TraderJump
{
    // 화면 전환(ShowScreenAsync) + 임무 탭 전환(TabGroup.TryHide 는 비동기)을 기다리는 실제 시간 한도. 프레임 수는 프레임률에 따라
    // 달라지므로 시간으로 재고, 프레임 상한은 시간이 멈춘 듯한 경우를 위한 안전장치일 뿐이다.
    private const float MaxSeconds = 3f;
    private const int MaxFrames = 2000;

    // QuestsListView 의 private BindableViewList<Quest, QuestListItem>(ViewList 의 하위 형식, Views 는 public Dictionary).
    private static readonly FieldInfo ViewListField = AccessTools.Field(typeof(QuestsListView), "_questBindableViewList");

    private static bool _running; // 연타로 상인 화면을 여러 번 열지 않게

    // 상인 화면을 닫고 캐릭터 화면으로 돌아오면 브리핑 탭을 다시 고르라는 한 번짜리 요청(우리 탭은 EInventoryTab 이 아니라 게임이
    // 기억하지 못하고 임무 탭으로 돌아온다). 상인 화면을 실제로 연 시각. 다음 캐릭터 화면 Show 때 BriefingTabHost.AttachWhenReady 가 TakeReturnRequest 로 꺼낸다.
    private const float ReturnRequestSeconds = 600f; // 10분이 지난 요청은 버린다(그사이 다른 경로로 캐릭터 화면을 열었을 수 있다)
    private static float? _returnRequestedAt;

    // 요청이 있고 10분 이내면 true. 지우지 않는다(Show 와 같은 프레임에 내용을 미리 가릴지 정할 때).
    internal static bool PeekReturnRequest()
    {
        var at = _returnRequestedAt;
        return at.HasValue && Time.realtimeSinceStartup - at.Value <= ReturnRequestSeconds;
    }

    // Peek 과 같은 값을 돌려주고 지운다(한 번짜리).
    internal static bool TakeReturnRequest()
    {
        var pending = PeekReturnRequest();
        _returnRequestedAt = null;
        return pending;
    }

    // 진행 단계 겸 기다리는 이유 — 시간이 다 되면 이유별로 다른 로그를 남긴다.
    private enum Step { WaitScreen, WaitList, NoViewListField, QuestNotFound, Verify, Overridden }

    internal static void Open(InventoryScreen screen, string questId)
    {
        if (_running) return;
        try
        {
            // 건네주기와 같은 판정(QuestBridge.CanHandoverOn): 레이드 밖 + 메뉴 컨트롤러 + 인벤토리가 막히지 않음(매칭 중 아님).
            if (!QuestBridge.CanHandoverOn(screen))
            {
                Log.Warn($"turn in: quest {questId} not opened (in raid, matching or screen unreadable)");
                return;
            }
            var questController = GameRefs.QuestControllerOf(screen);
            var screenController = GameRefs.InventoryScreenControllerOf(screen);
            var session = screenController?.Session;
            var inventoryController = GameRefs.InventoryControllerOf(screen);
            if (session == null || inventoryController == null)
            {
                Log.Warn($"turn in: quest {questId} not opened (session or controllers not found)");
                return;
            }
            var traderId = questController.Quests.FirstOrDefault(q => q?.Id == questId)?.Template?.TraderId;
            var trader = traderId == null ? null : session.Traders.FirstOrDefault(t => t.Id == traderId);
            if (trader == null)
            {
                Log.Warn($"turn in: trader of quest {questId} not found");
                return;
            }
            if (trader.Settings.AvailableInRaid) // TurnIns 가 이미 거르지만 직접 부르는 경우도 막는다
            {
                Log.Warn($"turn in: trader {trader.Id} is in-raid only, quest {questId} not opened");
                return;
            }
            // 게임 메뉴의 상인 목록(레이드 전용 상인 제외) — 위쪽 상인 카드 줄이 메뉴에서 연 것과 같다.
            var tradersList = session.Traders.Where(t => !t.Settings.AvailableInRaid).ToArray();
            var controller = new TraderScreensGroup.TraderScreenController(trader, tradersList, session.Profile, inventoryController,
                screenController.HealthController, questController, screenController.AchievementsController, session);
            controller.ShowScreen(EScreenState.Queued);
            _returnRequestedAt = Time.realtimeSinceStartup;
            _running = true;
            Plugin.Instance.StartCoroutine(SelectWhenReady(controller, trader, questId));
        }
        catch (Exception e)
        {
            _running = false;
            Log.Error($"turn in: quest {questId} failed: {e}");
        }
    }

    private static IEnumerator SelectWhenReady(TraderScreensGroup.TraderScreenController controller, Trader trader, string questId)
    {
        try
        {
            var step = Step.WaitScreen;
            var seen = false; // 상인 화면이 한 번이라도 켜진 것을 봤는가
            var deadline = Time.realtimeSinceStartup + MaxSeconds;
            for (var frame = 0; frame < MaxFrames && Time.realtimeSinceStartup < deadline; frame++)
            {
                yield return null;
                try
                {
                    // 한 번 뜬 화면이 닫혔으면 멈춘다 — 사용자가 나중에 다시 연 상인 화면을 우리가 몰지 않게.
                    var screen = controller.Screen;
                    var active = screen != null && screen.gameObject.activeInHierarchy && !controller.Closed;
                    if (seen && !active)
                    {
                        Log.Warn($"turn in: trader screen closed before quest {questId} was selected");
                        yield break;
                    }
                    seen |= active;
                    // 고른 다음 프레임에도 그 퀘스트가 보이면 끝. 아니면 되돌려진 것(Overridden) — Advance 가 다시 고른다.
                    if (step == Step.Verify || step == Step.Overridden)
                    {
                        if (Selected(controller, questId))
                        {
                            Log.Info($"turn in: opened trader {trader.Id}, quest {questId} selected");
                            yield break;
                        }
                        step = Step.Overridden;
                    }
                    step = Advance(controller, trader, questId, step);
                }
                catch (Exception e)
                {
                    Log.Error($"turn in: opened trader {trader.Id}, quest {questId} failed: {e}");
                    yield break;
                }
            }
            Log.Warn(TimeoutMessage(step, trader.Id, questId));
        }
        finally
        {
            _running = false;
        }
    }

    private static string TimeoutMessage(Step step, string traderId, string questId)
    {
        switch (step)
        {
            case Step.WaitScreen: return $"turn in: trader {traderId} screen was not ready after {MaxSeconds}s";
            case Step.WaitList: return $"turn in: opened trader {traderId}, tasks list was not ready after {MaxSeconds}s";
            case Step.NoViewListField: return $"turn in: opened trader {traderId}, quest list field _questBindableViewList not found (game update?)";
            case Step.Verify:
            case Step.Overridden: return $"turn in: opened trader {traderId}, quest {questId} selection kept being overridden";
            default: return $"turn in: opened trader {traderId}, quest {questId} not found";
        }
    }

    // 한 프레임 진행: 화면이 뜨면 임무 모드로 → 임무 목록이 켜지고 그 퀘스트 줄이 보이면 고른다(Verify).
    // 호출자가 다음 프레임에 그대로인지 본다(목록이 늦게 추가된 줄로 AutoSelectQuest 를 다시 돌면 첫 줄로 바뀔 수 있다 → 다시 고른다).
    // 한 번 되돌려졌으면(Overridden) 다시 고르더라도 그 상태를 유지해, 시간이 다 되면 "덮어써짐"으로 로그한다.
    private static Step Advance(TraderScreensGroup.TraderScreenController controller, Trader trader, string questId, Step step)
    {
        var screen = controller.Screen;
        if (screen == null || !screen.gameObject.activeInHierarchy || screen.Trader != trader) return Step.WaitScreen;
        if (step == Step.WaitScreen)
        {
            screen.SetMode(TraderScreensGroup.ETraderMode.Tasks); // Show 는 늘 거래 모드로 시작한다
            return Step.WaitList;
        }
        var waiting = step == Step.Overridden ? Step.Overridden : Step.WaitList;
        var list = screen._questsScreen != null ? screen._questsScreen._questsListView : null;
        if (list == null || !list.gameObject.activeInHierarchy) return waiting;
        if (ViewListField == null) return Step.NoViewListField;
        if (!(ViewListField.GetValue(list) is ViewList<Quest, QuestListItem> views)) return waiting;
        foreach (var pair in views.Views)
        {
            if (pair.Key == null || pair.Key.Id != questId || pair.Value == null) continue;
            // 숨은 줄(완료·실패·잠김 필터, IsVisible 거짓)은 고르지 않는다 — 완료 가능 퀘스트는 필터에 걸리지 않는다.
            if (!pair.Value.gameObject.activeSelf) return Step.QuestNotFound;
            if (!Selected(controller, questId)) list.OnQuestSelected(pair.Value);
            return step == Step.Overridden ? Step.Overridden : Step.Verify;
        }
        return Step.QuestNotFound;
    }

    // 오른쪽 퀘스트 화면(QuestView)이 그 퀘스트를 보이는 중인가.
    private static bool Selected(TraderScreensGroup.TraderScreenController controller, string questId)
    {
        var view = controller.Screen != null && controller.Screen._questsScreen != null ? controller.Screen._questsScreen._questView : null;
        return view != null && view.gameObject.activeInHierarchy && view.QuestId == questId;
    }
}
