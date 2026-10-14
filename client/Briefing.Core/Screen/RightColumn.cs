using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Briefing.Core.Contract;

namespace Briefing.Core.Screen;

public enum RightTabKey
{
    Bring,
    Find,
    Gear,
}

// Count 는 필터가 켜져 있으면 걸러진 뒤의 수.
public sealed class RightTab
{
    public RightTab(RightTabKey key, string label, int count)
    {
        Key = key;
        Label = label;
        Count = count;
    }

    public RightTabKey Key { get; }
    public string Label { get; }
    public int Count { get; }
}

// 준비물·수집 한 줄이 격자 칸 하나다. 칸 크기, 아이콘, 짧은 이름은 Client 가 Tpl 로 게임에서 찾아 채운다.
// Name 은 짧은 이름이 없을 때 칸에 쓰고 말풍선 첫 줄로도 쓴다. Short 면 수가 빨강, 아니면 초록.
// Alternatives 가 있으면 칸에 FIR 동그라미 대신 "+N" 을 쓴다. Tip* 가 null 이면 말풍선에서 그 줄을 뺀다.
public sealed class ItemCellModel
{
    public ItemCellModel(string tpl, string name, string count, bool @short, bool fir, int alternatives,
        string tipAlternatives, string tipCount, string tipQuests)
    {
        Tpl = tpl;
        Name = name;
        Count = count;
        Short = @short;
        Fir = fir;
        Alternatives = alternatives;
        TipAlternatives = tipAlternatives;
        TipCount = tipCount;
        TipQuests = tipQuests;
    }

    public string Tpl { get; }
    public string Name { get; }
    public string Count { get; }
    public bool Short { get; }
    public bool Fir { get; }
    public int Alternatives { get; }
    public string TipAlternatives { get; }
    public string TipCount { get; }
    public string TipQuests { get; }
}

// 머리줄과 격자. Note 는 머리줄 오른쪽 빨간 글("부족 N"). EmptyText 는 칸이 없을 때 머리줄 아래 문구.
public sealed class ItemSectionModel
{
    public ItemSectionModel(string title, string note, IReadOnlyList<ItemCellModel> cells, string emptyText)
    {
        Title = title;
        Note = note;
        Cells = cells;
        EmptyText = emptyText;
    }

    public string Title { get; }
    public string Note { get; }
    public IReadOnlyList<ItemCellModel> Cells { get; }
    public string EmptyText { get; }
}

// 세 탭을 다 계산해 두고 고른 탭만 그린다. 나머지는 탭 숫자에만 쓴다. *Empty 가 있으면 그 탭은 문구만 보인다.
// FilterText 는 탭 줄 아래 금색 띠 문구이고 필터가 꺼져 있으면 null.
public sealed class RightColumnModel
{
    public RightColumnModel(IReadOnlyList<RightTab> tabs, string filterText,
        IReadOnlyList<ItemSectionModel> bring, string bringEmpty, IReadOnlyList<ItemSectionModel> find, string findEmpty, GearTabModel gear)
    {
        Tabs = tabs;
        FilterText = filterText;
        Bring = bring;
        BringEmpty = bringEmpty;
        Find = find;
        FindEmpty = findEmpty;
        Gear = gear;
    }

    public IReadOnlyList<RightTab> Tabs { get; }
    public string FilterText { get; }
    public IReadOnlyList<ItemSectionModel> Bring { get; }
    public string BringEmpty { get; }
    public IReadOnlyList<ItemSectionModel> Find { get; }
    public string FindEmpty { get; }
    public GearTabModel Gear { get; }
}

// 규칙은 웹 레이드 준비 화면(RaidView.tsx)과 같다. 준비물은 이 맵 다음 아무 맵에서나, 둘을 섞지 않는다.
// 수집은 이 맵(비어도 제목은 둔다) 다음 모든 맵(비면 뺀다). elsewhere 는 그리지 않는다. 순서는 서버 그대로.
public static class RightColumn
{
    // filterQuestName 이 null 이면 이름을 찾아 쓴다. gameQuestName 은 응답 퀘스트 목록에 없는 출처 퀘스트의 이름을 게임에서 찾는다.
    // gear 가 null 이면 장비를 못 읽은 것이라 판정 없이 첫 이름만 보인다.
    public static RightColumnModel Build(BriefingResponse response, BriefingMap map, string filterQuestId, string filterQuestName,
        Func<string, string> gameQuestName, GearSnapshot gear = null)
    {
        var names = QuestNames(response);
        string Name(string questId) => QuestName(names, gameQuestName, questId);
        bool Keep(BriefingItemRow row) => row != null && (filterQuestId == null || row.Sources.Any(s => s?.QuestId == filterQuestId));

        var bring = new List<ItemSectionModel>();
        if (map != null) AddBring(bring, Texts.BringHere, map.Bring.Where(Keep), Name);
        AddBring(bring, Texts.AnywhereTitle, response.Anywhere.Bring.Where(Keep), Name);
        var bringCount = bring.Sum(s => s.Cells.Count);
        var bringEmpty = bringCount > 0 ? null : filterQuestId != null ? Texts.FilterEmptyBring : Texts.BringNone;

        var find = new List<ItemSectionModel>();
        if (map != null)
        {
            var here = map.Find.Where(r => r != null && r.Place == "here" && Keep(r)).Select(r => Cell(r, Name)).ToList();
            var unknown = map.Find.Where(r => r != null && r.Place == "unknown" && Keep(r)).Select(r => Cell(r, Name)).ToList();
            find.Add(new ItemSectionModel(Texts.FindHere(map.Name), null, here, here.Count == 0 ? Texts.FindNone : null));
            if (unknown.Count > 0) find.Add(new ItemSectionModel(Texts.FindAnyMap, null, unknown, null));
        }
        var findCount = find.Sum(s => s.Cells.Count);
        string findEmpty = null;
        if (filterQuestId != null && findCount == 0) findEmpty = Texts.FilterEmptyFind;
        else if (map == null) findEmpty = Texts.FindNone;
        if (findEmpty != null) find.Clear();

        var gearTab = GearTab.Build(response, map, filterQuestId, gear, Name);
        var tabs = new[]
        {
            new RightTab(RightTabKey.Bring, Texts.TabBring, bringCount),
            new RightTab(RightTabKey.Find, Texts.TabFind, findCount),
            new RightTab(RightTabKey.Gear, Texts.TabGear, gearTab.Count),
        };
        var filterText = filterQuestId == null ? null : Texts.FilterOnly(filterQuestName ?? Name(filterQuestId));
        return new RightColumnModel(tabs, filterText, bring, bringEmpty, find, findEmpty, gearTab);
    }

    // 필터는 지금 보이는 펼친 줄의 퀘스트다. ✕ 로 껐으면 없다. 이름은 그 줄에 보이는 것을 쓴다.
    public static (string QuestId, string Name) Filter(LeftColumnModel left, bool filterOff)
    {
        var expanded = left?.Expanded;
        if (expanded == null || filterOff) return (null, null);
        var section = new[] { left.TurnIn, left.Here, left.Anywhere, left.Ready }.FirstOrDefault(s => s != null && s.Key == expanded.SectionKey);
        return (expanded.QuestId, section?.Rows.FirstOrDefault(r => r.QuestId == expanded.QuestId)?.Name);
    }

    // 빈 묶음은 뺀다. "부족 N" 은 부족한 줄 수.
    private static void AddBring(List<ItemSectionModel> sections, string title, IEnumerable<BriefingItemRow> rows, Func<string, string> name)
    {
        var list = rows.ToList();
        if (list.Count == 0) return;
        var shortRows = list.Count(r => r.Missing > 0);
        sections.Add(new ItemSectionModel(title, shortRows > 0 ? Texts.ShortRows(shortRows) : null, list.Select(r => Cell(r, name)).ToList(), null));
    }

    public static ItemCellModel Cell(BriefingItemRow row, Func<string, string> questName)
    {
        var items = row.Items.Where(i => i != null).ToList();
        var first = items.FirstOrDefault();
        var name = Clean(first?.Name) ?? first?.Tpl ?? "";
        var others = items.Skip(1).Select(i => Clean(i.Name) ?? i.Tpl).Where(n => !string.IsNullOrEmpty(n)).ToList();
        var count = $"{Number(row.Have)}/{Number(row.Need)}";
        var tipCount = Texts.HaveNeed(Number(row.Have), Number(row.Need));
        if (row.Fir) tipCount += " · FIR";
        if (row.Missing > 0) tipCount += " · " + Texts.ShortBy(Number(row.Missing));
        var quests = row.Sources.Where(s => s?.QuestId != null).Select(s => questName(s.QuestId)).Distinct().ToList();
        return new ItemCellModel(first?.Tpl, name, count, row.Missing > 0, row.Fir, Math.Max(0, items.Count - 1),
            others.Count > 0 ? Texts.OrItems(string.Join(", ", others)) : null, tipCount,
            quests.Count > 0 ? string.Join(", ", quests) : null);
    }

    // 모든 맵과 anywhere 의 퀘스트 이름. 같은 퀘스트가 여러 곳에 있으면 처음 것.
    public static Dictionary<string, string> QuestNames(BriefingResponse response)
    {
        var names = new Dictionary<string, string>();
        foreach (var q in response.Maps.SelectMany(m => m.Quests).Concat(response.Anywhere.Quests))
            if (q?.QuestId != null && !string.IsNullOrEmpty(q.Name) && !names.ContainsKey(q.QuestId)) names[q.QuestId] = q.Name;
        return names;
    }

    // 응답에 있으면 그 이름, 없으면 게임 로케일, 그것도 없으면 questId.
    public static string QuestName(IReadOnlyDictionary<string, string> names, Func<string, string> gameQuestName, string questId)
    {
        if (questId == null) return "";
        if (names.TryGetValue(questId, out var name)) return name;
        string game = null;
        try { game = gameQuestName?.Invoke(questId); }
        catch (Exception) { game = null; }
        return string.IsNullOrWhiteSpace(game) ? questId : game;
    }

    private static string Clean(string name)
    {
        var t = name?.Trim();
        return string.IsNullOrEmpty(t) ? null : t;
    }

    internal static string Number(double value) => value.ToString("0.##", CultureInfo.InvariantCulture);
}
