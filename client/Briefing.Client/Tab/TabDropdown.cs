using System;
using System.Collections.Generic;
using Briefing.Core.Screen;
using Briefing.Patches;
using Briefing.Ui;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Briefing.Tab;

// 임무 탭 드롭다운. 임무 탭을 누르면 그 아래로 임무 탭을 복제한 두 항목(임무, 브리핑)이 미끄러져 펼쳐진다.
// 탭 줄 밑에 두지만 자기 Canvas(overrideSorting)로 그려 아래 화면 내용에 가리지 않는다. 탭 줄의 직접 자식 중 켜진 Tab 이 없게 해서
// 탭 줄을 다시 배치하는 다른 모드(Operator Traits)가 이 메뉴를 탭으로 세지 않는다.
// 임무 탭 글자는 지금 보는 쪽("임무 ▼" / "브리핑 ▼")을 따르고, 브리핑을 보는 동안 임무 탭을 고른 모양으로 칠한다.
// 메뉴가 열려 있거나 브리핑을 보는 동안에는 게임이 고른 탭에 하듯 임무 탭을 탭 줄 맨 뒤로 옮겨 옆 탭 위에 그린다.
internal sealed class TabDropdown : MonoBehaviour
{
    private const string MenuName = "BriefingTabMenu";
    private const float OpenSeconds = 0.14f;
    private const float CloseSeconds = 0.09f;
    private const float Gap = 2f; // 항목 사이와 임무 탭과의 간격
    private const char ArrowGlyph = '▼';
    private static readonly Color BackingColor = new Color(0.04f, 0.05f, 0.05f, 0.92f);

    private global::Tab _notes;
    private RectTransform _notesRt;
    private RectTransform _root, _clip, _slide;
    private CanvasGroup _fade;
    private Canvas _canvas;
    private readonly List<TMP_Text> _notesLabels = new();
    private readonly List<global::Tab> _items = new();
    private readonly List<List<TMP_Text>> _itemLabels = new();
    private Func<bool> _briefingShown, _tasksShown;
    private string _tasksFallback; // 임무 탭 원래 글자. LocalizedText 를 못 읽을 때 쓴다
    private string _arrow = "";
    private string _briefingTitle = "";
    private bool _open;
    private float _t; // 펼침 정도 0..1
    private bool _paintedBriefing;
    private bool _failed;
    private int _restoreIndex = -1; // 임무 탭을 올리기 전 자리. 올리지 않았으면 -1

    internal Transform Root => _root;

    internal static TabDropdown Create(global::Tab notes, Func<bool> briefingShown, Func<bool> tasksShown, Action pickTasks, Action pickBriefing)
    {
        var go = new GameObject(MenuName, typeof(RectTransform));
        try
        {
            go.transform.SetParent(notes.transform.parent, false);
            var dd = go.AddComponent<TabDropdown>();
            dd.Build(notes, briefingShown, tasksShown, pickTasks, pickBriefing);
            return dd;
        }
        catch
        {
            Destroy(go);
            throw;
        }
    }

    private void Build(global::Tab notes, Func<bool> briefingShown, Func<bool> tasksShown, Action pickTasks, Action pickBriefing)
    {
        _notes = notes;
        _notesRt = (RectTransform)notes.transform;
        _briefingShown = briefingShown;
        _tasksShown = tasksShown;
        _root = (RectTransform)transform;

        foreach (var t in notes.GetComponentsInChildren<TMP_Text>(true))
            if (!string.IsNullOrWhiteSpace(t.text)) _notesLabels.Add(t);
        _tasksFallback = _notesLabels.Count > 0 ? _notesLabels[0].text : "";

        _canvas = gameObject.AddComponent<Canvas>();
        gameObject.AddComponent<GraphicRaycaster>();
        ApplySorting();

        _clip = NewRect("Clip", _root);
        _clip.anchorMin = new Vector2(0f, 0f);
        _clip.anchorMax = new Vector2(1f, 0f);
        _clip.pivot = new Vector2(0.5f, 1f);
        _clip.gameObject.AddComponent<RectMask2D>();
        _fade = _clip.gameObject.AddComponent<CanvasGroup>();

        _slide = NewRect("Slide", _clip);
        _slide.anchorMin = Vector2.zero;
        _slide.anchorMax = Vector2.one;
        _slide.offsetMin = Vector2.zero;
        _slide.offsetMax = Vector2.zero;
        var backing = _slide.gameObject.AddComponent<Image>();
        backing.color = BackingColor; // 항목 사이로 아래 내용이 비치지 않게, 메뉴 위 클릭이 아래로 새지 않게

        AddItem("BriefingMenuTasks", () => Pick(pickTasks));
        AddItem("BriefingMenuBriefing", () => Pick(pickBriefing));

        TabClickPatch.Register(_notes, Toggle);
        Layout();
        CloseNow();
    }

    private void AddItem(string name, Action onClick)
    {
        var labels = new List<TMP_Text>();
        var tab = TabClone.Make(_notes, _slide, name, labels);
        var rt = (RectTransform)tab.transform;
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.localScale = Vector3.one;
        rt.localRotation = Quaternion.identity;
        tab.SetInteractable(true);
        tab.UpdateVisual(false);
        tab.gameObject.SetActive(true);
        TabClickPatch.Register(tab, onClick);
        _items.Add(tab);
        _itemLabels.Add(labels);
    }

    private static RectTransform NewRect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    // overrideSorting 은 꺼진 동안 정하면 풀릴 수 있어서 켜질 때마다 다시 정한다.
    private void ApplySorting()
    {
        if (_canvas == null || transform.parent == null) return;
        var parentCanvas = transform.parent.GetComponentInParent<Canvas>();
        _canvas.overrideSorting = true;
        if (parentCanvas == null) return;
        _canvas.sortingLayerID = parentCanvas.sortingLayerID;
        _canvas.sortingOrder = parentCanvas.sortingOrder + 1;
        _canvas.additionalShaderChannels = parentCanvas.rootCanvas.additionalShaderChannels;
    }

    // 게임 언어가 바뀌었을 수 있어 캐릭터 화면을 열 때마다 부른다.
    internal void Refresh()
    {
        _briefingTitle = Texts.TabTitle;
        _arrow = ArrowText();
        CloseNow();
        SyncNotes();
    }

    private string ArrowText()
    {
        var label = _notesLabels.Count > 0 ? _notesLabels[0] : null;
        if (label == null || label.font == null) return "";
        if (!label.font.HasCharacter(ArrowGlyph, true, true)) return "";
        return label.richText ? $" <size=60%>{ArrowGlyph}</size>" : $" {ArrowGlyph}";
    }

    private string TasksTitle()
    {
        var localized = _notes != null && _notes.LocalizedText != null ? _notes.LocalizedText.FormattedText : null;
        return string.IsNullOrEmpty(localized) ? _tasksFallback : localized;
    }

    private void Toggle()
    {
        if (_open) Close();
        else Open();
    }

    private void Open()
    {
        var briefing = _briefingShown();
        var tasksTitle = TasksTitle();
        SetLabels(_itemLabels[0], tasksTitle);
        SetLabels(_itemLabels[1], _briefingTitle);
        _items[0].UpdateVisual(!briefing && _tasksShown());
        _items[1].UpdateVisual(briefing);
        Layout();
        _open = true;
        _clip.gameObject.SetActive(true);
        ApplyAnimation();
    }

    internal void Close()
    {
        if (!_open) return;
        _open = false;
        ResetHover();
        ApplyAnimation();
    }

    private void CloseNow()
    {
        _open = false;
        _t = 0f;
        ResetHover();
        ApplyAnimation();
        if (_clip != null) _clip.gameObject.SetActive(false);
    }

    // 누르면 메뉴가 꺼지면서 마우스 올림 모양이 남으므로 지운다.
    private void ResetHover()
    {
        foreach (var item in _items) if (item != null) item.Hover(false);
    }

    private void Pick(Action pick)
    {
        Close();
        pick();
    }

    private void ApplyAnimation()
    {
        if (_slide == null || _fade == null) return;
        var eased = 1f - Mathf.Pow(1f - _t, 3f);
        _slide.anchoredPosition = new Vector2(0f, (1f - eased) * _clip.rect.height);
        _fade.alpha = eased;
        _fade.blocksRaycasts = _open;
        _fade.interactable = _open;
    }

    private void Update()
    {
        if (!_open || _notes == null) return;
        UiKit.Guard("tab menu input", () =>
        {
            if (Input.GetKeyDown(KeyCode.Escape)) { Close(); return; }
            if (!Input.GetMouseButtonDown(0) && !Input.GetMouseButtonDown(1)) return;
            // 임무 탭 위 클릭은 TabClickPatch 가 열고 닫으므로 여기서는 건드리지 않는다.
            var cam = EventCamera();
            if (RectTransformUtility.RectangleContainsScreenPoint(_clip, Input.mousePosition, cam)) return;
            if (RectTransformUtility.RectangleContainsScreenPoint(_notesRt, Input.mousePosition, cam)) return;
            Close();
        });
    }

    private Camera EventCamera()
    {
        var root = _canvas != null ? _canvas.rootCanvas : null;
        return root == null || root.renderMode == RenderMode.ScreenSpaceOverlay ? null : root.worldCamera;
    }

    private void LateUpdate()
    {
        if (_notes == null || _failed) return;
        try
        {
            Layout();
            SyncNotes();
            var target = _open ? 1f : 0f;
            if (!Mathf.Approximately(_t, target))
            {
                _t = Mathf.MoveTowards(_t, target, Time.unscaledDeltaTime / (_open ? OpenSeconds : CloseSeconds));
                ApplyAnimation();
                if (!_open && _t <= 0f) _clip.gameObject.SetActive(false);
            }
            RaiseNotes(_open || _t > 0f || _briefingShown());
        }
        catch (Exception e)
        {
            _failed = true; // 매 프레임 로그가 쏟아지지 않게 멈춘다
            Log.Error($"briefing tab menu failed: {e}");
        }
    }

    // 임무 탭 자리를 따라간다. 다른 모드나 게임이 탭 줄을 다시 배치할 수 있어서 매 프레임 맞춘다.
    private void Layout()
    {
        if (_notesRt == null || _root == null) return;
        _root.anchorMin = _notesRt.anchorMin;
        _root.anchorMax = _notesRt.anchorMax;
        _root.pivot = _notesRt.pivot;
        _root.sizeDelta = _notesRt.sizeDelta;
        _root.anchoredPosition = _notesRt.anchoredPosition;
        _root.localScale = _notesRt.localScale;

        var h = _notesRt.rect.height;
        if (h <= 0f) return;
        var height = _items.Count * h + Mathf.Max(0, _items.Count - 1) * Gap;
        if (!Mathf.Approximately(_clip.sizeDelta.y, height))
        {
            _clip.anchoredPosition = new Vector2(0f, -Gap);
            _clip.sizeDelta = new Vector2(0f, height);
        }
        for (var i = 0; i < _items.Count; i++)
        {
            var rt = (RectTransform)_items[i].transform;
            rt.sizeDelta = new Vector2(0f, h);
            rt.anchoredPosition = new Vector2(0f, -i * (h + Gap));
        }
    }

    // 게임 TabGroup.SelectTab 은 고른 탭을 SetAsLastSibling 으로 맨 뒤에 둬서 옆 탭 위에 그린다. 임무 탭 클릭은 메뉴가 받아
    // 그 일이 일어나지 않으므로 같은 방식으로 올린다. 내릴 때는 그사이 게임이 순서를 바꾸지 않았을 때만 원래 자리로 돌린다.
    private void RaiseNotes(bool raise)
    {
        var t = _notes.transform;
        var parent = t.parent;
        if (parent == null) return;
        var last = t.GetSiblingIndex() == parent.childCount - 1;
        if (raise)
        {
            if (last) return;
            if (_restoreIndex < 0) _restoreIndex = t.GetSiblingIndex();
            t.SetAsLastSibling();
            return;
        }
        if (_restoreIndex < 0) return;
        if (last && !_tasksShown()) t.SetSiblingIndex(Mathf.Min(_restoreIndex, parent.childCount - 1));
        _restoreIndex = -1;
    }

    // 임무 탭 글자와, 브리핑을 보는 동안의 고른 모양. LocalizedText 가 언어 갱신 때 글자를 되돌려도 다음 프레임에 다시 맞춘다.
    // 고른 모양은 게임 상태(_selected)를 건드리지 않고 보이는 것만 바꾼다. 브리핑이 닫히면 UpdateVersion 이 게임 상태대로 되돌린다.
    private void SyncNotes()
    {
        var briefing = _briefingShown();
        SetLabels(_notesLabels, (briefing ? _briefingTitle : TasksTitle()) + _arrow);
        if (briefing)
        {
            if (_notes._selectedVersion != null && !_notes._selectedVersion.activeSelf) _notes._selectedVersion.SetActive(true);
            if (_notes._normalVersion != null && _notes._normalVersion.activeSelf) _notes._normalVersion.SetActive(false);
        }
        else if (_paintedBriefing)
        {
            _notes.UpdateVersion();
        }
        _paintedBriefing = briefing;
    }

    private static void SetLabels(List<TMP_Text> labels, string text)
    {
        foreach (var t in labels) if (t != null && t.text != text) t.text = text;
    }

    private void OnEnable() => ApplySorting();

    private void OnDisable()
    {
        if (_notes == null) return;
        UiKit.Guard("tab menu close", CloseNow);
    }

    // 메뉴가 사라지면 임무 탭을 원래 글자와 게임 동작으로 되돌린다.
    private void OnDestroy()
    {
        TabClickPatch.Unregister(_notes);
        foreach (var item in _items) TabClickPatch.Unregister(item);
        if (_notes == null) return;
        UiKit.Guard("tab menu restore", () =>
        {
            RaiseNotes(false);
            SetLabels(_notesLabels, TasksTitle());
            _notes.UpdateVersion();
        });
    }
}
