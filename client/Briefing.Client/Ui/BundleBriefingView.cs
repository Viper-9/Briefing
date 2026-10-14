using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Briefing.Core.Contract;
using Briefing.Core.Fetch;
using Briefing.Core.Map;
using Briefing.Core.Screen;
using Briefing.Game;
using Briefing.Map;
using Briefing.Net;
using EFT;
using EFT.UI;
using TMPro;
using UnityEngine;

namespace Briefing.Ui;

// briefing.bundle 의 프리팹으로 화면을 만든다. 프리팹 경로가 하나라도 없으면 예외가 나고 BriefingTabHost 는 탭을 만들지 않는다.
// 탭이 켜질 때마다 다시 요청하고 그린다.
internal sealed class BundleBriefingView : IBriefingView
{
    private InventoryScreen _screen;
    private GameObject _root;
    private MapBarView _mapBar;
    private QuestListView _list;
    private StatusBarView _status;
    private MapView _map;
    private ColumnResize _columns;
    private RightColumnView _right;
    private GearReader _gear;
    private LeftColumnModel _model; // 마지막으로 그린 왼쪽 열. 지도 마커는 그 맵 묶음의 퀘스트다
    private BriefingResponse _response; // 마지막으로 그린 응답(레이드 중이면 실시간 진행이 반영된 것). 지도·오른쪽 열만 다시 그릴 때 쓴다
    private bool _hotFromList;      // HotQuestId 가 목록 줄에서 왔다. 목록을 다시 그리면 그 줄이 사라지므로 지운다
    private TraderFaces _faces;
    private QuestBridge _bridge;
    private bool _shown;

    public GameObject Build(InventoryScreen screen, RectTransform parent)
    {
        var go = BundleLoader.CreateScreen(screen, parent);
        if (go == null) return null;
        try
        {
            var root = go.transform;
            UseGameLanguage(); // 문구를 쓰기 전에 언어부터 맞춘다
            UiKit.Get<TMP_Text>(root, UiPaths.HeaderTrader).text = Texts.HeaderTrader;
            UiKit.Get<TMP_Text>(root, UiPaths.HeaderQuest).text = Texts.HeaderQuest;
            UiKit.Get<TMP_Text>(root, UiPaths.HeaderProgress).text = Texts.HeaderProgress;

            _faces = new TraderFaces(() => GameRefs.ProfileOf(screen));
            _gear = new GearReader(() => GameRefs.ProfileOf(screen));
            _mapBar = new MapBarView(root, PickMap);
            _bridge = new QuestBridge(screen);
            var detail = new QuestDetailView(root, questId => TasksJump.Open(_screen, questId),
                questId => TraderJump.Open(_screen, questId), line => _ = HandoverAndRefreshAsync(line), () => _bridge.Busy);
            _list = new QuestListView(root, _faces, detail, () => Render(false), OnQuestClick, OnRowHot);
            _status = new StatusBarView(root, () => _ = RefreshAndRenderAsync());
            UiKit.Guard("map view", () => _map = new MapView(root, RenderMap, OnMarkerHot, OnMarkerClick)); // 실패하면 지도만 빠진다
            UiKit.Guard("column resize", () => _columns = new ColumnResize(root)); // 실패하면 손잡이만 빠진다
            UiKit.Guard("right column", () => _right = new RightColumnView(root, RenderRight)); // 실패하면 오른쪽 열만 빠진다
            // 생성자가 중간에 실패하면 프리팹의 견본 데이터가 그대로 보이므로 따로 지운다.
            if (_right == null) UiKit.Guard("right column samples", () => UiKit.DestroySamples(UiKit.Child(root, UiPaths.RightContent)));
            _screen = screen;
            _root = go;
            return go;
        }
        catch
        {
            UnityEngine.Object.Destroy(go);
            throw;
        }
    }

    // 요청에 쓰는 언어와 같게 맞춘다. 실패하면 직전 언어 그대로.
    internal static void UseGameLanguage()
    {
        UiKit.Guard("screen language", () =>
            Texts.UseLanguage(BriefingRequest.LangFor(LocalizationManager.Instance.Culture)));
    }

    public void OnShown()
    {
        _shown = true;
        UiKit.Guard("column widths", () => _columns?.Apply()); // 설정 값이 다른 곳에서 바뀌었을 수 있다
        UiKit.Guard("map retry", () => _map?.Retry()); // 지난번에 못 받은 지도와 층을 다시 받는다
        UiKit.Guard("gear stash", () => _gear?.ScanStash()); // 보관함은 무거워서 탭이 켜질 때 한 번만 읽는다
        // 레이드 때문에 미룬 지도 미리 그려 두기를 메뉴에서 이어 간다.
        UiKit.Guard("map precache resume", () => MapPrecache.Resume(BriefingClient.Cache.Last));
        _ = RefreshAndRenderAsync();
    }

    public void OnHidden()
    {
        _shown = false;
        BriefingState.HotQuestId = null; // 숨기면 마우스 내림 알림이 오지 않을 수 있다
        _hotFromList = false;
        _faces?.CancelAll();
        _bridge?.CloseWindow();
        UiKit.Guard("map menus close", () => _map?.CloseMenus());
        UiKit.Guard("right tooltip close", () => _right?.HideTip()); // 숨긴 칸에는 마우스 내림 알림이 오지 않는다
    }

    // 요청을 보내고 지금 상태로 한 번, 응답이 오면 한 번 더 그린다. BriefingClient 의 Task 는 예외로 끝나지 않는다.
    // 레이드 중에는 서버에 요청하지 않는다. RaidStartPatch 가 보낸 요청이 아직 오는 중이면 그것만 기다리고,
    // 아니면 마지막 응답으로 한 번만 그린다.
    private async Task RefreshAndRenderAsync()
    {
        var request = RaidInfo.InRaid() ? BriefingClient.Pending : BriefingClient.RefreshAsync();
        Render(false);
        if (request == null) return;
        await request;
        Render(false);
    }

    // 제출이 끝나면 브리핑을 다시 받아 그린다. 시작하자마자 한 번 그려서 버튼이 잠긴 것을 보인다.
    private async Task HandoverAndRefreshAsync(DetailLine line)
    {
        var handover = _bridge.HandoverAsync(line.QuestId, line.ConditionId);
        Render(false);
        await handover;
        await RefreshAndRenderAsync();
    }

    private void PickMap(string key)
    {
        BriefingState.SelectedMapKey = key;
        BriefingState.RightOpenRows.Clear(); // 맵을 바꾸면 "외 N종" 상자는 모두 닫힌다
        Render(true);
    }

    private void Render(bool scrollToTop)
    {
        if (!_shown || _root == null) return; // 숨겨져 있으면 다음 OnShown 이 그린다. 파괴됐으면 _root 는 Unity null
        RenderAll(scrollToTop, RaidInfo.InRaid());
    }

    private void RenderAll(bool scrollToTop, bool inRaid)
    {
        UseGameLanguage();
        if (_hotFromList)
        {
            // 목록 줄을 다시 만들면 올려 둔 줄이 사라져 내림 알림이 오지 않는다. 마우스 아래 새 줄이 다음 프레임에 다시 알린다.
            BriefingState.HotQuestId = null;
            _hotFromList = false;
        }
        var cache = BriefingClient.Cache;
        var response = cache.Last;
        _response = response;
        StatusLine line = null;
        // 하나가 실패해도 나머지는 그려지도록 부분마다 따로 감싼다.
        UiKit.Guard("render status", () =>
        {
            line = StatusLine.For(cache, BriefingClient.Loading, DateTimeOffset.UtcNow, inRaid);
            _status.Render(line);
        });
        if (line == null) return;
        if (!line.ShowList || response == null)
        {
            UiKit.Guard("clear map bar", () => _mapBar.Clear());
            UiKit.Guard("clear list", () =>
            {
                _list.Clear();
                _list.SetVisible(false);
            });
            _model = null;
            RenderMap();
            _rightDrawnFor = null; // 비웠으니 다음 레이드 그리기는 건너뛰지 않는다
            RenderRight();
            return;
        }
        // 레이드 중에는 처음 그릴 때 한 번 레이드 맵을 고르고, 보이는 퀘스트의 진행을 게임 값으로 덮어쓴다.
        string raidMapKey = null;
        if (inRaid)
        {
            var moved = false;
            UiKit.Guard("raid map", () => moved = PickRaidMap(response, out raidMapKey));
            if (moved) scrollToTop = true;
            UiKit.Guard("live progress", () => response = ApplyLive(response)); // 실패하면 응답 값 그대로
            _response = response;
        }
        IReadOnlyList<HandoverGoal> goals = null;
        UiKit.Guard("handover goals", () => goals = _bridge.Goals()); // 실패해도 건네주기만 빠진다
        // 펼친 퀘스트의 게임 목표. 실패하면 null 이고 펼침은 서버 목표와 건네주기 줄로 그린다.
        IReadOnlyList<GameObjective> objectives = null;
        if (BriefingState.ExpandedQuestId != null)
            UiKit.Guard("quest objectives", () => objectives = _bridge.Objectives(BriefingState.ExpandedQuestId));
        // 실패하면 "완료 가능" 묶음만 빠진다.
        IReadOnlyList<TurnInQuest> turnIns = null;
        UiKit.Guard("turn-in quests", () => turnIns = _bridge.TurnIns());
        LeftColumnModel model = null;
        UiKit.Guard("build model", () => model = LeftColumn.Build(response, BriefingState.SelectedMapKey,
            goals, BriefingState.ExpandedSection, BriefingState.ExpandedQuestId, objectives, turnIns, raidMapKey));
        _model = model;
        if (model == null)
        {
            RenderMap(); // 모델이 없으면 지도와 오른쪽 열도 비운다
            _rightDrawnFor = null;
            RenderRight();
            return;
        }
        UiKit.Guard("render map bar", () => _mapBar.Render(model.Chips));
        UiKit.Guard("render list", () =>
        {
            _list.SetVisible(true);
            _list.Render(model, scrollToTop);
            _list.SetHot(BriefingState.HotQuestId); // 마커에 마우스를 올린 채 다시 그렸으면 줄 강조를 되살린다
        });
        RenderMap();
        var skipRight = false;
        UiKit.Guard("right skip", () => skipRight = RightUnchangedInRaid(inRaid));
        if (!skipRight) RenderRight();
    }

    // 레이드에서 처음 그릴 때 한 번만 고른 맵을 레이드 맵으로 옮긴다. 펼침과 필터는 그대로 둔다.
    // raidMapKey 는 레이드 맵 칩 키이고 못 찾으면 null. 맵을 옮겼으면 true.
    private static bool PickRaidMap(BriefingResponse response, out string raidMapKey)
    {
        var location = RaidInfo.LocationId();
        raidMapKey = RaidMap.Match(response, location);
        if (BriefingState.RaidMapPicked) return false;
        BriefingState.RaidMapPicked = true;
        if (raidMapKey == null)
        {
            Log.Warn($"raid map: location '{location}' is not in the briefing maps — map choice kept");
            return false;
        }
        Log.Info($"raid map: {location} -> {raidMapKey}");
        if (BriefingState.SelectedMapKey == raidMapKey) return false;
        BriefingState.SelectedMapKey = raidMapKey;
        BriefingState.RightOpenRows.Clear(); // PickMap 과 같다
        return true;
    }

    // 부하를 줄이려고 보이는 퀘스트(고른 맵과 아무 맵에서나)의 목표만 읽는다. 바뀐 게 없으면 같은 응답을 돌려준다.
    private BriefingResponse ApplyLive(BriefingResponse response)
    {
        var targets = LiveProgress.Targets(LeftColumn.SelectedMap(response, BriefingState.SelectedMapKey), response.Anywhere);
        return LiveProgress.Apply(response, _bridge.LiveObjectives(targets));
    }

    // 레이드 중 준비물·수집 탭은 보유 수가 레이드 전 값이라 응답, 맵, 필터, 탭이 같으면 내용도 같다. 그때는 오른쪽 열을 건너뛴다.
    // 장비 탭은 장착이 바뀔 수 있어 늘 다시 그린다. 탭 클릭, ×, "외 N종"은 RenderRight 를 바로 부른다.
    private object _rightDrawnFor;

    private bool RightUnchangedInRaid(bool inRaid)
    {
        if (!inRaid || BriefingState.RightTab == RightTabKey.Gear)
        {
            _rightDrawnFor = null;
            return false;
        }
        var (filterId, _) = RightColumn.Filter(_model, BriefingState.RightFilterOff);
        object key = (BriefingClient.Cache.Last, BriefingState.SelectedMapKey, filterId, BriefingState.RightTab);
        if (Equals(_rightDrawnFor, key)) return true;
        _rightDrawnFor = key;
        return false;
    }

    // 오른쪽 열만 다시 그린다. 필터는 마지막으로 그린 왼쪽 열의 펼친 줄에서 온다.
    private void RenderRight()
    {
        if (!_shown || _root == null || _right == null) return;
        var response = _response;
        var model = _model;
        UiKit.Guard("render right column", () =>
        {
            if (response == null || model == null)
            {
                _right.Clear(true);
                return;
            }
            var map = LeftColumn.SelectedMap(response, BriefingState.SelectedMapKey);
            // 장비를 바꾸고 돌아오면 바로 반영되도록 장착은 그릴 때마다 읽는다. 실패하면 판정 없이 그린다.
            GearSnapshot gear = null;
            IReadOnlyList<LoadoutSlot> loadout = null;
            UiKit.Guard("gear read", () =>
            {
                gear = _gear?.Snapshot();
                loadout = _gear?.Loadout();
            });
            var (filterId, filterName) = RightColumn.Filter(model, BriefingState.RightFilterOff);
            _right.Render(RightColumn.Build(response, map, filterId, filterName, GameNames.Quest, gear), loadout);
        });
    }

    // 지도 칸만 다시 그린다. 마커는 왼쪽 맵 묶음에 보이는 퀘스트만이라 "완료 가능"으로 옮겨 간 것은 빠진다.
    private void RenderMap()
    {
        if (!_shown || _root == null || _map == null) return;
        var response = _response;
        var model = _model;
        UiKit.Guard("render map", () =>
        {
            if (response == null || model == null)
            {
                _map.Render(null, null, null, null);
                return;
            }
            var shown = new HashSet<string>(model.Here?.Rows.Select(r => r.QuestId) ?? Enumerable.Empty<string>());
            var emphasis = MapScene.EmphasisQuest(BriefingState.HotQuestId, BriefingState.ExpandedSection, BriefingState.ExpandedQuestId, shown);
            _map.Render(response, LeftColumn.SelectedMap(response, BriefingState.SelectedMapKey), shown, emphasis);
        });
    }

    // 맵 묶음 줄을 펼쳤으면 지도를 그 퀘스트의 층과 위치로 옮긴다. 확대는 하지 않는다.
    private void OnQuestClick(string section, string questId)
    {
        BriefingState.ToggleExpanded(section, questId);
        var expanded = BriefingState.ExpandedSection == section && BriefingState.ExpandedQuestId == questId;
        if (expanded && section == LeftColumn.HereKey) UiKit.Guard("map focus", () => _map?.Focus(questId));
        else UiKit.Guard("map focus cancel", () => _map?.CancelFocus());
        Render(false);
    }

    // 맵 묶음 줄을 누른 것과 같이 펼치고 그 줄까지 스크롤한다. 지도 시점은 그대로.
    private void OnMarkerClick(string questId)
    {
        BriefingState.ToggleExpanded(LeftColumn.HereKey, questId);
        BriefingState.Collapsed.Remove(LeftColumn.HereKey);
        Render(false);
        UiKit.Guard("list scroll", () => _list.ScrollTo(LeftColumn.HereKey, questId));
    }

    // 목록은 그대로 두고 지도 강조만 다시 그린다.
    private void OnRowHot(string questId)
    {
        BriefingState.HotQuestId = questId;
        _hotFromList = questId != null;
        RenderMap();
    }

    // 지도 강조와 함께 목록의 그 줄도 밝힌다.
    private void OnMarkerHot(string questId)
    {
        BriefingState.HotQuestId = questId;
        _hotFromList = false;
        RenderMap();
        UiKit.Guard("list hot", () => _list.SetHot(questId));
    }
}
