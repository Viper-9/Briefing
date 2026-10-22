using System;
using System.Collections.Generic;
using System.Linq;
using Briefing.Core.Contract;

namespace Briefing.Core.Screen;

// Objective 는 이 묶음의 서버 목표(문구, 마커 원, 수치). Handover 는 건네주기 버튼이 있는 제출 줄.
// Plain 은 게임 문구와 수치만 있는 줄(찾기, 스킬, 레이드 중 제출 등). Done 은 끝난 목표라 흐리게, 마커 자리에 체크.
public enum DetailLineKind
{
    Objective,
    Handover,
    Plain,
    Done,
}

// 화면이 그대로 찍는 값만 담는다. target 이 없으면 Progress 는 null.
// Items 는 글 아래 작은 아이템 이름 줄. Marker 는 Objective 줄이 지도 좌표를 가질 때만 있다.
// ShowButton 은 Handover 줄에만 켜지고, Ready 면 그 버튼을 지금 누를 수 있다.
public sealed class DetailLine
{
    public DetailLine(DetailLineKind kind, string questId, string conditionId, string text, string items, string progress,
        int? marker = null, string markerColor = null, bool ready = false, bool showButton = false)
    {
        Kind = kind;
        QuestId = questId;
        ConditionId = conditionId;
        Text = text;
        Items = items;
        Progress = progress;
        Marker = marker;
        MarkerColor = markerColor;
        Ready = ready;
        ShowButton = showButton;
    }

    public DetailLineKind Kind { get; }
    public string QuestId { get; }
    public string ConditionId { get; }
    public string Text { get; }
    public string Items { get; }
    public string Progress { get; }
    public int? Marker { get; }
    public string MarkerColor { get; }
    public bool Ready { get; }
    public bool ShowButton { get; }
}

// 같은 퀘스트가 맵 묶음과 "아무 맵에서나"에 함께 있을 수 있고 묶음마다 목표가 달라서 SectionKey 로 어느 줄 아래인지 정한다.
// CanTurnIn 은 "완료 가능" 묶음에서만 켜지고 상인 임무 화면으로 가는 버튼을 그린다.
public sealed class QuestDetailModel
{
    public QuestDetailModel(string sectionKey, string questId, IReadOnlyList<DetailLine> lines, bool canTurnIn = false,
        bool kappa = false, bool lightkeeper = false)
    {
        SectionKey = sectionKey;
        QuestId = questId;
        Lines = lines;
        CanTurnIn = canTurnIn;
        Kappa = kappa;
        Lightkeeper = lightkeeper;
    }

    public string SectionKey { get; }
    public string QuestId { get; }
    public IReadOnlyList<DetailLine> Lines { get; }
    public bool CanTurnIn { get; }
    // 줄 배지의 뜻을 펼침 맨 위에 글로 적는다.
    public bool Kappa { get; }
    public bool Lightkeeper { get; }

    public QuestDetailModel WithGoals(bool kappa, bool lightkeeper) =>
        new QuestDetailModel(SectionKey, QuestId, Lines, CanTurnIn, kappa, lightkeeper);
}

// quest 는 서버 퀘스트이고 "제출" 묶음에서는 null. goals 가 null 이면 레이드 중이라 건네주기 버튼을 그리지 않는다.
// objectives 가 null 이면 게임을 못 읽은 것이라 서버 목표와 건네주기 줄만으로 그린다.
public static class QuestDetail
{
    private const int MaxItemNames = 3;

    // "완료 가능" 묶음도 같은 규칙으로 그린다. 끝난 목표뿐이라 전부 Done 줄이 된다.
    public static QuestDetailModel Build(string sectionKey, string questId, BriefingQuest quest, IReadOnlyList<HandoverGoal> goals,
        IReadOnlyList<GameObjective> objectives = null, bool canTurnIn = false)
    {
        List<DetailLine> lines;
        if (objectives == null) lines = Fallback(questId, quest, goals);
        else
        {
            // 게임 목록과 합치다 실패해도 블록이 사라지지 않게 대체 경로로 그린다.
            try { lines = FromGame(questId, quest, goals, objectives); }
            catch (Exception) { lines = Fallback(questId, quest, goals); }
        }
        return new QuestDetailModel(sectionKey, questId, lines, canTurnIn);
    }

    // 임무 탭처럼 게임 순서대로 그린다. 끝난 목표는 Done.
    // 레이드 목표는 이 묶음의 서버 목표에 있을 때만 그린다(없으면 다른 맵 묶음 것).
    // 제출은 goal 이 있으면(레이드 밖) 건네주기 줄, 없으면 Plain. 나머지는 서버 목표에 있으면 목표 줄, 없으면 Plain.
    // 게임 목록에 없는 서버 목표는 원래 없어야 하지만 혹시 있으면 끝에 붙인다.
    private static List<DetailLine> FromGame(string questId, BriefingQuest quest, IReadOnlyList<HandoverGoal> goals,
        IReadOnlyList<GameObjective> objectives)
    {
        var contract = quest?.Objectives ?? new List<BriefingObjective>();
        var byId = new Dictionary<string, BriefingObjective>();
        foreach (var o in contract)
            if (o.ConditionId != null && !byId.ContainsKey(o.ConditionId)) byId.Add(o.ConditionId, o);

        var lines = new List<DetailLine>();
        var seen = new HashSet<string>();
        foreach (var g in objectives.Where(o => o != null && o.QuestId == questId))
        {
            if (g.ConditionId != null) seen.Add(g.ConditionId);
            var items = ItemsText(g.ItemNames, g.Text);
            var progress = LeftColumn.ProgressText(g.Current, g.Target);
            if (g.Done)
            {
                lines.Add(new DetailLine(DetailLineKind.Done, questId, g.ConditionId, g.Text, items, progress));
                continue;
            }
            switch (g.Kind)
            {
                case GameObjectiveKind.Raid:
                    if (g.ConditionId != null && byId.TryGetValue(g.ConditionId, out var c)) lines.Add(Objective(questId, c, quest.Marker, items));
                    break;
                case GameObjectiveKind.Handover:
                    var goal = goals?.FirstOrDefault(x => x.QuestId == questId && x.ConditionId == g.ConditionId);
                    lines.Add(goal == null
                        ? new DetailLine(DetailLineKind.Plain, questId, g.ConditionId, g.Text, items, progress)
                        : new DetailLine(DetailLineKind.Handover, questId, g.ConditionId, g.Text, items, progress,
                            ready: goal.Ready, showButton: true));
                    break;
                default:
                    // 퀘스트 아이템 찾기처럼 서버가 레이드 목표로 보낸 것은 마커가 있는 목표 줄로 그린다.
                    lines.Add(g.ConditionId != null && byId.TryGetValue(g.ConditionId, out var sent)
                        ? Objective(questId, sent, quest.Marker, items)
                        : new DetailLine(DetailLineKind.Plain, questId, g.ConditionId, g.Text, items, progress));
                    break;
            }
        }
        foreach (var o in contract.Where(o => o.ConditionId == null || !seen.Contains(o.ConditionId)))
            lines.Add(Objective(questId, o, quest.Marker, null));
        return lines;
    }

    // 게임 목표를 못 읽었을 때. 서버 목표 줄 다음에 건네주기 줄(레이드 중이면 없음).
    private static List<DetailLine> Fallback(string questId, BriefingQuest quest, IReadOnlyList<HandoverGoal> goals)
    {
        var lines = quest == null
            ? new List<DetailLine>()
            : quest.Objectives.Select(o => Objective(questId, o, quest.Marker, null)).ToList();
        if (goals != null)
            lines.AddRange(goals.Where(g => g.QuestId == questId)
                .Select(g => new DetailLine(DetailLineKind.Handover, g.QuestId, g.ConditionId, g.Text, ItemsText(g.ItemNames, g.Text),
                    LeftColumn.ProgressText(g.Current, g.Target), ready: g.Ready, showButton: true)));
        return lines;
    }

    // LiveProgress 가 끝났다고 표시한 목표는 대체 경로에서도 Done 줄로 그린다.
    private static DetailLine Objective(string questId, BriefingObjective o, int? questMarker, string items)
    {
        if (o.Done) return new DetailLine(DetailLineKind.Done, questId, o.ConditionId, o.Text, items, LeftColumn.ProgressText(o.Current, o.Target));
        var marker = questMarker.HasValue && HasPlace(o) ? questMarker : null;
        return new DetailLine(DetailLineKind.Objective, questId, o.ConditionId, o.Text, items, LeftColumn.ProgressText(o.Current, o.Target),
            marker, marker.HasValue ? MarkerPalette.Hex(marker.Value) : null);
    }

    // 맵 탭 안의 locations 는 그 탭 것만 오므로 점이나 구역이 있으면 이 지도에 찍힌다.
    private static bool HasPlace(BriefingObjective o) => o.Locations.Any(l => l.Points.Count > 0 || l.Areas.Count > 0);

    // 중복 없이 3개까지 잇고 나머지는 개수로 붙인다. 본문에 이미 모든 이름이 들어 있으면 null.
    public static string ItemsText(IReadOnlyList<string> names, string text)
    {
        var distinct = (names ?? Array.Empty<string>()).Where(n => !string.IsNullOrWhiteSpace(n)).Distinct().ToList();
        if (distinct.Count == 0) return null;
        if (text != null && distinct.All(n => text.Contains(n))) return null;
        var shown = string.Join(", ", distinct.Take(MaxItemNames));
        return distinct.Count > MaxItemNames ? shown + Texts.MoreItems(distinct.Count - MaxItemNames) : shown;
    }
}
