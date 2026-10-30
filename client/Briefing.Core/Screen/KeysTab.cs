using System.Collections.Generic;
using System.Linq;
using Briefing.Core.Contract;

namespace Briefing.Core.Screen;

public sealed class KeysTabModel
{
    public KeysTabModel(IReadOnlyList<ItemSectionModel> sections, string empty, int count)
    {
        Sections = sections;
        Empty = empty;
        Count = count;
    }

    public IReadOnlyList<ItemSectionModel> Sections { get; }
    public string Empty { get; }
    public int Count { get; }
}

// 이 맵에서 남은 목표의 requiredKeys(QuestCodex 가 tarkov.dev 스냅샷으로 보낸다). 묶음 하나가 칸 하나이고
// 묶음 안의 열쇠는 하나만 있으면 되므로 나머지는 "+N" 대안으로 보인다. 같은 묶음은 퀘스트를 모아 한 칸으로 합친다.
// 지닌 것(장착품 안 어디든), 보관함에만 있는 것, 없는 것으로 나눈다. 보관함을 못 읽었으면(레이드 중) 지니지 않은 것은 한 묶음이다.
public static class KeysTab
{
    public static KeysTabModel Build(BriefingMap map, string filterQuestId, GearSnapshot gear)
    {
        var groups = new List<(List<BriefingItemRef> Keys, List<string> Quests)>();
        foreach (var quest in map?.Quests ?? new List<BriefingQuest>())
        {
            if (quest == null || (filterQuestId != null && quest.QuestId != filterQuestId)) continue;
            foreach (var objective in quest.Objectives.Where(o => o != null && !o.Done))
            {
                foreach (var group in objective.RequiredKeys)
                {
                    var keys = group?.Where(k => !string.IsNullOrEmpty(k?.Tpl)).ToList();
                    if (keys == null || keys.Count == 0) continue;
                    var same = groups.FindIndex(g => g.Keys.Select(k => k.Tpl).SequenceEqual(keys.Select(k => k.Tpl)));
                    if (same < 0) groups.Add((keys, new List<string> { quest.Name }));
                    else if (!groups[same].Quests.Contains(quest.Name)) groups[same].Quests.Add(quest.Name);
                }
            }
        }

        var carried = gear != null && gear.EquipmentRead ? gear.Carried : new HashSet<string>();
        var stash = gear != null && gear.StashRead ? gear.Stash : null;
        var missing = new List<ItemCellModel>();
        var inStash = new List<ItemCellModel>();
        var withYou = new List<ItemCellModel>();
        foreach (var (keys, quests) in groups)
        {
            var have = keys.FirstOrDefault(k => carried.Contains(k.Tpl));
            var stored = have == null && stash != null ? keys.FirstOrDefault(k => stash.Contains(k.Tpl)) : null;
            if (have != null) withYou.Add(Cell(keys, have, true, Texts.KeyCarried, quests));
            else if (stored != null) inStash.Add(Cell(keys, stored, false, Texts.KeyInStash, quests));
            else missing.Add(Cell(keys, keys[0], false, stash != null ? Texts.KeyMissing : Texts.KeyNotCarried, quests));
        }

        var sections = new List<ItemSectionModel>();
        if (missing.Count > 0) sections.Add(new ItemSectionModel(stash != null ? Texts.KeysMissing : Texts.KeysNotCarried, null, missing, null));
        if (inStash.Count > 0) sections.Add(new ItemSectionModel(Texts.KeysInStash, null, inStash, null));
        if (withYou.Count > 0) sections.Add(new ItemSectionModel(Texts.KeysCarried, null, withYou, null));
        var empty = groups.Count > 0 ? null : filterQuestId != null ? Texts.FilterEmptyKeys : Texts.KeysNone;
        return new KeysTabModel(sections, empty, groups.Count);
    }

    // 칸에는 지닌(또는 보관함의) 열쇠를 보이고 묶음의 나머지 열쇠는 "또는" 줄로 보인다.
    private static ItemCellModel Cell(List<BriefingItemRef> keys, BriefingItemRef shown, bool ok, string tipCount, List<string> quests)
    {
        var others = keys.Where(k => k != shown).Select(Name).ToList();
        return new ItemCellModel(shown.Tpl, Name(shown), ok ? "1/1" : "0/1", !ok, false, others.Count,
            others.Count > 0 ? Texts.OrItems(string.Join(", ", others)) : null, tipCount, string.Join(", ", quests));
    }

    private static string Name(BriefingItemRef key) => string.IsNullOrWhiteSpace(key.Name) ? key.Tpl : key.Name.Trim();
}
