using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Briefing.Core.Contract;

namespace Briefing.Core.Screen;

// 맵 칩. 목표가 없는 맵도 지도와 탈출구를 보려고 누를 수 있게 두고 흐리게만 그린다.
// InRaid 면 지금 레이드 중인 맵이라 칩에 표시한다.
public sealed class MapChip
{
    public MapChip(string key, string name, int count, bool selected, bool inRaid = false)
    {
        Key = key;
        Name = name;
        Count = count;
        Selected = selected;
        InRaid = inRaid;
    }

    public string Key { get; }
    public string Name { get; }
    public int Count { get; }
    public bool Selected { get; }
    public bool InRaid { get; }
    public bool Dim => Count == 0;
}

// 화면이 그대로 찍는 값만 담는다. target 이 없으면 Progress·Fraction 은 null 이고 수치와 막대를 숨긴다.
public sealed class QuestRow
{
    public QuestRow(string questId, string name, string summary, int? marker, string markerColor,
        string traderId, string traderName, string traderInitial, string progress, float? fraction, bool handInReady = false)
    {
        QuestId = questId;
        Name = name;
        Summary = summary;
        Marker = marker;
        MarkerColor = markerColor;
        TraderId = traderId;
        TraderName = traderName;
        TraderInitial = traderInitial;
        Progress = progress;
        Fraction = fraction;
        HandInReady = handInReady;
    }

    public string QuestId { get; }
    public string Name { get; }
    public string Summary { get; }
    public int? Marker { get; }
    public string MarkerColor { get; }
    public string TraderId { get; }
    public string TraderName { get; }
    public string TraderInitial { get; }
    public string Progress { get; }
    public float? Fraction { get; }
    // 지금 건네줄 수 있는 목표가 있으면 이름 옆에 손 아이콘을 붙인다.
    public bool HandInReady { get; }
}

// 접히는 묶음 하나("세관에서 진행", "아무 맵에서나" 등). EmptyText 는 줄이 없을 때만 값이 있다.
public sealed class QuestSection
{
    public QuestSection(string key, string title, IReadOnlyList<QuestRow> rows, string emptyText)
    {
        Key = key;
        Title = title;
        Rows = rows;
        EmptyText = emptyText;
    }

    public string Key { get; }
    public string Title { get; }
    public IReadOnlyList<QuestRow> Rows { get; }
    public int Count => Rows.Count;
    public string EmptyText { get; }
}

public sealed class LeftColumnModel
{
    public LeftColumnModel(IReadOnlyList<MapChip> chips, string selectedKey, QuestSection here, QuestSection anywhere,
        QuestSection ready = null, QuestDetailModel expanded = null, QuestSection turnIn = null)
    {
        Chips = chips;
        SelectedKey = selectedKey;
        Here = here;
        Anywhere = anywhere;
        Ready = ready;
        Expanded = expanded;
        TurnIn = turnIn;
    }

    public IReadOnlyList<MapChip> Chips { get; }
    public string SelectedKey { get; }
    public QuestSection Here { get; }     // 맵이 하나도 없으면 null
    public QuestSection Anywhere { get; } // 퀘스트가 없으면 null 이라 묶음째 숨는다
    public QuestSection Ready { get; }    // "제출". 레이드 중이거나 줄이 없으면 null
    public QuestDetailModel Expanded { get; } // 펼친 퀘스트가 보이는 묶음에 있을 때만
    public QuestSection TurnIn { get; }   // "완료 가능", 맨 위. 레이드 중이거나 못 읽었거나 줄이 없으면 null
}

// 맵 칩 줄과 왼쪽 열. 순서·마커·요약·수치는 서버 것을 그대로 쓰고 여기서는 모양만 바꾼다.
public static class LeftColumn
{
    public const string HereKey = "here";
    public const string AnywhereKey = "anywhere";
    public const string ReadyKey = "ready";
    public const string TurnInKey = "turnin";

    // 목표가 있는 첫 맵, 없으면 첫 맵.
    public static string DefaultMapKey(BriefingResponse response) =>
        (response.Maps.FirstOrDefault(m => m.Count > 0) ?? response.Maps.FirstOrDefault())?.Key;

    // preferredKey 의 맵, 없으면 DefaultMapKey. 맵 칩·목록·지도가 모두 이 맵을 쓴다.
    public static BriefingMap SelectedMap(BriefingResponse response, string preferredKey)
    {
        var key = response.Maps.Any(m => m.Key == preferredKey) ? preferredKey : DefaultMapKey(response);
        return response.Maps.FirstOrDefault(m => m.Key == key);
    }

    // goals 가 null 이면 레이드 중이라 건네주기 줄과 "제출" 묶음을 만들지 않는다.
    // objectives 가 null 이면 게임 목표를 못 읽은 것이라 펼침은 서버 목표로만 그린다.
    // turnIns 가 null 이면(레이드 중이거나 못 읽음) "완료 가능" 묶음이 없다.
    public static LeftColumnModel Build(BriefingResponse response, string preferredKey,
        IReadOnlyList<HandoverGoal> goals = null, string expandedSection = null, string expandedQuestId = null,
        IReadOnlyList<GameObjective> objectives = null, IReadOnlyList<TurnInQuest> turnIns = null, string raidMapKey = null)
    {
        var selected = SelectedMap(response, preferredKey);
        var chips = response.Maps.Select(m => new MapChip(m.Key, m.Name, m.Count, m == selected, raidMapKey != null && m.Key == raidMapKey)).ToList();
        // "완료 가능" 쪽에서 예외가 나도 나머지 묶음은 그린다.
        // 서버 응답이 게임보다 늦어 진행 중으로 온 퀘스트라도 지금은 보고만 하면 되므로 다른 묶음에서는 뺀다. 맵 칩 수는 서버 값 그대로.
        QuestSection turnIn = null;
        try { turnIn = TurnIn(response, turnIns); }
        catch (Exception) { turnIn = null; }
        var finished = new HashSet<string>(turnIn?.Rows.Select(r => r.QuestId) ?? Enumerable.Empty<string>());
        var hereQuests = selected?.Quests.Where(q => !finished.Contains(q.QuestId)).ToList();
        var anywhereQuests = response.Anywhere.Quests.Where(q => !finished.Contains(q.QuestId)).ToList();
        var handIn = new HashSet<string>(goals?.Where(g => g != null && g.Ready).Select(g => g.QuestId) ?? Enumerable.Empty<string>());
        var here = selected == null
            ? null
            : Section(HereKey, Texts.HereTitle(selected.Name), hereQuests, response.Traders, Texts.NoQuestsHere, handIn);
        var anywhere = anywhereQuests.Count == 0
            ? null
            : Section(AnywhereKey, Texts.AnywhereTitle, anywhereQuests, response.Traders, null, handIn);
        // 건네주기 쪽에서 예외가 나도 맵 묶음은 그린다.
        QuestSection ready = null;
        QuestDetailModel expanded = null;
        try { ready = Ready(response, goals?.Where(g => !finished.Contains(g.QuestId)).ToList(), hereQuests, anywhereQuests); }
        catch (Exception) { ready = null; }
        try { expanded = Expanded(hereQuests, anywhereQuests, here, anywhere, ready, turnIn, goals, objectives, expandedSection, expandedQuestId); }
        catch (Exception) { expanded = null; }
        return new LeftColumnModel(chips, selected?.Key, here, anywhere, ready, expanded, turnIn);
    }

    // 이름순(응답 언어의 문화권), 같은 이름은 id 순. 마커와 진행 수치는 없고 같은 퀘스트는 한 줄만.
    private static QuestSection TurnIn(BriefingResponse response, IReadOnlyList<TurnInQuest> turnIns)
    {
        if (turnIns == null) return null;
        var names = NameOrder(response.Lang);
        var rows = turnIns.Where(t => t?.QuestId != null)
            .GroupBy(t => t.QuestId)
            .Select(group => group.First())
            .Select(t => Row(new BriefingQuest
            {
                QuestId = t.QuestId,
                TraderId = t.TraderId,
                Name = t.QuestName,
                Summary = Texts.TurnInSummary,
            }, response.Traders))
            .OrderBy(r => r.Name ?? "", names).ThenBy(r => r.QuestId, StringComparer.Ordinal)
            .ToList();
        return rows.Count == 0 ? null : new QuestSection(TurnInKey, Texts.TurnInTitle, rows, null);
    }

    // 건네줄 목표가 남은 퀘스트 중 위의 두 묶음에 없는 것. 서버는 레이드 목표만 보내므로 건네주기만 남은 퀘스트는 여기서만 보인다.
    // 지금 낼 수 있는 퀘스트가 먼저, 그 안에서는 이름순. 요약은 남은 목표가 하나면 그 문장, 여럿이면 개수.
    private static QuestSection Ready(BriefingResponse response, IReadOnlyList<HandoverGoal> goals,
        IEnumerable<BriefingQuest> here, IEnumerable<BriefingQuest> anywhere)
    {
        if (goals == null) return null;
        var shown = new HashSet<string>((here ?? Enumerable.Empty<BriefingQuest>()).Concat(anywhere).Select(q => q.QuestId));
        var names = NameOrder(response.Lang);
        var rows = goals.Where(g => !shown.Contains(g.QuestId))
            .GroupBy(g => g.QuestId)
            .Select(group =>
            {
                var first = group.First();
                var count = group.Count();
                var row = Row(new BriefingQuest
                {
                    QuestId = first.QuestId,
                    TraderId = first.TraderId,
                    Name = first.QuestName,
                    Summary = count == 1 ? first.Text : Texts.HandoverCount(count),
                }, response.Traders);
                return (Row: row, AnyReady: group.Any(g => g.Ready));
            })
            .OrderBy(x => x.AnyReady ? 0 : 1).ThenBy(x => x.Row.Name ?? "", names).ThenBy(x => x.Row.QuestId, StringComparer.Ordinal)
            .Select(x => WithHandIn(x.Row, x.AnyReady))
            .ToList();
        return rows.Count == 0 ? null : new QuestSection(ReadyKey, Texts.ReadyTitle, rows, null);
    }

    // 같은 퀘스트가 두 묶음에 있을 수 있어 묶음 키로 찾는다.
    private static QuestDetailModel Expanded(IReadOnlyList<BriefingQuest> hereQuests, IReadOnlyList<BriefingQuest> anywhereQuests,
        QuestSection here, QuestSection anywhere, QuestSection ready, QuestSection turnIn, IReadOnlyList<HandoverGoal> goals,
        IReadOnlyList<GameObjective> objectives, string section, string questId)
    {
        if (section == null || questId == null) return null;
        var target = new[] { turnIn, here, anywhere, ready }.FirstOrDefault(s => s != null && s.Key == section);
        if (target == null || target.Rows.All(r => r.QuestId != questId)) return null;
        var source = section == HereKey ? hereQuests : section == AnywhereKey ? anywhereQuests : null;
        // "완료 가능" 묶음은 서버 퀘스트 없이 게임 목표만 그리고 "완료하러 가기" 버튼을 켠다.
        return QuestDetail.Build(section, questId, source?.FirstOrDefault(q => q.QuestId == questId), goals, objectives,
            canTurnIn: section == TurnInKey);
    }

    // 서버가 이름을 정렬할 때와 같은 문화권. 못 만들면 Invariant.
    public static StringComparer NameOrder(string lang)
    {
        var culture = lang == "kr" ? "ko-KR" : lang == "ru" ? "ru-RU" : null;
        if (culture == null) return StringComparer.InvariantCulture;
        try { return StringComparer.Create(CultureInfo.GetCultureInfo(culture), false); }
        catch (Exception) { return StringComparer.InvariantCulture; }
    }

    public static QuestRow Row(BriefingQuest quest, IReadOnlyDictionary<string, BriefingTrader> traders)
    {
        BriefingTrader trader = null;
        if (quest.TraderId != null) traders.TryGetValue(quest.TraderId, out trader);
        var traderName = trader?.Name;
        return new QuestRow(quest.QuestId, quest.Name, quest.Summary, quest.Marker,
            quest.Marker.HasValue ? MarkerPalette.Hex(quest.Marker.Value) : null,
            quest.TraderId, traderName, Initial(traderName),
            ProgressText(quest.Current, quest.Target), Fraction(quest.Current, quest.Target));
    }

    private static QuestRow WithHandIn(QuestRow r, bool handInReady) =>
        new QuestRow(r.QuestId, r.Name, r.Summary, r.Marker, r.MarkerColor, r.TraderId, r.TraderName, r.TraderInitial, r.Progress,
            r.Fraction, handInReady);

    public static string ProgressText(double? current, double? target) =>
        target == null ? null : $"{Number(current ?? 0)} / {Number(target.Value)}";

    public static float? Fraction(double? current, double? target)
    {
        if (target == null || target.Value <= 0) return null;
        return (float)Math.Max(0, Math.Min(1, (current ?? 0) / target.Value));
    }

    // 상인 얼굴이 없을 때 대신 쓰는 첫 글자.
    public static string Initial(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return Texts.UnknownTraderInitial;
        return StringInfo.GetNextTextElement(name.Trim(), 0).ToUpperInvariant();
    }

    private static QuestSection Section(string key, string title, IEnumerable<BriefingQuest> quests,
        IReadOnlyDictionary<string, BriefingTrader> traders, string emptyText, ISet<string> handIn)
    {
        var rows = quests.Select(q => WithHandIn(Row(q, traders), handIn.Contains(q.QuestId))).ToList();
        return new QuestSection(key, title, rows, rows.Count == 0 ? emptyText : null);
    }

    private static string Number(double value) => value.ToString("0.##", CultureInfo.InvariantCulture);
}
