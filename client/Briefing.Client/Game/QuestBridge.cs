using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Briefing.Core.Screen;
using Briefing.Net;
using Comfort.Common;
using EFT;
using EFT.InventoryLogic;
using EFT.Quests;
using EFT.UI;

namespace Briefing.Game;

// 게임 퀘스트를 읽고 건네주기를 실행한다. 판정과 제출은 게임 QuestObjectiveView 의 OnConditionChanged·QuestHandover 를
// 그대로 옮겨서 상인 화면의 "건네주기" 버튼과 조건, 창, 서버 호출이 같다.
// 제출은 메뉴(QuestControllerClientBackend)에서만 한다. 레이드용 컨트롤러로는 보내지 않는다.
internal sealed class QuestBridge
{
    private readonly InventoryScreen _screen;
    private bool _warned;
    private bool _objectivesWarned;
    private bool _turnInsWarned;
    private bool _liveWarned;
    private TaskCompletionSource<Item[]> _picking; // 선택 창이 떠 있는 동안만 있다

    internal QuestBridge(InventoryScreen screen) => _screen = screen;

    // 제출 중에는(선택 창 포함) 모든 "건네주기" 버튼을 막는다. 게임의 _performingAction 과 같은 역할.
    internal bool Busy { get; private set; }

    private bool CanHandover => CanHandoverOn(_screen);

    // 건네주기나 상인 화면 열기를 해도 되는지. TraderJump.Open 도 이 판정을 쓴다.
    // 매칭 중에는 InRaid 가 거짓이고 컨트롤러도 메뉴용인데, 게임이 캐릭터 화면을 IsInventoryBlocked 로 열어 임무 탭 버튼을 막는다.
    // 그래서 IsInventoryBlocked 를 따로 보고, 화면 컨트롤러를 못 읽으면 막힌 것으로 본다.
    internal static bool CanHandoverOn(InventoryScreen screen)
    {
        try
        {
            if (TasksPanel.InRaid() || !(GameRefs.QuestControllerOf(screen) is QuestControllerClientBackend)) return false;
            var controller = GameRefs.InventoryScreenControllerOf(screen);
            return controller != null && !controller.IsInventoryBlocked;
        }
        catch (Exception) { return false; }
    }

    // 진행 중 퀘스트의 남은 제출 목표. 레이드 중이면 null, 낼 것이 없거나 못 읽으면 빈 목록.
    internal IReadOnlyList<HandoverGoal> Goals()
    {
        try
        {
            if (!CanHandover) return null;
            var controller = GameRefs.QuestControllerOf(_screen);
            var profile = controller.Profile;
            var goals = new List<HandoverGoal>();
            foreach (var quest in controller.Quests)
            {
                if (!IsMenuQuest(controller, quest, EQuestStatus.Started)) continue;
                foreach (var condition in quest.NecessaryConditions)
                {
                    if (!(condition is ConditionHandoverItem || condition is ConditionWeaponAssembly) || quest.IsConditionDone(condition)) continue;
                    if (!quest.ProgressCheckers.TryGetValue(condition, out var checker)) continue;
                    goals.Add(new HandoverGoal(quest.Id, quest.Template.Name, quest.Template.TraderId, condition.id.ToString(),
                        condition.FormattedDescription, checker.CurrentValue, condition.IsHiddenValue ? (double?)null : condition.value,
                        Ready(condition, profile), ItemNames(condition)));
                }
            }
            return goals;
        }
        catch (Exception e)
        {
            if (!_warned) Log.Warn($"handover goals unavailable: {e}");
            _warned = true;
            return Array.Empty<HandoverGoal>();
        }
    }

    // 상인에게 보고만 하면 되는 퀘스트(AvailableForFinish). 메뉴 상인 화면에서 끝내므로 Goals 와 같은 조건으로 거른다.
    // 레이드 중이거나 못 읽으면 null.
    internal IReadOnlyList<TurnInQuest> TurnIns()
    {
        try
        {
            if (!CanHandover) return null;
            var controller = GameRefs.QuestControllerOf(_screen);
            var list = new List<TurnInQuest>();
            foreach (var quest in controller.Quests)
            {
                if (!IsMenuQuest(controller, quest, EQuestStatus.AvailableForFinish)) continue;
                list.Add(new TurnInQuest(quest.Id, quest.Template.Name, quest.Template.TraderId));
            }
            return list;
        }
        catch (Exception e)
        {
            if (!_turnInsWarned) Log.Warn($"turn-in quests unavailable: {e}");
            _turnInsWarned = true;
            return null;
        }
    }

    // 펼친 퀘스트의 목표를 임무 탭과 같은 순서(quest.NecessaryConditions)로. 읽기만 하므로 레이드 중에도 된다.
    // 못 읽으면 null 이고 펼침은 서버 목표와 건네주기 줄로 그린다.
    internal IReadOnlyList<GameObjective> Objectives(string questId)
    {
        try
        {
            if (questId == null) return null;
            var controller = GameRefs.QuestControllerOf(_screen);
            if (controller?.Quests == null) return null;
            // 스캐브 레이드처럼 다른 프로필의 퀘스트는 보지 않는다.
            var quest = controller.Quests.FirstOrDefault(q => q?.Id == questId && controller.IsQuestForCurrentProfile(q));
            if (quest?.Template == null) return null;
            var list = new List<GameObjective>();
            foreach (var condition in quest.NecessaryConditions)
            {
                // IsConditionDone 이 ProgressCheckers 인덱서를 써서 검사기가 없으면 예외가 난다. 그런 조건은 건너뛴다.
                if (condition == null || !quest.ProgressCheckers.TryGetValue(condition, out var checker)) continue;
                list.Add(new GameObjective(quest.Id, condition.id.ToString(), condition.FormattedDescription, checker.CurrentValue,
                    condition.IsHiddenValue ? (double?)null : condition.value, quest.IsConditionDone(condition), KindOf(condition),
                    ItemNames(condition)));
            }
            return list;
        }
        catch (Exception e)
        {
            if (!_objectivesWarned) Log.Warn($"quest objectives unavailable: {e}");
            _objectivesWarned = true;
            return null;
        }
    }

    // 레이드 중 실시간 진행. targets 의 목표만 ProgressCheckers[c].CurrentValue 와 IsConditionDone(c) 로 읽는다.
    // CurrentValue 는 레이드용 컨트롤러(SPT 에서는 QuestControllerClientLocalGame)의 조건 카운터를 그때그때 읽는 게터다.
    // 스캐브 레이드면 PMC 퀘스트가 없어 빈 표가 되고 응답 값이 그대로 보인다. 못 읽으면 null 이라 덮어쓰지 않는다.
    internal IReadOnlyDictionary<string, LiveObjective> LiveObjectives(LiveTargets targets)
    {
        try
        {
            if (targets == null || targets.QuestIds.Count == 0) return null;
            var controller = GameRefs.QuestControllerOf(_screen);
            if (controller?.Quests == null) return null;
            var live = new Dictionary<string, LiveObjective>(StringComparer.Ordinal);
            foreach (var quest in controller.Quests)
            {
                if (quest?.Template == null || !targets.QuestIds.Contains(quest.Id)) continue;
                if (quest.QuestStatus != EQuestStatus.Started || !controller.IsQuestForCurrentProfile(quest)) continue;
                foreach (var condition in quest.NecessaryConditions)
                {
                    if (condition == null) continue;
                    var id = condition.id.ToString();
                    // 검사기가 없으면 IsConditionDone 이 예외를 던지므로 건너뛴다.
                    if (!targets.ConditionIds.Contains(id) || !quest.ProgressCheckers.TryGetValue(condition, out var checker)) continue;
                    live[id] = new LiveObjective(id, checker.CurrentValue, quest.IsConditionDone(condition));
                }
            }
            return live;
        }
        catch (Exception e)
        {
            if (!_liveWarned) Log.Warn($"live quest progress unavailable: {e}");
            _liveWarned = true;
            return null;
        }
    }

    // 서버 objectives 에 오는 레이드 목표 4종, 제출 2종, 그 밖.
    private static GameObjectiveKind KindOf(Condition condition)
    {
        switch (condition)
        {
            case ConditionCounterCreator _:
            case ConditionLeaveItemAtLocation _:
            case ConditionPlaceBeacon _:
            case ConditionVisitPlace _:
                return GameObjectiveKind.Raid;
            case ConditionHandoverItem _:
            case ConditionWeaponAssembly _:
                return GameObjectiveKind.Handover;
            default:
                return GameObjectiveKind.Other;
        }
    }

    // ConditionItem(건네주기, 찾기, 무기 조립)의 대상 아이템 이름. 키는 게임 LocalizedName(MongoID) 와 같다.
    // 키가 없으면 LocalizedValue 가 키를 그대로 돌려주므로 그런 이름은 뺀다. 실패하면 빈 목록.
    private static IReadOnlyList<string> ItemNames(Condition condition)
    {
        try
        {
            if (!(condition is ConditionItem item) || item.target == null) return Array.Empty<string>();
            var names = new List<string>();
            foreach (var tpl in item.target)
            {
                if (string.IsNullOrEmpty(tpl)) continue;
                var key = tpl + " Name";
                var name = LocalizationManager.Instance.LocalizedValue(key);
                if (string.IsNullOrWhiteSpace(name) || name == key || names.Contains(name)) continue;
                names.Add(name);
            }
            return names;
        }
        catch (Exception)
        {
            return Array.Empty<string>();
        }
    }

    // 게임 메뉴의 상인 목록은 Traders.Where(t => !t.Settings.AvailableInRaid) 다. BTR 운전사, 등대지기, 이야기꾼처럼
    // 레이드 안에서만 쓰는 상인은 메뉴 상인 화면이 없어 건네주기도 메뉴에서 받지 않는다. 설정에 없는 상인은 메뉴 상인으로 본다.
    private static bool InRaidOnlyTrader(Quest quest) =>
        Singleton<GlobalConfiguration>.Instance.TradersSettings.TryGetValue(quest.Template.TraderId, out var s) && s.AvailableInRaid;

    // Goals, TurnIns, HandoverAsync 가 같이 쓴다.
    private static bool IsMenuQuest(QuestController controller, Quest quest, EQuestStatus status) =>
        quest?.Template != null && quest.QuestStatus == status && controller.IsQuestForCurrentProfile(quest) && !InRaidOnlyTrader(quest);

    // 무기 조립 목표에 낼 수 있는 아이템
    private static Item[] AssemblyItems(Profile profile, ConditionWeaponAssembly assembly) =>
        Inventory.GetWeaponAssembly(profile.Inventory.GetPlayerItems(EPlayerItems.NonQuestItemsExceptHideoutStashes), assembly).ToArray();

    // QuestObjectiveView.OnConditionChanged 와 같다. 진행 중이고 안 끝난 목표만 들어온다.
    private static bool Ready(Condition condition, Profile profile)
    {
        switch (condition)
        {
            case ConditionWeaponAssembly assembly:
                return AssemblyItems(profile, assembly).Length >= assembly.value;
            case ConditionHandoverItem handover:
                var tpl = handover.target.FirstOrDefault();
                if (tpl == null || !Singleton<ItemFactory>.Instance.ItemTemplates.TryGetValue(tpl, out var template)) return false;
                if (template is MoneyTemplate)
                {
                    // 돈은 조금이라도 있으면 나눠 낼 수 있다(게임과 같다).
                    var sums = InventoryExtension.GetMoneySums(profile.Inventory.Stash.Grid.ContainedItems.Keys);
                    return sums.TryGetValue(CurrencyUtil.GetCurrencyTypeById(tpl), out var sum) && sum > 0;
                }
                return ConditionalController<Quest>.GetItemsForCondition(profile.Inventory, handover).Length > 0;
            default:
                return false;
        }
    }

    // QuestObjectiveView.QuestHandover 와 같다. 아이템이 전부 퀘스트 아이템이나 돈이면 창 없이 내고, 아니면 게임 선택 창을 띄운다.
    // 취소하면 아무것도 보내지 않는다. 예외를 밖으로 던지지 않는다.
    internal async Task HandoverAsync(string questId, string conditionId)
    {
        if (Busy) return;
        Busy = true;
        try
        {
            if (!CanHandover) return;
            var controller = GameRefs.QuestControllerOf(_screen);
            // 펼침을 그린 뒤 상태가 바뀌었을 수 있어 누르는 순간 다시 확인한다.
            var quest = controller.Quests.FirstOrDefault(q => q?.Id == questId && IsMenuQuest(controller, q, EQuestStatus.Started));
            var condition = quest?.NecessaryConditions.FirstOrDefault(c => c.id.ToString() == conditionId);
            if (quest == null || condition == null
                || !(condition is ConditionHandoverItem || condition is ConditionWeaponAssembly) || quest.IsConditionDone(condition))
            {
                Log.Warn($"handover: quest {questId} condition {conditionId} not handable (not started, in-raid trader, not found or done)");
                return;
            }
            var profile = controller.Profile;
            var items = condition is ConditionWeaponAssembly assembly
                ? AssemblyItems(profile, assembly)
                : ConditionalController<Quest>.GetItemsForCondition(profile.Inventory, (ConditionItem)condition);
            if (items == null || items.Length == 0)
            {
                Log.Warn($"handover: no items for quest {questId} condition {conditionId}");
                return;
            }
            if (!items.All(i => i.QuestItem || CurrencyUtil.IsCurrencyId(i.TemplateId)))
            {
                items = await PickAsync(quest, (ConditionItem)condition, items, profile);
                if (items == null || items.Length == 0) return; // 창을 닫았다
            }
            var result = await controller.HandoverItem(quest, (ConditionItem)condition, items, true);
            if (result.Failed) Log.Warn($"handover failed: quest {questId} condition {conditionId}: {BriefingClient.Scrub(result.Error?.ToString())}");
            else Log.Info($"handover ok: quest {questId} condition {conditionId}, {items.Length} item(s)");
        }
        catch (Exception e)
        {
            Log.Error($"handover failed: {BriefingClient.Scrub(e.ToString())}");
        }
        finally
        {
            _picking = null;
            Busy = false;
        }
    }

    private Task<Item[]> PickAsync(Quest quest, ConditionItem condition, Item[] items, Profile profile)
    {
        // 창이 먼저 닫히도록 HandoverItem 은 Accept 안에서 바로 돌리지 않고 다음 틱으로 미룬다.
        var picking = new TaskCompletionSource<Item[]>(TaskCreationOptions.RunContinuationsAsynchronously);
        _picking = picking;
        var window = ItemUiContext.Instance.HandoverQuestItemsWindow;
        var context = window.Show(condition, quest.ProgressCheckers[condition].CurrentValue, items, profile,
            GameRefs.InventoryControllerOf(_screen), picked => picking.TrySetResult(picked), true);
        context.OnDecline += () => picking.TrySetResult(null);
        return picking.Task;
    }

    // 탭이나 화면을 닫을 때 선택 창도 닫는다(QuestObjectiveView.Close 와 같다). 창이 닫힘을 알리지 않을 수 있어
    // 기다리던 제출은 취소로 끝낸다.
    internal void CloseWindow()
    {
        if (_picking == null) return;
        try
        {
            var window = ItemUiContext.Instance != null ? ItemUiContext.Instance.HandoverQuestItemsWindow : null;
            if (window != null && window.gameObject.activeSelf) window.Close();
        }
        catch (Exception e)
        {
            Log.Warn($"handover window close failed: {e.Message}");
        }
        _picking?.TrySetResult(null);
    }
}
