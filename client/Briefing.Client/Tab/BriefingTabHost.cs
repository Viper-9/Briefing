using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Briefing.Core.Screen;
using Briefing.Game;
using Briefing.Patches;
using Briefing.Ui;
using EFT.UI;
using HarmonyLib;
using UnityEngine;

namespace Briefing.Tab;

// InventoryScreen 에 붙는 컴포넌트. 화면이 열릴 때마다 탭이 없으면 만들고 게임 탭과의 선택을 이어 준다.
// 브리핑은 탭 줄에 따로 보이지 않고 임무 탭 드롭다운(TabDropdown)에서 고른다. 선택에 쓰는 브리핑 탭은 숨겨 두고 그대로 쓴다.
// 브리핑 탭은 게임 TabGroup 배열 밖이라 그룹이 모른다. 그래서 브리핑을 고르면 TabGroup.TryHide() 로 게임 탭을 내린 뒤 Select 하고,
// 게임 탭을 누르면 그룹이 그 탭을 고르게 두고 OnSelectionChanged 를 들어 브리핑 탭만 내린다.
internal sealed class BriefingTabHost : MonoBehaviour
{
    private const string TabName = "BriefingTab";
    private const string PanelName = "BriefingPanel";
    private static readonly FieldInfo TabsField = AccessTools.Field(typeof(InventoryScreen), "_tabs");

    // Plugin.Awake 가 정한다. 탭을 만들 때마다 새 뷰 하나를 받는다.
    internal static Func<IBriefingView> ViewFactory = () => new BundleBriefingView();

    internal GameObject Panel { get; private set; }

    private InventoryScreen _screen;
    private global::Tab _tab;
    private IBriefingView _view;
    private readonly List<global::Tab> _nativeTabs = new();
    private bool _busy;
    private TabDropdown _dropdown;

    internal static void AttachOrRefresh(InventoryScreen screen)
    {
        try
        {
            var host = screen.GetComponent<BriefingTabHost>();
            if (host == null) host = screen.gameObject.AddComponent<BriefingTabHost>();
            host._screen = screen;
            host.StopAllCoroutines();
            host.RestoreContent(null); // 앞 코루틴이 가린 채로 멈췄을 수 있다
            TabReturnPatch.Clear();     // 이번 Show 가 새 그룹을 만들었으니 앞에서 맡아 둔 것은 버린다
            host.HideContentForReturn();
            host.StartCoroutine(host.AttachWhenReady());
        }
        catch (Exception e)
        {
            Log.Error($"tab attach failed: {e}");
        }
    }

    private IEnumerator AttachWhenReady()
    {
        // "완료하러 가기"로 연 상인 화면에서 돌아왔으면 브리핑 탭을 다시 고른다. 요청은 여기서 꺼내면 지워진다.
        var returnToBriefing = false;
        try { returnToBriefing = TraderJump.TakeReturnRequest(); }
        catch (Exception e) { Log.Warn($"turn in: return request read failed: {e.Message}"); }

        // ShowWithTab 코루틴이 탭 컨트롤러를 Init 할 때까지 최대 120프레임 기다린다.
        var created = false;
        for (var frame = 0; frame < 120 && !created; frame++)
        {
            yield return null;
            if (frame < 2) continue;
            if (TabsField.GetValue(_screen) == null || _screen._tabDictionary == null) continue;
            try
            {
                created = EnsureCreated();
            }
            catch (Exception e)
            {
                Log.Error($"briefing tab not created: {e}");
                DiscardPartial();
                if (returnToBriefing) Log.Warn("turn in: return to briefing tab failed (tab not created)");
                SettleHold();
                RestoreContent(null);
                yield break;
            }
        }
        if (!created)
        {
            Log.Warn("tab group was not ready after 120 frames; briefing tab skipped");
            if (returnToBriefing) Log.Warn("turn in: return to briefing tab failed (tab not created)");
            SettleHold();
            RestoreContent(null);
            yield break;
        }
        if (returnToBriefing) yield return ReturnToBriefing();
        SettleHold();
        RestoreContent(null); // 어떻게 끝났든 가린 채로 두지 않는다
    }

    // 사용자가 누른 것처럼 HandlePointerClick 으로 브리핑 탭을 고른다. 내용 영역은 Show 때 가려 두었으니
    // 게임이 기억한 탭은 보이지 않는다. 패널이 켜지는 프레임에 가린 것을 되돌린다(최대 2초).
    private IEnumerator ReturnToBriefing()
    {
        yield return null;
        try
        {
            if (TasksPanel.InRaid() || _tab == null || Panel == null)
            {
                Log.Warn("turn in: return to briefing tab failed (in raid or no tab)");
                RestoreContent(null);
                yield break;
            }
            if (!Panel.activeSelf) _tab.HandlePointerClick(false);
        }
        catch (Exception e)
        {
            Log.Warn($"turn in: return to briefing tab failed: {e.Message}");
            RestoreContent(null);
            yield break;
        }
        var deadline = Time.realtimeSinceStartup + 2f;
        while (Time.realtimeSinceStartup < deadline)
        {
            if (Panel != null && Panel.activeSelf)
            {
                TabReturnPatch.Release();
                RestoreContent(null);
                Log.Info("turn in: returned to briefing tab");
                yield break;
            }
            yield return null;
        }
        RestoreContent(null);
        Log.Warn("turn in: return to briefing tab failed (panel not shown)");
    }

    // ── 상인 화면에서 돌아오는 동안 내용 가리기 ──
    // 캐릭터 화면 Show 와 같은 프레임에 탭 내용 영역(_tasksScreen 의 부모, 브리핑 패널의 부모이기도 하다)을 CanvasGroup alpha 0 으로 가려
    // 브리핑 탭이 골라지기 전에 게임 탭이 보이지 않게 한다. 성공, 실패, 화면 닫힘, 2초 시간 초과 어느 경우든 원래 값으로 되돌린다.
    private const float HideSafetySeconds = 2f;
    private CanvasGroup _hiddenGroup;
    private float _prevAlpha;
    private bool _prevBlocksRaycasts, _prevInteractable;
    private bool _addedGroup;
    private float _hiddenAt;

    private void HideContentForReturn()
    {
        try
        {
            if (!TraderJump.PeekReturnRequest() || TasksPanel.InRaid()) return;
            // 탭 그룹의 첫 Show 를 맡아 두면 게임 탭이 아예 그려지지 않는다. 아래 CanvasGroup 가리기는 그게 안 될 때를 위한 것이다.
            if (TabsField.GetValue(_screen) is TabGroup tabs) TabReturnPatch.Arm(tabs);
            var container = _screen != null && _screen._tasksScreen != null ? _screen._tasksScreen.transform.parent : null;
            if (container == null) return;
            var group = container.GetComponent<CanvasGroup>();
            _addedGroup = group == null; // 직접 붙인 것이면 되돌릴 때 떼어 낸다
            if (_addedGroup) group = container.gameObject.AddComponent<CanvasGroup>();
            _prevAlpha = group.alpha;
            _prevBlocksRaycasts = group.blocksRaycasts;
            _prevInteractable = group.interactable;
            group.alpha = 0f;
            group.blocksRaycasts = false;
            _hiddenGroup = group;
            _hiddenAt = Time.realtimeSinceStartup;
        }
        catch (Exception e)
        {
            Log.Warn($"turn in: content hide failed: {e.Message}");
            RestoreContent(null);
        }
    }

    // reason 이 있으면 로그를 남긴다.
    private void RestoreContent(string reason)
    {
        var group = _hiddenGroup;
        _hiddenGroup = null;
        var added = _addedGroup;
        _addedGroup = false;
        if (group == null) return; // 가린 적이 없거나 이미 파괴됐다
        try
        {
            if (added)
            {
                Destroy(group);
            }
            else
            {
                group.alpha = _prevAlpha;
                group.blocksRaycasts = _prevBlocksRaycasts;
                group.interactable = _prevInteractable;
            }
            if (reason != null) Log.Warn(reason);
        }
        catch (Exception e) { Log.Warn($"turn in: content restore failed: {e.Message}"); }
    }

    // 코루틴이 어떻게 되든 가린 지 2초가 지나면 되돌린다.
    private void Update()
    {
        if (_hiddenGroup != null && Time.realtimeSinceStartup - _hiddenAt > HideSafetySeconds)
        {
            SettleHold();
            RestoreContent("turn in: content unhidden by timeout");
        }
    }

    // 맡아 둔 게임 탭 Show 가 있으면, 브리핑 탭이 열려 있을 때는 버리고 아니면 그대로 불러 빈 화면으로 남지 않게 한다.
    private void SettleHold()
    {
        if (!TabReturnPatch.Holding) { TabReturnPatch.Clear(); return; }
        if (Panel != null && Panel.activeSelf) TabReturnPatch.Release();
        else TabReturnPatch.Replay();
    }

    private bool EnsureCreated()
    {
        if (!_screen._tabDictionary.TryGetValue(EInventoryTab.Notes, out var notes) || notes == null) return false;

        if (_tab == null)
        {
            _dropdown = TabDropdown.Create(notes, () => Panel != null && Panel.activeSelf, TasksShown, PickTasks, PickBriefing);
            // 탭 줄에 놓지 않고 메뉴 밑에 꺼 둔다. 탭 줄을 배치하는 다른 모드가 세지 않는다.
            _tab = TabClone.Make(notes, _dropdown.Root, TabName, null);
            _tab.OnSelectionChanged += OnOurSelectionChanged;
            CreatePanel();
            Log.Info($"briefing tab created (inRaid={InRaid()})");
        }

        _tab.Init(new BriefingTabController(Panel, _view, ApplyLayout));
        _tab.SetInteractable(true);
        _tab.UpdateVisual(false);
        HideNow();
        RefreshDropdown(); // 게임 언어가 바뀌었을 수 있다
        SubscribeNativeTabs();
        return true;
    }

    // 실패하면 글자만 전의 값으로 남는다.
    private void RefreshDropdown()
    {
        UiKit.Guard("tab menu refresh", () =>
        {
            BundleBriefingView.UseGameLanguage();
            _dropdown.Refresh();
        });
    }

    private bool TasksShown() => _screen != null && _screen._tasksScreen != null && _screen._tasksScreen.gameObject.activeSelf;

    // 임무 화면이 이미 열려 있으면 그대로 둔다. 아니면 사용자가 임무 탭을 누른 것처럼 게임 그룹에 맡긴다.
    private void PickTasks()
    {
        if (Panel != null && !Panel.activeSelf && TasksShown()) return;
        if (_screen._tabDictionary.TryGetValue(EInventoryTab.Notes, out var notes) && notes != null) notes.HandlePointerClick(false);
    }

    private void PickBriefing()
    {
        if (_tab != null && Panel != null && !Panel.activeSelf) _tab.HandlePointerClick(false);
    }

    // 로그용이라 실패하면 null.
    private bool? InRaid()
    {
        try
        {
            var controller = GameRefs.ScreenControllerOf(_screen);
            if (controller == null) return null;
            var inRaid = AccessTools.Property(controller.GetType(), "InRaid");
            return inRaid?.GetValue(controller) as bool?;
        }
        catch { return null; }
    }

    // EnsureCreated 가 중간에 실패하면 반쯤 만든 것을 지우고 다음 Show 에서 다시 만든다.
    private void DiscardPartial()
    {
        try
        {
            if (_tab != null)
            {
                _tab.OnSelectionChanged -= OnOurSelectionChanged;
                Destroy(_tab.gameObject);
            }
            if (_dropdown != null) Destroy(_dropdown.gameObject);
            if (Panel != null) Destroy(Panel);
        }
        catch (Exception e) { Log.Warn($"discard partial briefing tab failed: {e.Message}"); }
        _tab = null;
        _dropdown = null;
        Panel = null;
        _view = null;
    }

    private void CreatePanel()
    {
        // 임무 화면(_tasksScreen)과 같은 부모·같은 영역에 둔다.
        var tasks = (RectTransform)_screen._tasksScreen.transform;
        var panel = new GameObject(PanelName, typeof(RectTransform), typeof(CanvasGroup));
        Panel = panel; // Build 가 실패해도 DiscardPartial 이 치울 수 있게 먼저 둔다
        panel.transform.SetParent(tasks.parent, false);
        var rt = (RectTransform)panel.transform;
        ApplyLayout(rt, tasks);
        panel.transform.SetAsLastSibling();
        _view = ViewFactory();
        if (_view.Build(_screen, rt) == null) throw new InvalidOperationException("briefing view build returned nothing (bundle missing or unloadable)");
        panel.SetActive(false);
    }

    // 가로는 부모 전체에서 좌우 여백을 빼고, 세로는 탭 줄 아래부터 하단 여백까지. 설정이 바뀌었을 수 있어 열 때마다 다시 잰다.
    internal void ApplyLayout()
    {
        try
        {
            if (Panel == null || _screen == null || _screen._tasksScreen == null) return;
            ApplyLayout((RectTransform)Panel.transform, (RectTransform)_screen._tasksScreen.transform);
        }
        catch (Exception e)
        {
            Log.Error($"briefing apply layout failed: {e}");
        }
    }

    private void ApplyLayout(RectTransform rt, RectTransform tasks)
    {
        var parent = rt.parent as RectTransform;
        float? tabBottom = null;
        try
        {
            if (parent != null && _screen._tabDictionary != null
                && _screen._tabDictionary.TryGetValue(EInventoryTab.Notes, out var notes) && notes != null
                && notes.transform.parent is RectTransform row)
            {
                var corners = new Vector3[4];
                row.GetWorldCorners(corners);
                tabBottom = corners.Min(c => parent.InverseTransformPoint(c).y);
            }
        }
        catch (Exception e) { Log.Warn($"tab row bottom edge failed: {e.Message}"); }

        if (tabBottom == null || parent == null)
        {
            Log.Warn("tab row rect not found; briefing panel falls back to the tasks screen rect");
            rt.anchorMin = tasks.anchorMin;
            rt.anchorMax = tasks.anchorMax;
            rt.pivot = tasks.pivot;
            rt.anchoredPosition = tasks.anchoredPosition;
            rt.sizeDelta = tasks.sizeDelta;
        }
        else
        {
            var side = Plugin.SideMargin?.Value ?? 20f;
            var top = Plugin.TopMargin?.Value ?? 8f;
            var bottom = Plugin.BottomMargin?.Value ?? 40f;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = new Vector2(side, bottom);
            rt.offsetMax = new Vector2(-side, -(parent.rect.yMax - tabBottom.Value + top));
        }
        Log.Info($"panel rect = {rt.rect}, canvas scale = {rt.GetComponentInParent<Canvas>()?.scaleFactor}");
    }

    private void SubscribeNativeTabs()
    {
        foreach (var t in _nativeTabs) if (t != null) t.OnSelectionChanged -= OnNativeSelectionChanged;
        _nativeTabs.Clear();
        _nativeTabs.AddRange(_screen._tabDictionary.Values.Where(t => t != null).Distinct());
        foreach (var t in _nativeTabs) t.OnSelectionChanged += OnNativeSelectionChanged;
    }

    private void OnOurSelectionChanged(global::Tab tab, bool shouldSelect)
    {
        if (!shouldSelect || _busy) return;
        _ = SelectOursAsync();
    }

    private async Task SelectOursAsync()
    {
        _busy = true;
        try
        {
            var group = (TabGroup)TabsField.GetValue(_screen);
            if (group != null && !await group.TryHide()) return;
            _tab.Select();
        }
        catch (Exception e)
        {
            Log.Error($"select briefing failed: {e}");
            HideNow();
        }
        finally
        {
            _busy = false;
        }
    }

    private void OnNativeSelectionChanged(global::Tab tab, bool shouldSelect)
    {
        if (shouldSelect && _dropdown != null) _dropdown.Close();
        if (!shouldSelect || Panel == null || !Panel.activeSelf) return;
        _ = DeselectOursAsync();
    }

    private async Task DeselectOursAsync()
    {
        try { await _tab.Deselect(); }
        catch (Exception e) { Log.Warn($"deselect briefing failed: {e.Message}"); HideNow(); }
    }

    private void HideNow()
    {
        if (Panel != null && Panel.activeSelf)
        {
            Panel.SetActive(false);
            try { _view?.OnHidden(); }
            catch (Exception e) { Log.Error($"briefing view OnHidden failed: {e}"); }
        }
        if (_tab != null) _tab.UpdateVisual(false);
    }

    // 캐릭터 화면이 닫히면 패널도 닫고 구독을 푼다. 다음 Show 에서 다시 붙는다.
    private void OnDisable()
    {
        StopAllCoroutines();
        TabReturnPatch.Clear(); // 화면이 닫혔으니 맡은 Show 는 부르지 않는다
        RestoreContent(null);
        HideNow();
        foreach (var t in _nativeTabs) if (t != null) t.OnSelectionChanged -= OnNativeSelectionChanged;
        _nativeTabs.Clear();
    }

    private void OnDestroy()
    {
        RestoreContent(null);
        if (_tab != null) _tab.OnSelectionChanged -= OnOurSelectionChanged;
    }
}
