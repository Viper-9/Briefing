using System;
using System.Collections.Generic;
using System.Linq;
using Briefing.Core.Contract;

namespace Briefing.Core.Screen;

// 장비 탭의 조건 줄 하나. Key("questId/줄 번호")로 "외 N종" 상자가 열려 있는지 기억한다. 착용 금지 줄은 Warn 이라 라벨이 주황.
// More 가 null 이면 상자도 없다.
public sealed class GearLineModel
{
    public GearLineModel(string key, string label, bool warn, GearMark mark, string lead, string tag, string more, IReadOnlyList<GearAlt> alternatives)
    {
        Key = key;
        Label = label;
        Warn = warn;
        Mark = mark;
        Lead = lead;
        Tag = tag;
        More = more;
        Alternatives = alternatives;
    }

    public string Key { get; }
    public string Label { get; }
    public bool Warn { get; }
    public GearMark Mark { get; }
    public string Lead { get; }
    public string Tag { get; }
    public string More { get; }
    public IReadOnlyList<GearAlt> Alternatives { get; }
}

// 특수 조건 줄. 예: "레이드 | 한 레이드 안에 완료", "탈출 | 생존 · 환승 · X 탈출구". 판정은 없다.
public sealed class SpecialLineModel
{
    public SpecialLineModel(string label, string text)
    {
        Label = label;
        Text = text;
    }

    public string Label { get; }
    public string Text { get; }
}

// 퀘스트 하나의 덩어리. 장비 덩어리는 Rules 와 Verdict, 특수 조건 덩어리는 Specials 만 쓴다. Verdict 는 화면에 그리지 않는다.
public sealed class GearBlockModel
{
    public GearBlockModel(string questId, string title, GearMark verdict,
        IReadOnlyList<GearLineModel> rules, IReadOnlyList<SpecialLineModel> specials)
    {
        QuestId = questId;
        Title = title;
        Verdict = verdict;
        Rules = rules;
        Specials = specials;
    }

    public string QuestId { get; }
    public string Title { get; }
    public GearMark Verdict { get; }
    public IReadOnlyList<GearLineModel> Rules { get; }
    public IReadOnlyList<SpecialLineModel> Specials { get; }
}

// 위에서부터 현재 장비 줄(못 읽었으면 LoadoutError 문구), 무기·장비 조건, 특수 조건.
// Empty 는 덩어리가 하나도 없을 때 보일 문구.
public sealed class GearTabModel
{
    public GearTabModel(string loadoutError, IReadOnlyList<GearBlockModel> gear, IReadOnlyList<GearBlockModel> special, string empty, IReadOnlyList<GearMark> legend)
    {
        Legend = legend ?? Array.Empty<GearMark>();
        LoadoutError = loadoutError;
        Gear = gear;
        Special = special;
        Empty = empty;
    }

    public string LoadoutError { get; }
    public IReadOnlyList<GearBlockModel> Gear { get; }
    public IReadOnlyList<GearBlockModel> Special { get; }
    public string Empty { get; }
    // "무기·장비 조건" 머리 오른쪽 범례. 비어 있으면 범례를 그리지 않는다.
    public IReadOnlyList<GearMark> Legend { get; }
    public int Count => Gear.Count + Special.Count;
}

// 맵의 gear 와 anywhere.gear 를 questId 로 합치고(맵 것 먼저) 라벨과 이름 목록이 같은 줄은 한 번만 둔다(웹 dedupeRows).
// 특수 조건도 같은 식으로 합친다.
public static class GearTab
{
    public static GearTabModel Build(BriefingResponse response, BriefingMap map, string filterQuestId, GearSnapshot gear, Func<string, string> questName)
    {
        var gearRows = Merge((map?.Gear ?? new List<BriefingGear>()).Concat(response.Anywhere.Gear), g => g.QuestId, g => g.Rows,
            r => r.Label + "\u0000" + string.Join("\u0001", r.Names));
        var specialRows = Merge((map?.Special ?? new List<BriefingSpecial>()).Concat(response.Anywhere.Special), s => s.QuestId, s => s.Rows,
            r => r.Kind + "\u0000" + string.Join("\u0001", r.ExitStatuses) + "\u0000" + r.ExitName);

        var blocks = gearRows.Where(q => filterQuestId == null || q.QuestId == filterQuestId)
            .Select(q => GearBlock(q.QuestId, q.Rows, gear, questName)).ToList();
        var specials = specialRows.Where(q => filterQuestId == null || q.QuestId == filterQuestId)
            .Select(q => new GearBlockModel(q.QuestId, questName(q.QuestId), GearMark.Unknown, Array.Empty<GearLineModel>(),
                q.Rows.Select(SpecialLine).Where(l => l != null).ToList()))
            .Where(b => b.Specials.Count > 0)
            .ToList();
        var loadoutError = gear != null && gear.EquipmentRead ? null : Texts.LoadoutUnreadable;
        string empty = null;
        if (blocks.Count + specials.Count == 0) empty = filterQuestId != null ? Texts.FilterEmptyGear : Texts.GearNone;
        // 범례는 조건 덩어리가 있고 장착을 읽었을 때만. 보관함을 못 읽었으면 ◐ 는 뺀다.
        var legend = new List<GearMark>();
        if (blocks.Count > 0 && gear != null && gear.EquipmentRead)
        {
            legend.Add(GearMark.Equipped);
            if (gear.StashRead) legend.Add(GearMark.Stash);
            legend.Add(GearMark.Missing);
        }
        return new GearTabModel(loadoutError, blocks, specials, empty, legend);
    }

    private static GearBlockModel GearBlock(string questId, IReadOnlyList<BriefingGearRow> rows, GearSnapshot gear, Func<string, string> questName)
    {
        var lines = new List<GearLineModel>();
        for (var i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            var check = GearCheck.Row(row, gear);
            var forbidden = row.Label == GearCheck.Forbidden;
            var count = forbidden ? row.Names.Count : row.Names.Count - 1;
            var more = count <= 0 ? null : forbidden ? Texts.ListCount(count) : Texts.MoreAlternatives(count);
            lines.Add(new GearLineModel(questId + "/" + i, Texts.GearLabel(row.Label), forbidden, check.Mark, check.Lead, check.Tag, more, check.Alternatives));
        }
        var verdict = GearCheck.Overall(lines.Select(l => l.Mark));
        return new GearBlockModel(questId, questName(questId), verdict, lines, Array.Empty<SpecialLineModel>());
    }

    // exit 줄은 탈출 상태와 탈출구 이름(웹 exitText). 둘 다 없거나 모르는 종류면 null.
    public static SpecialLineModel SpecialLine(BriefingSpecialRow row)
    {
        if (row?.Kind == "oneRaid") return new SpecialLineModel(Texts.SpecialRaid, Texts.OneRaid);
        if (row?.Kind != "exit") return null;
        var parts = row.ExitStatuses.Where(s => !string.IsNullOrEmpty(s)).Select(Texts.ExitStatus).ToList();
        if (!string.IsNullOrEmpty(row.ExitName)) parts.Add(Texts.ExitVia(row.ExitName));
        return parts.Count == 0 ? null : new SpecialLineModel(Texts.SpecialExit, string.Join(" · ", parts));
    }

    // questId 로 합치고(처음 나온 순서) 같은 키의 줄은 한 번만 둔다.
    private static List<(string QuestId, List<TRow> Rows)> Merge<TBlock, TRow>(IEnumerable<TBlock> blocks, Func<TBlock, string> questId,
        Func<TBlock, List<TRow>> rows, Func<TRow, string> key)
    {
        var order = new List<(string QuestId, List<TRow> Rows)>();
        var byQuest = new Dictionary<string, (List<TRow> Rows, HashSet<string> Seen)>();
        foreach (var block in blocks)
        {
            var id = block == null ? null : questId(block);
            if (id == null) continue;
            if (!byQuest.TryGetValue(id, out var entry))
            {
                entry = (new List<TRow>(), new HashSet<string>());
                byQuest[id] = entry;
                order.Add((id, entry.Rows));
            }
            foreach (var row in rows(block))
                if (row != null && entry.Seen.Add(key(row))) entry.Rows.Add(row);
        }
        return order;
    }
}
