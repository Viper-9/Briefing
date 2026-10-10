using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Briefing.Ui;
using EFT.UI;
using HarmonyLib;
using TMPro;
using UnityEngine;

namespace Briefing.Tab;

// InventoryScreen 에 붙어 사는 컴포넌트. 화면이 열릴 때마다 탭을 (없으면) 만들고, 우리 탭 ↔ 게임 탭 선택을 중개한다.
// 우리 탭은 게임 TabGroup 의 배열 밖이라 그룹이 우리를 모른다:
//  - 우리 탭 클릭 → TabGroup.TryHide()(현재 게임 탭 Deselect) 성공 시 우리 탭 Select.
//  - 게임 탭 클릭 → 그룹이 알아서 그 탭을 Select. 우리는 그 탭의 OnSelectionChanged 를 듣고 우리 탭만 Deselect.
internal sealed class BriefingTabHost : MonoBehaviour
{
    private const string TabName = "BriefingTab";
    private const string PanelName = "BriefingPanel";
    private static readonly FieldInfo TabsField = AccessTools.Field(typeof(InventoryScreen), "_tabs");
    private static readonly FieldInfo SelectionEventField = AccessTools.Field(typeof(global::Tab), "_onSelectionChanged");

    // Plugin.Awake 가 정한다. 탭을 만들 때마다 새 뷰 하나를 받는다.
    internal static Func<IBriefingView> ViewFactory = () => new BundleBriefingView();

    internal GameObject Panel { get; private set; }

    private InventoryScreen _screen;
    private global::Tab _tab;
    private IBriefingView _view;
    private readonly List<global::Tab> _nativeTabs = new();
    private bool _busy;

    internal static void AttachOrRefresh(InventoryScreen screen)
    {
        try
        {
            var host = screen.GetComponent<BriefingTabHost>();
            if (host == null) host = screen.gameObject.AddComponent<BriefingTabHost>();
            host._screen = screen;
            host.StopAllCoroutines();
            host.StartCoroutine(host.AttachWhenReady());
        }
        catch (Exception e)
        {
            Log.Error($"tab attach failed: {e}");
        }
    }

    private IEnumerator AttachWhenReady()
    {
        // ShowWithTab 코루틴이 탭 컨트롤러를 Init 할 때까지 기다린다(최대 120프레임).
        for (var frame = 0; frame < 120; frame++)
        {
            yield return null;
            if (frame < 2) continue;
            if (TabsField.GetValue(_screen) == null || _screen._tabDictionary == null) continue;
            try
            {
                if (EnsureCreated()) yield break;
            }
            catch (Exception e)
            {
                Log.Error($"briefing tab not created: {e}");
                DiscardPartial();
                yield break;
            }
        }
        Log.Warn("tab group was not ready after 120 frames; briefing tab skipped");
    }

    private bool EnsureCreated()
    {
        if (!_screen._tabDictionary.TryGetValue(EInventoryTab.Notes, out var notes) || notes == null) return false;
        var row = notes.transform.parent;

        // 줄 배치를 먼저 확인한다: 준비 안 되면 우리 탭이 임무 탭과 겹치므로 탭을 만들지 않는다.
        var layout = row.GetComponent<TabRowLayout>();
        if (layout == null) layout = row.gameObject.AddComponent<TabRowLayout>();
        if (!layout.Configure(_screen._tabDictionary.Values))
            throw new InvalidOperationException("tab row layout not ready (cannot place the tab without overlapping)");

        if (_tab == null)
        {
            // 비활성 임시 부모 밑에서 복제해 복제본의 Awake/OnEnable 이 정리 전에 돌지 않게 한다.
            var holder = new GameObject("BriefingTabHolder");
            holder.SetActive(false);
            try
            {
                var go = Instantiate(notes.gameObject, holder.transform, false);
                go.SetActive(false); // 끝까지 성공할 때만 켠다(중간 실패 시 겹친 빈 탭이 남지 않게)
                go.name = TabName;
                _tab = go.GetComponent<global::Tab>();
                SelectionEventField?.SetValue(_tab, null); // 복제본에 이벤트 구독이 딸려 오지 않게
                foreach (var lt in go.GetComponentsInChildren<LocalizedText>(true)) DestroyImmediate(lt);
                _tab.LocalizedText = null;
                foreach (var t in go.GetComponentsInChildren<TMP_Text>(true))
                    if (!string.IsNullOrWhiteSpace(t.text)) t.text = "브리핑"; // 0단계: 고정 문구. i18n 은 5단계
                _tab.OnSelectionChanged += OnOurSelectionChanged;
                go.transform.SetParent(row, false);
            }
            finally
            {
                Destroy(holder);
            }

            CreatePanel();
            Log.Info($"briefing tab created (inRaid={InRaid()})");
        }

        // 임무 탭 바로 오른쪽: x 를 임무 탭 + 0.1 로 두면 TabRowLayout 정렬에서 임무 탭 다음 자리가 된다.
        var notesRt = (RectTransform)notes.transform;
        var ourRt = (RectTransform)_tab.transform;
        ourRt.anchoredPosition = new Vector2(notesRt.anchoredPosition.x + 0.1f, notesRt.anchoredPosition.y);

        _tab.Init(new BriefingTabController(Panel, _view, ApplyLayout));
        _tab.SetInteractable(true);
        _tab.UpdateVisual(false);
        _tab.gameObject.SetActive(true);
        Panel.SetActive(false);
        SubscribeNativeTabs();
        return true;
    }

    // ScreenController 는 BaseScreen<,,> 의 protected 필드라(프로퍼티 아님) 상속 사슬을 따라 올라가며 읽는다. 로그용이라 실패하면 null.
    private bool? InRaid()
    {
        try
        {
            for (var type = _screen.GetType(); type != null; type = type.BaseType)
            {
                var field = AccessTools.Field(type, "ScreenController");
                if (field == null || field.DeclaringType != type) continue;
                var controller = field.GetValue(_screen);
                if (controller == null) return null;
                var inRaid = AccessTools.Property(controller.GetType(), "InRaid");
                return inRaid?.GetValue(controller) as bool?;
            }
            return null;
        }
        catch { return null; }
    }

    // EnsureCreated 가 중간에 실패했을 때: 반쯤 만든 탭·패널을 지워 다음 Show 에서 깨끗이 다시 시작한다.
    private void DiscardPartial()
    {
        try
        {
            if (_tab != null)
            {
                _tab.OnSelectionChanged -= OnOurSelectionChanged;
                Destroy(_tab.gameObject);
            }
            if (Panel != null) Destroy(Panel);
        }
        catch (Exception e) { Log.Warn($"discard partial briefing tab failed: {e.Message}"); }
        _tab = null;
        Panel = null;
        _view = null;
    }

    private void CreatePanel()
    {
        // 임무 화면(_tasksScreen)과 같은 부모·같은 영역에 둔다.
        var tasks = (RectTransform)_screen._tasksScreen.transform;
        var panel = new GameObject(PanelName, typeof(RectTransform), typeof(CanvasGroup));
        Panel = panel; // Build 가 실패해도 DiscardPartial 이 치울 수 있게 먼저 보관
        panel.transform.SetParent(tasks.parent, false);
        var rt = (RectTransform)panel.transform;
        ApplyLayout(rt, tasks);
        panel.transform.SetAsLastSibling();
        _view = ViewFactory();
        if (_view.Build(_screen, rt) == null) throw new InvalidOperationException("briefing view build returned nothing (bundle missing or unloadable)");
        panel.SetActive(false);
    }

    // 패널 영역: 부모 가로 전체(좌우 여백) + 탭 줄 바로 아래부터 하단 여백 위까지. 탭이 열릴 때마다 다시 계산한다(설정 변경 반영).
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

    // 캐릭터 화면이 닫히면(비활성) 우리 패널도 닫고 구독을 푼다. 다음 Show 에서 다시 붙는다.
    private void OnDisable()
    {
        StopAllCoroutines();
        HideNow();
        foreach (var t in _nativeTabs) if (t != null) t.OnSelectionChanged -= OnNativeSelectionChanged;
        _nativeTabs.Clear();
    }

    private void OnDestroy()
    {
        if (_tab != null) _tab.OnSelectionChanged -= OnOurSelectionChanged;
    }
}
