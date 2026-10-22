using System;
using System.Collections.Generic;
using System.Globalization;
using Briefing.Core.Screen;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Briefing.Ui;

// 왼쪽 열 목록. 묶음 머리줄, 퀘스트 줄, 펼친 줄의 펼침 블록으로 이뤄진다.
// 줄이 수십 개뿐이라 그릴 때마다 전부 지우고 템플릿을 다시 복제한다. 머리줄을 누르면 Collapsed 를 바꾸고 onToggle 로 다시 그린다.
// 퀘스트 줄 클릭의 펼침과 지도 초점은 부른 쪽이 처리한다. 맵 묶음 줄은 마우스 올림·내림을 onHot 으로 알린다.
internal sealed class QuestListView
{
    // 게임 글꼴의 한글 폴백(KS X 1001)에 있는 기호다.
    private const string CaretOpen = "▼";
    private const string CaretClosed = "▶";

    private readonly GameObject _list;
    private readonly RectTransform _content;
    private readonly ScrollRect _scroll;
    private readonly GameObject _sectionTemplate;
    private readonly GameObject _questTemplate;
    private readonly GameObject _emptyTemplate;
    private readonly TraderFaces _faces;
    private readonly QuestDetailView _detail;
    private readonly Action _onToggle;
    private readonly Action<string, string> _onQuestClick;
    private readonly Action<string> _onHot;
    private readonly Color _rowColor;
    private readonly List<GameObject> _rows = new List<GameObject>();
    private readonly Dictionary<string, (Image Bg, Color Base, bool Selected, Transform Row)> _hereRows =
        new Dictionary<string, (Image, Color, bool, Transform)>();                                       // questId 별 맵 묶음 줄
    private readonly Dictionary<(string, string), RectTransform> _rowsByKey = new Dictionary<(string, string), RectTransform>(); // (묶음, questId)

    private static readonly Color RowHot = UiKit.Hex("#2b3030");

    internal QuestListView(Transform screenRoot, TraderFaces faces, QuestDetailView detail, Action onToggle,
        Action<string, string> onQuestClick, Action<string> onHot)
    {
        _faces = faces;
        _detail = detail;
        _onToggle = onToggle;
        _onQuestClick = onQuestClick;
        _onHot = onHot;
        _list = UiKit.Child(screenRoot, UiPaths.List).gameObject;
        _scroll = UiKit.Get<ScrollRect>(screenRoot, UiPaths.List);
        _content = (RectTransform)UiKit.Child(screenRoot, UiPaths.ListContent);

        _sectionTemplate = UiKit.Child(screenRoot, UiPaths.SectionTemplate).gameObject;
        foreach (var p in new[] { UiPaths.SectionCaret, UiPaths.SectionTitle, UiPaths.SectionCount }) UiKit.Get<TMP_Text>(_sectionTemplate.transform, p);
        if (_sectionTemplate.GetComponent<Button>() == null) throw new InvalidOperationException($"{UiPaths.SectionTemplate} has no Button");

        _questTemplate = UiKit.Child(screenRoot, UiPaths.QuestTemplate).gameObject;
        var q = _questTemplate.transform;
        foreach (var p in new[] { UiPaths.QuestInitial, UiPaths.QuestMarkerNumber, UiPaths.QuestName, UiPaths.QuestSummary, UiPaths.QuestValue })
            UiKit.Get<TMP_Text>(q, p);
        foreach (var p in new[] { UiPaths.QuestFace, UiPaths.QuestMarkerFill, UiPaths.QuestMarkerRing, UiPaths.QuestBar, UiPaths.QuestHandIcon,
                     UiPaths.QuestBadges, UiPaths.QuestBadgeKappa, UiPaths.QuestBadgeLightkeeper })
            UiKit.Get<Image>(q, p);
        UiKit.Child(q, UiPaths.QuestFill);
        if (_questTemplate.GetComponent<Button>() == null) throw new InvalidOperationException($"{UiPaths.QuestTemplate} has no Button");
        var rowBg = _questTemplate.GetComponent<Image>();
        if (rowBg == null) throw new InvalidOperationException($"{UiPaths.QuestTemplate} has no Image");
        _rowColor = rowBg.color;

        _emptyTemplate = UiKit.Child(screenRoot, UiPaths.EmptyTemplate).gameObject;
        if (_emptyTemplate.GetComponent<TMP_Text>() == null) throw new InvalidOperationException($"{UiPaths.EmptyTemplate} has no TMP_Text");

        _sectionTemplate.SetActive(false);
        _questTemplate.SetActive(false);
        _emptyTemplate.SetActive(false);
        UiKit.DestroySamples(_content);
    }

    internal void SetVisible(bool visible) => _list.SetActive(visible);

    internal void Render(LeftColumnModel model, bool scrollToTop)
    {
        Clear();
        Game.QuestListColors.Refresh();
        if (model.TurnIn != null) AddSection(model.TurnIn, model.Expanded);
        if (model.Here != null) AddSection(model.Here, model.Expanded);
        if (model.Anywhere != null) AddSection(model.Anywhere, model.Expanded);
        if (model.Ready != null) AddSection(model.Ready, model.Expanded);
        if (!scrollToTop) return;
        LayoutRebuilder.ForceRebuildLayoutImmediate(_content);
        _scroll.verticalNormalizedPosition = 1f;
    }

    internal void Clear()
    {
        _faces.CancelAll();
        foreach (var r in _rows)
        {
            if (r == null) continue;
            r.SetActive(false); // Destroy 는 프레임 끝이라 바로 꺼서 같은 프레임의 ScrollTo 레이아웃에서 빠지게 한다
            UnityEngine.Object.Destroy(r);
        }
        _rows.Clear();
        _hereRows.Clear();
        _rowsByKey.Clear();
    }

    // 지도 마커에 마우스를 올린 퀘스트의 줄을 밝힌다. null 이면 모두 원래대로.
    internal void SetHot(string questId)
    {
        foreach (var kv in _hereRows)
            if (kv.Value.Bg != null) kv.Value.Bg.color = kv.Key == questId && !kv.Value.Selected ? RowHot : kv.Value.Base; // 펼친 줄은 원래 바탕 그대로
        LightMarkers(questId);
    }

    // 지도에서 강조되는 퀘스트(마우스를 올린 것)와 펼친 줄의 번호를 채워 지도 마커와 같은 모양으로 맞춘다.
    private void LightMarkers(string hotQuestId)
    {
        foreach (var kv in _hereRows)
            if (kv.Value.Row != null) PaintMarker(kv.Value.Row, kv.Value.Selected || kv.Key == hotQuestId);
    }

    // 강조되면 선택 줄 색으로 채우고 테두리와 번호를 어둡게, 아니면 테두리와 번호만 밝게 그린다.
    private static void PaintMarker(Transform row, bool lit)
    {
        UiKit.Get<Image>(row, UiPaths.QuestMarkerFill).color = lit ? MarkerTone.LitFill : Color.clear;
        UiKit.Get<Image>(row, UiPaths.QuestMarkerRing).color = lit ? MarkerTone.Dark : MarkerTone.Ink;
        UiKit.Get<TMP_Text>(row, UiPaths.QuestMarkerNumber).color = lit ? MarkerTone.LitText : MarkerTone.Ink;
    }

    // 그 줄이 보일 만큼만 스크롤한다. Render 바로 뒤에 부른다.
    internal void ScrollTo(string sectionKey, string questId)
    {
        if (!_rowsByKey.TryGetValue((sectionKey, questId), out var row) || row == null) return;
        LayoutRebuilder.ForceRebuildLayoutImmediate(_content);
        var viewport = _scroll.viewport != null ? _scroll.viewport : (RectTransform)_scroll.transform;
        var corners = new Vector3[4];
        row.GetWorldCorners(corners); // 0 은 왼쪽 아래, 1 은 왼쪽 위
        var top = viewport.InverseTransformPoint(corners[1]).y;
        var bottom = viewport.InverseTransformPoint(corners[0]).y;
        var view = viewport.rect;
        var move = top > view.yMax ? -(top - view.yMax) : bottom < view.yMin ? view.yMin - bottom : 0f;
        if (move == 0f) return;
        _scroll.StopMovement();
        _content.anchoredPosition += new Vector2(0, move); // 범위를 넘으면 ScrollRect 가 끝에 맞춘다
    }

    private void AddSection(QuestSection section, QuestDetailModel expanded)
    {
        var collapsed = BriefingState.Collapsed.Contains(section.Key);
        var head = Spawn(_sectionTemplate, "Section_" + section.Key);
        UiKit.Get<TMP_Text>(head.transform, UiPaths.SectionCaret).text = collapsed ? CaretClosed : CaretOpen;
        UiKit.Get<TMP_Text>(head.transform, UiPaths.SectionTitle).text = section.Title;
        UiKit.Get<TMP_Text>(head.transform, UiPaths.SectionCount).text = section.Count.ToString(CultureInfo.InvariantCulture);
        var key = section.Key;
        head.GetComponent<Button>().onClick.AddListener(() => UiKit.Guard("section toggle", () =>
        {
            if (!BriefingState.Collapsed.Remove(key)) BriefingState.Collapsed.Add(key);
            _onToggle();
        }));
        if (collapsed) return;

        if (section.EmptyText != null) Spawn(_emptyTemplate, "Empty_" + section.Key).GetComponent<TMP_Text>().text = section.EmptyText;
        foreach (var row in section.Rows)
        {
            var selected = expanded != null && expanded.SectionKey == section.Key && expanded.QuestId == row.QuestId;
            AddQuest(section.Key, row, selected);
            if (selected)
                UiKit.Guard("render quest detail", () => _rows.Add(_detail.Create(_content, expanded)));
        }
    }

    private void AddQuest(string sectionKey, QuestRow row, bool selected)
    {
        var go = Spawn(_questTemplate, "Quest_" + row.QuestId);
        var t = go.transform;
        var questId = row.QuestId;
        go.GetComponent<Button>().onClick.AddListener(() => UiKit.Guard("quest expand", () => _onQuestClick(sectionKey, questId)));
        _rowsByKey[(sectionKey, questId)] = (RectTransform)t;
        // 상인 임무 목록과 같은 색을 쓴다. 펼친 줄은 밝은 회색에 어두운 글자, "완료 가능" 줄은 푸른 바탕.
        var bg = go.GetComponent<Image>();
        var baseColor = selected ? Over(Game.QuestListColors.SelectedBg)
            : sectionKey == LeftColumn.TurnInKey ? Over(Game.QuestListColors.TurnInBg) : _rowColor;
        bg.color = baseColor;
        if (sectionKey == LeftColumn.HereKey)
        {
            _hereRows[questId] = (bg, baseColor, selected, t);
            var relay = go.AddComponent<HoverRelay>(); // 지도 강조용. 클릭은 Button 이 받는다
            relay.Enter = () =>
            {
                _onHot(questId);
                LightMarkers(questId);
            };
            relay.Exit = () =>
            {
                _onHot(null);
                LightMarkers(null);
            };
        }
        UiKit.Get<TMP_Text>(t, UiPaths.QuestName).text = row.Name ?? "";
        // 그림을 못 구하면 손 아이콘만 꺼 둔다.
        var hand = UiKit.Get<Image>(t, UiPaths.QuestHandIcon);
        hand.gameObject.SetActive(false);
        if (row.HandInReady)
            UiKit.Guard("hand icon", () =>
            {
                var sprite = Game.GameIcons.Hand();
                if (sprite == null) return;
                hand.sprite = sprite;
                hand.color = Color.cyan;
                hand.gameObject.SetActive(true);
            });
        UiKit.Get<TMP_Text>(t, UiPaths.QuestSummary).text = row.Summary ?? "";

        UiKit.Child(t, UiPaths.QuestMarker).gameObject.SetActive(row.Marker.HasValue);
        if (row.Marker.HasValue)
        {
            UiKit.Get<TMP_Text>(t, UiPaths.QuestMarkerNumber).text = row.Marker.Value.ToString(CultureInfo.InvariantCulture);
            PaintMarker(t, selected);
        }

        var value = UiKit.Get<TMP_Text>(t, UiPaths.QuestValue);
        value.gameObject.SetActive(row.Progress != null);
        value.text = row.Progress ?? "";
        UiKit.Child(t, UiPaths.QuestBar).gameObject.SetActive(row.Fraction.HasValue);
        if (row.Fraction.HasValue)
        {
            var fill = (RectTransform)UiKit.Child(t, UiPaths.QuestFill);
            fill.anchorMin = Vector2.zero;
            fill.anchorMax = new Vector2(row.Fraction.Value, 1f);
            fill.offsetMin = fill.offsetMax = Vector2.zero;
        }

        var initial = UiKit.Get<TMP_Text>(t, UiPaths.QuestInitial);
        initial.text = row.TraderInitial;
        _faces.Assign(row.TraderId, UiKit.Get<Image>(t, UiPaths.QuestFace), initial.gameObject);
        // 얼굴 대신 첫 글자를 보일 때도 같은 자리에 붙는다.
        UiKit.Child(t, UiPaths.QuestBadgeKappa).gameObject.SetActive(row.Kappa);
        UiKit.Child(t, UiPaths.QuestBadgeLightkeeper).gameObject.SetActive(row.Lightkeeper);
        UiKit.Child(t, UiPaths.QuestBadges).gameObject.SetActive(row.Kappa || row.Lightkeeper);

        if (selected)
        {
            var dark = Game.QuestListColors.SelectedText;
            UiKit.Get<TMP_Text>(t, UiPaths.QuestName).color = dark;
            value.color = dark;
            UiKit.Get<TMP_Text>(t, UiPaths.QuestSummary).color = Color.Lerp(dark, baseColor, 0.35f); // 요약은 조금 흐리게
        }
    }

    // 게임 색표의 반투명 바탕을 줄 바탕 위에 미리 섞어서 목록 바탕이 비치지 않게 한다.
    private Color Over(Color c)
    {
        var mixed = Color.Lerp(_rowColor, c, c.a);
        mixed.a = Mathf.Max(_rowColor.a, c.a);
        return mixed;
    }

    private GameObject Spawn(GameObject template, string name)
    {
        var go = UnityEngine.Object.Instantiate(template, _content, false);
        go.name = name;
        go.SetActive(true);
        _rows.Add(go);
        return go;
    }
}
