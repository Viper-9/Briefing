using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Briefing.Core.Screen;
using Briefing.Game;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Briefing.Ui;

// 오른쪽 열. 탭 줄과 고른 탭의 본문(머리줄, 빈 문구, 아이템 격자)을 그린다. 칸이 수십 개뿐이라 그릴 때마다 지우고 다시 복제한다.
// 탭을 누르면 RightTab 을 바꾸고 onChanged 로 오른쪽 열만 다시 그린다. 칸 하나가 실패해도 나머지는 그린다.
internal sealed class RightColumnView
{
    private const float SidePadding = 24f;      // 프리팹 Grid 의 좌우 여백 12 + 12
    private const float ScrollbarReserve = 10f; // 스크롤바 8 + 간격 2. 스크롤바가 생겨도 칸 수가 바뀌지 않게 늘 뺀다

    private static readonly Color TabTextOn = UiKit.Hex("#141615");
    private static readonly Color TabCountOn = UiKit.Hex("#3c403d");
    private static readonly Color CellShortBg = new Color(0.290f, 0.145f, 0.133f, 0.9f); // #4a2522
    private static readonly Color CellOkBg = new Color(0.173f, 0.239f, 0.169f, 0.9f);    // #2c3d2b
    internal static readonly Color ShortRed = UiKit.Hex("#ff8a80");
    internal static readonly Color HaveGreen = UiKit.Hex("#9ff59a");
    private static readonly Color Gold = UiKit.Hex("#c9a64a"); // 프리팹 생성 스크립트의 Gold 와 같다
    private static readonly Color StashCyan = UiKit.Hex("#7fd4f0");
    private static readonly Color WarnOrange = UiKit.Hex("#f2b05e");
    private static readonly Color PlainText = UiKit.Hex("#c9cbc6");
    private static readonly Color SlotFilledBg = new Color(0.227f, 0.204f, 0.133f, 0.8f); // #3a3422
    private static readonly Color SlotEmptyBg = UiKit.Hex("#151918");
    private const float MaxAltsHeight = 150f; // 약 8줄. 프리팹 Alts 와 같은 값

    private readonly List<(RightTabKey Key, GameObject Selected, TMP_Text Label, TMP_Text Count)> _tabs =
        new List<(RightTabKey, GameObject, TMP_Text, TMP_Text)>();
    private readonly Color _tabText;
    private readonly Color _tabCount;
    private readonly GameObject _filter;
    private readonly TMP_Text _filterText;
    private readonly TMP_Text _filterClear;
    private readonly RectTransform _scrollArea;
    private readonly ScrollRect _scroll;
    private readonly RectTransform _content;
    private readonly GameObject _sectionTemplate;
    private readonly GameObject _emptyTemplate;
    private readonly GameObject _gridTemplate;
    private readonly GameObject _loadoutTemplate;
    private readonly GameObject _gearTemplate;
    private readonly Sprite _markOk;
    private readonly Sprite _markStash;
    private readonly Sprite _markWarn;
    private readonly Action _onChanged;
    private readonly List<GameObject> _spawned = new List<GameObject>();
    private readonly List<Action> _unsubscribe = new List<Action>();
    private readonly List<ItemGridView> _grids = new List<ItemGridView>();
    private readonly RectTransform _screen;
    private readonly RectTransform _tip;
    private readonly TMP_Text _tipTitle;
    private readonly TMP_Text _tipKind;
    private readonly Color _tipDim; // 프리팹 Alt 줄의 흐린 글자색
    private readonly TMP_Text _tipAlt;
    private readonly TMP_Text _tipCount;
    private readonly TMP_Text _tipQuests;
    private RightTabKey? _drawnTab;

    private const float TipGap = 6f;      // 칸 왼쪽 끝과 말풍선 오른쪽 끝 사이
    private const float TipMinWidth = 140f;
    private const float TipMaxWidth = 320f;
    private const float TipPadding = 20f;  // 프리팹 RightTooltip 좌우 여백 10 + 10

    // 경로가 없으면 예외. 부르는 쪽이 Guard 로 감싸 오른쪽 열만 빠지게 한다.
    internal RightColumnView(Transform screenRoot, Action onChanged)
    {
        _onChanged = onChanged;
        foreach (var (key, path) in new[] { (RightTabKey.Bring, UiPaths.TabBring), (RightTabKey.Find, UiPaths.TabFind), (RightTabKey.Gear, UiPaths.TabGear) })
        {
            var tab = UiKit.Child(screenRoot, path);
            var button = tab.GetComponent<Button>();
            if (button == null) throw new InvalidOperationException($"{path} has no Button");
            var k = key;
            button.onClick.AddListener(() => UiKit.Guard("right tab", () =>
            {
                BriefingState.RightTab = k;
                _onChanged();
            }));
            TabFeel.Attach(tab.gameObject);
            _tabs.Add((key, UiKit.Child(tab, UiPaths.TabSelected).gameObject, UiKit.Get<TMP_Text>(tab, UiPaths.TabLabel), UiKit.Get<TMP_Text>(tab, UiPaths.TabCount)));
        }
        // 프리팹은 첫 탭이 골라진 모습이라 평소 글자색은 둘째 탭에서 가져온다.
        _tabText = _tabs[1].Label.color;
        _tabCount = _tabs[1].Count.color;
        // 필터 띠의 × 는 필터만 끄고 펼침과 지도 강조는 그대로 둔다.
        _filter = UiKit.Child(screenRoot, UiPaths.RightFilter).gameObject;
        _filterText = UiKit.Get<TMP_Text>(_filter.transform, UiPaths.FilterText);
        _filterClear = UiKit.Get<TMP_Text>(_filter.transform, UiPaths.FilterClearLabel);
        UiKit.Get<Button>(_filter.transform, UiPaths.FilterClear).onClick.AddListener(() => UiKit.Guard("right filter off", () =>
        {
            BriefingState.RightFilterOff = true;
            _onChanged();
        }));
        _filter.SetActive(false);
        _scrollArea = (RectTransform)UiKit.Child(screenRoot, UiPaths.RightScroll);
        _scroll = UiKit.Get<ScrollRect>(screenRoot, UiPaths.RightScroll);
        // 휠로 스크롤하면 말풍선이 옛 칸 자리에 남으므로 숨긴다.
        _scroll.onValueChanged.AddListener(_ => UiKit.Guard("right tooltip scroll", HideTip));
        _content = (RectTransform)UiKit.Child(screenRoot, UiPaths.RightContent);

        _sectionTemplate = UiKit.Child(screenRoot, UiPaths.RightSectionTemplate).gameObject;
        UiKit.Get<TMP_Text>(_sectionTemplate.transform, UiPaths.RightSectionTitle);
        UiKit.Get<TMP_Text>(_sectionTemplate.transform, UiPaths.RightSectionNote);
        UiKit.Child(_sectionTemplate.transform, UiPaths.RightSectionLegend);
        foreach (var item in new[] { UiPaths.LegendOk, UiPaths.LegendStash, UiPaths.LegendWarn })
        {
            var legendItem = UiKit.Child(_sectionTemplate.transform, item);
            UiKit.Get<Image>(legendItem, UiPaths.LegendMark);
            UiKit.Get<TMP_Text>(legendItem, UiPaths.LegendText);
        }
        _emptyTemplate = UiKit.Child(screenRoot, UiPaths.RightEmptyTemplate).gameObject;
        if (_emptyTemplate.GetComponent<TMP_Text>() == null) throw new InvalidOperationException($"{UiPaths.RightEmptyTemplate} has no TMP_Text");
        _gridTemplate = UiKit.Child(screenRoot, UiPaths.RightGridTemplate).gameObject;
        UiKit.Get<LayoutElement>(_gridTemplate.transform, UiPaths.GridBoard);
        var cell = UiKit.Child(_gridTemplate.transform, UiPaths.GridCellTemplate);
        UiKit.Get<Image>(cell, UiPaths.CellIcon);
        UiKit.Get<Image>(cell, UiPaths.CellFir);
        foreach (var p in new[] { UiPaths.CellShort, UiPaths.CellCount, UiPaths.CellAlt }) UiKit.Get<TMP_Text>(cell, p);
        UiKit.Get<Image>(_gridTemplate.transform, UiPaths.GridBlankTemplate);
        _loadoutTemplate = UiKit.Child(screenRoot, UiPaths.RightLoadoutTemplate).gameObject;
        var slot = UiKit.Child(_loadoutTemplate.transform, UiPaths.LoadoutSlotTemplate);
        UiKit.Get<Image>(slot, UiPaths.SlotIcon);
        UiKit.Get<TMP_Text>(slot, UiPaths.SlotLabel);
        _gearTemplate = UiKit.Child(screenRoot, UiPaths.RightGearTemplate).gameObject;
        var g = _gearTemplate.transform;
        UiKit.Get<TMP_Text>(g, UiPaths.GearTitle);
        UiKit.Child(g, UiPaths.GearLine);
        var rule = UiKit.Child(g, UiPaths.RuleRowTemplate);
        foreach (var p in new[] { UiPaths.RuleLabel, UiPaths.RuleLead, UiPaths.RuleTag, UiPaths.RuleMoreLabel }) UiKit.Get<TMP_Text>(rule, p);
        UiKit.Get<Image>(rule, UiPaths.RuleMark);
        UiKit.Get<Button>(rule, UiPaths.RuleMore);
        UiKit.Get<LayoutElement>(rule, UiPaths.RuleAlts);
        UiKit.Child(rule, UiPaths.RuleAltsContent);
        var altLine = UiKit.Child(rule, UiPaths.AltTemplate);
        UiKit.Get<Image>(altLine, UiPaths.AltMark);
        UiKit.Get<TMP_Text>(altLine, UiPaths.AltText);
        var special = UiKit.Child(g, UiPaths.SpecialRowTemplate);
        UiKit.Get<TMP_Text>(special, UiPaths.SpecialLabel);
        UiKit.Get<TMP_Text>(special, UiPaths.SpecialText);
        _markOk = UiKit.Get<Image>(screenRoot, UiPaths.IconMarkOk).sprite;
        _markStash = UiKit.Get<Image>(screenRoot, UiPaths.IconMarkStash).sprite;
        _markWarn = UiKit.Get<Image>(screenRoot, UiPaths.IconMarkWarn).sprite;
        _screen = (RectTransform)screenRoot;
        _tip = (RectTransform)UiKit.Child(screenRoot, UiPaths.RightTooltip);
        _tipTitle = UiKit.Get<TMP_Text>(_tip, UiPaths.RightTipTitle);
        _tipKind = UiKit.Get<TMP_Text>(_tip, UiPaths.RightTipKind);
        _tipAlt = UiKit.Get<TMP_Text>(_tip, UiPaths.RightTipAlt);
        _tipDim = _tipAlt.color;
        _tipCount = UiKit.Get<TMP_Text>(_tip, UiPaths.RightTipCount);
        _tipQuests = UiKit.Get<TMP_Text>(_tip, UiPaths.RightTipQuests);
        _tip.gameObject.SetActive(false);
        // 폭이 바뀌면 다시 그리지 않고 격자만 다시 놓는다.
        _scrollArea.gameObject.AddComponent<ResizeRelay>().Resized = Relayout;

        _sectionTemplate.SetActive(false);
        _emptyTemplate.SetActive(false);
        _gridTemplate.SetActive(false);
        _loadoutTemplate.SetActive(false);
        _gearTemplate.SetActive(false);
        UiKit.DestroySamples(_content);
    }

    // loadout 은 장비 탭에서만 쓰고 못 읽었으면 null.
    internal void Render(RightColumnModel model, IReadOnlyList<LoadoutSlot> loadout = null)
    {
        UiKit.Guard("right tabs", () => RenderTabs(model.Tabs));
        UiKit.Guard("right filter", () =>
        {
            _filter.SetActive(model.FilterText != null);
            _filterText.text = model.FilterText ?? "";
            _filterClear.text = Texts.ShowAll;
        });
        Clear();
        GameIcons.BeginRender();
        var tab = BriefingState.RightTab;
        switch (tab)
        {
            case RightTabKey.Bring:
                RenderItems(model.Bring, model.BringEmpty);
                break;
            case RightTabKey.Find:
                RenderItems(model.Find, model.FindEmpty);
                break;
            case RightTabKey.Gear:
                RenderGear(model.Gear, loadout);
                break;
        }
        if (_drawnTab != tab)
        {
            // 탭을 바꾸면 맨 위부터 보이고, 같은 탭을 다시 그리면 스크롤 위치를 그대로 둔다.
            LayoutRebuilder.ForceRebuildLayoutImmediate(_content);
            _scroll.verticalNormalizedPosition = 1f;
            _drawnTab = tab;
        }
    }

    // 응답이 없을 때는 counts 로 탭 숫자도 지운다.
    internal void Clear(bool counts = false)
    {
        HideTip(); // 지운 칸에는 마우스 내림 알림이 오지 않는다
        foreach (var u in _unsubscribe) UiKit.Guard("item icon unsubscribe", u);
        _unsubscribe.Clear();
        foreach (var go in _spawned)
        {
            if (go == null) continue;
            go.SetActive(false); // Destroy 는 프레임 끝이라 같은 프레임 레이아웃에서 빠지게 바로 끈다
            UnityEngine.Object.Destroy(go);
        }
        _spawned.Clear();
        _grids.Clear();
        if (!counts) return;
        foreach (var t in _tabs) t.Count.text = "";
        _filter.SetActive(false);
    }

    private void RenderTabs(IReadOnlyList<RightTab> tabs)
    {
        foreach (var t in _tabs)
        {
            RightTab model = null;
            foreach (var m in tabs) if (m.Key == t.Key) model = m;
            var on = t.Key == BriefingState.RightTab;
            t.Selected.SetActive(on);
            t.Label.text = model?.Label ?? "";
            t.Label.color = on ? TabTextOn : _tabText;
            t.Count.text = model == null ? "" : model.Count.ToString(CultureInfo.InvariantCulture);
            t.Count.color = on ? TabCountOn : _tabCount;
        }
    }

    // empty 가 있으면 그 문구만, 아니면 묶음마다 머리줄과 격자(또는 빈 문구).
    private void RenderItems(IReadOnlyList<ItemSectionModel> sections, string empty)
    {
        if (empty != null)
        {
            UiKit.Guard("right empty", () => AddEmpty(empty));
            return;
        }
        foreach (var section in sections)
            UiKit.Guard("right section", () =>
            {
                AddSection(section.Title, section.Note);
                if (section.Cells.Count == 0)
                {
                    if (section.EmptyText != null) AddEmpty(section.EmptyText);
                    return;
                }
                AddGrid(section.Cells);
            });
    }

    internal GameObject AddSection(string title, string note)
    {
        var go = Spawn(_sectionTemplate, "Section");
        UiKit.Get<TMP_Text>(go.transform, UiPaths.RightSectionTitle).text = title ?? "";
        var n = UiKit.Get<TMP_Text>(go.transform, UiPaths.RightSectionNote);
        n.gameObject.SetActive(note != null);
        n.text = note ?? "";
        return go;
    }

    internal void AddEmpty(string text) => Spawn(_emptyTemplate, "Empty").GetComponent<TMP_Text>().text = text;

    private void AddGrid(IReadOnlyList<ItemCellModel> cells)
    {
        var go = Spawn(_gridTemplate, "Grid");
        var board = (RectTransform)UiKit.Child(go.transform, UiPaths.GridBoard);
        var cellTemplate = UiKit.Child(go.transform, UiPaths.GridCellTemplate).gameObject;
        var blankTemplate = UiKit.Child(go.transform, UiPaths.GridBlankTemplate).gameObject;
        cellTemplate.SetActive(false);
        blankTemplate.SetActive(false);
        var grid = new ItemGridView(board, blankTemplate);
        foreach (var cell in cells)
            UiKit.Guard("item cell", () =>
            {
                var item = ItemIcons.Preset(cell.Tpl);
                grid.Add(CreateCell(cellTemplate, board, cell, item), ItemIcons.CellSize(item));
            });
        grid.Layout(Columns());
        _grids.Add(grid);
    }

    // 실패하면 반쯤 만든 칸이 남지 않게 지우고 다시 던진다.
    private RectTransform CreateCell(GameObject template, RectTransform board, ItemCellModel model, EFT.InventoryLogic.Item item)
    {
        var go = UnityEngine.Object.Instantiate(template, board, false);
        go.name = "Cell_" + model.Tpl;
        try
        {
            var t = go.transform;
            go.GetComponent<Image>().color = model.Short ? CellShortBg : CellOkBg;
            UiKit.Get<TMP_Text>(t, UiPaths.CellShort).text = ItemIcons.ShortName(model.Tpl, model.Name);
            var count = UiKit.Get<TMP_Text>(t, UiPaths.CellCount);
            count.text = model.Count;
            count.color = model.Short ? ShortRed : HaveGreen;
            var questItem = false;
            UiKit.Guard("item cell mark", () =>
            {
                questItem = item != null && item.QuestItem; // 게임 값 읽기가 실패해도 칸은 남게 같이 감싼다
                SetMark(UiKit.Child(t, UiPaths.CellFir), model, questItem);
            });
            var alt = UiKit.Get<TMP_Text>(t, UiPaths.CellAlt);
            alt.gameObject.SetActive(model.Alternatives > 0);
            alt.text = "+" + model.Alternatives.ToString(CultureInfo.InvariantCulture);
            go.SetActive(true);
            var unsubscribe = ItemIcons.Fill(UiKit.Get<Image>(t, UiPaths.CellIcon), item);
            if (unsubscribe != null) _unsubscribe.Add(unsubscribe);
            var relay = go.AddComponent<HoverRelay>(); // 말풍선만 띄우고 클릭 동작은 없다
            var inFind = BriefingState.RightTab == RightTabKey.Find;
            var resolved = item != null;
            relay.Enter = () => ShowTip((RectTransform)go.transform, model, resolved, questItem, inFind);
            relay.Exit = HideTip;
            return (RectTransform)t;
        }
        catch
        {
            go.SetActive(false);
            UnityEngine.Object.Destroy(go);
            throw;
        }
    }

    // 오른쪽 아래 표시. 대안이 있으면 그 자리를 "+N" 이 쓰므로 없다. 퀘스트 아이템이면 게임의 퀘스트 아이템 아이콘, 아니고 FIR 이면 FIR 동그라미.
    // 게임 그림을 못 찾으면 퀘스트 아이템은 표시 없이, FIR 은 프리팹의 금색 고리를 쓴다.
    private static void SetMark(Transform fir, ItemCellModel model, bool questItem)
    {
        var show = false;
        if (model.Alternatives == 0)
        {
            var image = fir.GetComponent<Image>();
            var sprite = questItem ? GameIcons.QuestItem() : model.Fir ? GameIcons.FoundInRaid() : null;
            if (sprite != null)
            {
                image.sprite = sprite;
                image.color = Color.white;
                image.preserveAspect = true;
                show = true;
            }
            else show = model.Fir && !questItem;
        }
        fir.gameObject.SetActive(show);
    }

    // 현재 장비 줄, 무기·장비 조건, 특수 조건, 빈 문구 순서.
    private void RenderGear(GearTabModel gear, IReadOnlyList<LoadoutSlot> loadout)
    {
        if (gear == null) return;
        UiKit.Guard("right loadout", () =>
        {
            AddSection(Texts.GearLoadoutTitle, null);
            if (gear.LoadoutError != null || loadout == null) AddEmpty(gear.LoadoutError ?? Texts.LoadoutUnreadable);
            else AddLoadout(loadout);
        });
        if (gear.Gear.Count > 0)
        {
            var header = (GameObject)null;
            UiKit.Guard("right gear title", () => header = AddSection(Texts.GearRulesTitle, null));
            if (header != null) UiKit.Guard("right gear legend", () => FillLegend(header, gear.Legend));
        }
        foreach (var block in gear.Gear) UiKit.Guard("right gear block", () => AddBlock(block));
        if (gear.Special.Count > 0) UiKit.Guard("right special title", () => AddSection(Texts.GearSpecialTitle, null));
        foreach (var block in gear.Special) UiKit.Guard("right special block", () => AddBlock(block));
        if (gear.Empty != null) UiKit.Guard("right gear empty", () => AddEmpty(gear.Empty));
    }

    // marks 에 없는 항목(보관함을 못 읽었을 때 ◐)은 숨기고, 비어 있으면 범례를 통째로 끈다.
    private void FillLegend(GameObject header, IReadOnlyList<GearMark> marks)
    {
        var legend = UiKit.Child(header.transform, UiPaths.RightSectionLegend).gameObject;
        legend.SetActive(marks.Count > 0);
        foreach (var (mark, path) in new[] { (GearMark.Equipped, UiPaths.LegendOk), (GearMark.Stash, UiPaths.LegendStash), (GearMark.Missing, UiPaths.LegendWarn) })
        {
            var item = UiKit.Child(header.transform, path);
            item.gameObject.SetActive(marks.Contains(mark));
            var legendMark = UiKit.Get<Image>(item, UiPaths.LegendMark);
            SetMark(legendMark, mark);
            var legendText = UiKit.Get<TMP_Text>(item, UiPaths.LegendText);
            legendText.text = Texts.LegendMark(mark);
            FitMarkToLine(legendMark, legendText);
        }
    }

    // 슬롯마다 실제 장착 아이템 아이콘을 쓰고 빈 슬롯은 어둡게.
    private void AddLoadout(IReadOnlyList<LoadoutSlot> slots)
    {
        var go = Spawn(_loadoutTemplate, "Loadout");
        var template = UiKit.Child(go.transform, UiPaths.LoadoutSlotTemplate).gameObject;
        template.SetActive(false);
        foreach (var slot in slots)
            UiKit.Guard("loadout slot", () =>
            {
                var s = UnityEngine.Object.Instantiate(template, go.transform, false);
                s.name = "Slot";
                s.GetComponent<Image>().color = slot.Item != null ? SlotFilledBg : SlotEmptyBg;
                UiKit.Get<TMP_Text>(s.transform, UiPaths.SlotLabel).text = slot.Label;
                s.SetActive(true);
                var unsubscribe = ItemIcons.Fill(UiKit.Get<Image>(s.transform, UiPaths.SlotIcon), slot.Item);
                if (unsubscribe != null) _unsubscribe.Add(unsubscribe);
            });
    }

    // 실패하면 반쯤 그린 덩어리가 남지 않게 지우고 다시 던진다.
    private void AddBlock(GearBlockModel block)
    {
        var go = Spawn(_gearTemplate, "Gear_" + block.QuestId);
        try
        {
            var t = go.transform;
            UiKit.Get<TMP_Text>(t, UiPaths.GearTitle).text = block.Title ?? "";
            var ruleTemplate = UiKit.Child(t, UiPaths.RuleRowTemplate).gameObject;
            var specialTemplate = UiKit.Child(t, UiPaths.SpecialRowTemplate).gameObject;
            ruleTemplate.SetActive(false);
            specialTemplate.SetActive(false);
            foreach (var rule in block.Rules) UiKit.Guard("gear rule", () => AddRule(ruleTemplate, rule));
            foreach (var line in block.Specials) UiKit.Guard("special line", () => AddSpecial(specialTemplate, line));
            UiKit.Child(t, UiPaths.GearLine).SetAsLastSibling(); // 구분선을 맨 아래로
        }
        catch
        {
            _spawned.Remove(go);
            go.SetActive(false);
            UnityEngine.Object.Destroy(go);
            throw;
        }
    }

    // 라벨, 판정 표시, 대표 이름, 태그, "외 N종". 열어 둔 줄은 아래 상자에 전체 목록을 보인다.
    private void AddRule(GameObject template, GearLineModel rule)
    {
        var go = UnityEngine.Object.Instantiate(template, template.transform.parent, false);
        go.name = "Rule_" + rule.Key;
        var t = go.transform;
        var label = UiKit.Get<TMP_Text>(t, UiPaths.RuleLabel);
        label.text = rule.Label;
        if (rule.Warn) label.color = WarnOrange;
        SetMark(UiKit.Get<Image>(t, UiPaths.RuleMark), rule.Mark);
        var lead = UiKit.Get<TMP_Text>(t, UiPaths.RuleLead);
        lead.text = rule.Lead ?? "";
        lead.color = rule.Mark == GearMark.Equipped ? HaveGreen : rule.Mark == GearMark.Stash ? StashCyan : PlainText; // 맞는 이름에만 색을 넣는다
        var tag = UiKit.Get<TMP_Text>(t, UiPaths.RuleTag);
        tag.gameObject.SetActive(rule.Tag != null);
        tag.text = rule.Tag ?? "";
        var open = rule.More != null && BriefingState.RightOpenRows.Contains(rule.Key);
        var more = UiKit.Get<Button>(t, UiPaths.RuleMore);
        more.gameObject.SetActive(rule.More != null);
        UiKit.Get<TMP_Text>(t, UiPaths.RuleMoreLabel).text = open ? Texts.ShowLess + " ▲" : rule.More + " ▼";
        var key = rule.Key;
        more.onClick.AddListener(() => UiKit.Guard("gear more", () =>
        {
            if (!BriefingState.RightOpenRows.Remove(key)) BriefingState.RightOpenRows.Add(key);
            _onChanged(); // 같은 탭이라 스크롤 위치는 그대로다
        }));
        var alts = UiKit.Child(t, UiPaths.RuleAlts).gameObject;
        alts.SetActive(open);
        go.SetActive(true);
        UiKit.Guard("gear rule mark fit", () => FitMarkToLine(UiKit.Get<Image>(t, UiPaths.RuleMark), lead));
        if (open) FillAlternatives(t, rule.Alternatives);
    }

    // 상자 높이는 내용에 맞추되 약 8줄까지이고 넘치면 상자 안에서 스크롤한다.
    private void FillAlternatives(Transform rule, IReadOnlyList<GearAlt> alternatives)
    {
        var content = (RectTransform)UiKit.Child(rule, UiPaths.RuleAltsContent);
        var template = UiKit.Child(rule, UiPaths.AltTemplate).gameObject;
        template.SetActive(false);
        foreach (var alt in alternatives)
        {
            var a = UnityEngine.Object.Instantiate(template, content, false);
            a.name = "Alt";
            SetMark(UiKit.Get<Image>(a.transform, UiPaths.AltMark), alt.Mark);
            var text = UiKit.Get<TMP_Text>(a.transform, UiPaths.AltText);
            text.text = alt.Name ?? "";
            text.color = MarkColor(alt.Mark); // Unknown 도 칠해야 템플릿의 초록이 남지 않는다
            a.SetActive(true);
            UiKit.Guard("gear alt mark fit", () => FitMarkToLine(UiKit.Get<Image>(a.transform, UiPaths.AltMark), text));
        }
        LayoutRebuilder.ForceRebuildLayoutImmediate(content);
        var box = UiKit.Get<LayoutElement>(rule, UiPaths.RuleAlts);
        box.preferredHeight = box.minHeight = Mathf.Min(MaxAltsHeight, LayoutUtility.GetPreferredHeight(content));
    }

    private void AddSpecial(GameObject template, SpecialLineModel line)
    {
        var go = UnityEngine.Object.Instantiate(template, template.transform.parent, false);
        go.name = "Special";
        UiKit.Get<TMP_Text>(go.transform, UiPaths.SpecialLabel).text = line.Label ?? "";
        UiKit.Get<TMP_Text>(go.transform, UiPaths.SpecialText).text = line.Text ?? "";
        go.SetActive(true);
    }

    // 한글 폴백 글꼴에 ✓·⚠ 가 없어 번들의 아이콘 그림을 쓴다. Unknown 이면 숨긴다.
    private void SetMark(Image image, GearMark mark)
    {
        image.gameObject.SetActive(mark != GearMark.Unknown);
        if (mark == GearMark.Unknown) return;
        image.sprite = mark == GearMark.Equipped ? _markOk : mark == GearMark.Stash ? _markStash : _markWarn;
        image.color = MarkColor(mark);
    }

    // 판정 표시 높이를 옆 글의 한 줄 높이에 맞춘다. 한글 폴백은 줄이 높고 Bender 라틴은 낮아서 고정 높이로는 ◐ 가 어긋나 보였다.
    // 글을 넣고 켠 뒤에 불러야 TMP 가 실제 글꼴로 잰다. 못 재면 프리팹 높이를 그대로 둔다.
    private static void FitMarkToLine(Image mark, TMP_Text text)
    {
        var layout = mark.GetComponent<LayoutElement>();
        if (layout == null || text == null) return;
        var lineHeight = text.GetPreferredValues(text.text, float.PositiveInfinity, 0f).y;
        if (!(lineHeight > 0f)) return;
        layout.minHeight = layout.preferredHeight = lineHeight;
    }

    private static Color MarkColor(GearMark mark) =>
        mark == GearMark.Equipped ? HaveGreen : mark == GearMark.Stash ? StashCyan : mark == GearMark.Missing ? WarnOrange : PlainText;

    // 칸 수가 같으면 ItemGridView 가 건너뛴다. 칸이 움직였을 수 있으니 말풍선은 닫는다.
    private void Relayout()
    {
        if (_grids.Count == 0) return;
        HideTip();
        var columns = Columns();
        foreach (var grid in _grids) UiKit.Guard("item grid relayout", () => grid.Layout(columns));
    }

    // 폭은 가장 긴 줄에 맞추되 140~320 이고 넘으면 줄을 바꾼다. 칸 왼쪽에 띄우고 화면 밖으로 나가면 안으로 민다.
    // 화면 루트의 마지막 자식이라 스크롤 마스크에 잘리지 않는다.
    private void ShowTip(RectTransform cell, ItemCellModel model, bool resolved, bool questItem, bool inFind)
    {
        _tipTitle.text = model.Name ?? "";
        // 퀘스트 전용 아이템은 금색으로, 수집 탭의 일반 아이템은 흐리게 종류 줄을 넣는다.
        if (resolved && questItem) { SetTipLine(_tipKind, Texts.QuestItemKind); _tipKind.color = Gold; }
        else if (resolved && inFind) { SetTipLine(_tipKind, Texts.LootItemKind); _tipKind.color = _tipDim; }
        else SetTipLine(_tipKind, null);
        SetTipLine(_tipAlt, model.TipAlternatives);
        SetTipLine(_tipCount, model.TipCount);
        _tipCount.color = model.Short ? ShortRed : HaveGreen;
        SetTipLine(_tipQuests, model.TipQuests);
        var width = TipMinWidth;
        foreach (var t in new[] { _tipTitle, _tipKind, _tipAlt, _tipCount, _tipQuests })
            if (t.gameObject.activeSelf) width = Mathf.Max(width, t.GetPreferredValues(t.text, 9999f, 9999f).x + TipPadding);
        _tip.sizeDelta = new Vector2(Mathf.Min(width, TipMaxWidth), _tip.sizeDelta.y);
        _tip.gameObject.SetActive(true);
        _tip.SetAsLastSibling();
        LayoutRebuilder.ForceRebuildLayoutImmediate(_tip);

        var corners = new Vector3[4];
        cell.GetWorldCorners(corners); // 1 은 왼쪽 위
        var at = (Vector2)_screen.InverseTransformPoint(corners[1]);
        var size = _tip.rect.size;
        var area = _screen.rect;
        var x = Mathf.Clamp(at.x - TipGap, area.xMin + size.x, Mathf.Max(area.xMin + size.x, area.xMax));
        var y = Mathf.Clamp(at.y, area.yMin + size.y, Mathf.Max(area.yMin + size.y, area.yMax));
        // 앵커가 루트 가운데라 루트 로컬 좌표가 곧 anchoredPosition 이다.
        _tip.anchoredPosition = new Vector2(x - area.center.x, y - area.center.y);
    }

    private static void SetTipLine(TMP_Text text, string value)
    {
        text.gameObject.SetActive(!string.IsNullOrEmpty(value));
        text.text = value ?? "";
    }

    internal void HideTip()
    {
        if (_tip != null) _tip.gameObject.SetActive(false);
    }

    // 레이아웃 전이라 폭이 0 이면 캔버스를 한 번 갱신해 본다.
    private int Columns()
    {
        var width = _scrollArea.rect.width;
        if (width <= 0)
        {
            Canvas.ForceUpdateCanvases();
            width = _scrollArea.rect.width;
        }
        return ItemGrid.Columns(width - SidePadding - ScrollbarReserve);
    }

    private GameObject Spawn(GameObject template, string name)
    {
        var go = UnityEngine.Object.Instantiate(template, _content, false);
        go.name = name;
        go.SetActive(true);
        _spawned.Add(go);
        return go;
    }
}

// 칸을 넣은 순서대로 들고 있다가 열 수가 정해지면 ItemGrid.Pack 으로 자리를 정한다.
// 쓰인 줄 안의 빈 자리는 Blank 로 채워 게임 격자처럼 보이게 한다.
internal sealed class ItemGridView
{
    private readonly RectTransform _board;
    private readonly LayoutElement _size;
    private readonly GameObject _blankTemplate;
    private readonly List<(RectTransform Cell, (int W, int H) Size)> _cells = new List<(RectTransform, (int, int))>();
    private readonly List<GameObject> _blanks = new List<GameObject>();
    private int _columns = -1;

    internal ItemGridView(RectTransform board, GameObject blankTemplate)
    {
        _board = board;
        _size = board.GetComponent<LayoutElement>();
        _blankTemplate = blankTemplate;
    }

    internal void Add(RectTransform cell, (int W, int H) size) => _cells.Add((cell, size));

    internal void Layout(int columns)
    {
        if (columns == _columns) return;
        _columns = columns;
        var sizes = new List<(int, int)>();
        foreach (var c in _cells) sizes.Add(c.Size);
        var layout = ItemGrid.Pack(sizes, columns);
        foreach (var placed in layout.Cells) Place(_cells[placed.Index].Cell, placed.X, placed.Y, placed.W, placed.H);
        foreach (var b in _blanks) if (b != null) UnityEngine.Object.Destroy(b);
        _blanks.Clear();
        foreach (var (x, y) in layout.Free)
        {
            var blank = UnityEngine.Object.Instantiate(_blankTemplate, _board, false);
            blank.name = "Blank";
            Place((RectTransform)blank.transform, x, y, 1, 1);
            blank.transform.SetAsFirstSibling(); // 칸 뒤에 그린다
            blank.SetActive(true);
            _blanks.Add(blank);
        }
        var w = layout.Columns * ItemGrid.CellSize + 1;
        var h = layout.Rows * ItemGrid.CellSize + 1;
        _size.preferredWidth = _size.minWidth = w;
        _size.preferredHeight = _size.minHeight = h;
    }

    private static void Place(RectTransform rt, int x, int y, int w, int h)
    {
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0, 1);
        rt.anchoredPosition = new Vector2(x * ItemGrid.CellSize + 1, -(y * ItemGrid.CellSize + 1));
        rt.sizeDelta = new Vector2(w * ItemGrid.CellSize - 1, h * ItemGrid.CellSize - 1);
    }
}
