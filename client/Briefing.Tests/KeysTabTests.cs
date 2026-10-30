using System.Collections.Generic;
using System.Linq;
using Briefing.Core.Contract;
using Briefing.Core.Screen;

namespace Briefing.Tests;

// 열쇠 탭: requiredKeys 묶음을 지닌 것, 보관함, 없는 것으로 나눈다. Texts 를 읽으므로 Texts 컬렉션에 둔다.
[Collection("Texts")]
public class KeysTabTests
{
    private static BriefingItemRef Key(string tpl) => new BriefingItemRef { Tpl = tpl, Name = "Key " + tpl };

    private static BriefingObjective Obj(string id, bool done = false, params List<BriefingItemRef>[] groups) =>
        new BriefingObjective { ConditionId = id, Done = done, RequiredKeys = groups.ToList() };

    private static BriefingQuest Quest(string id, params BriefingObjective[] objectives) =>
        new BriefingQuest { QuestId = id, Name = "Quest " + id, Objectives = objectives.ToList() };

    private static BriefingMap Map(params BriefingQuest[] quests) => new BriefingMap { Key = "m", Name = "M", Quests = quests.ToList() };

    private static GearSnapshot Gear(IEnumerable<string> carried, IEnumerable<string> stash = null) => new GearSnapshot
    {
        EquipmentRead = true,
        StashRead = stash != null,
        Carried = new HashSet<string>(carried),
        Stash = new HashSet<string>(stash ?? Enumerable.Empty<string>()),
    };

    [Fact]
    public void Splits_keys_into_missing_stash_and_carried()
    {
        var map = Map(Quest("a", Obj("a1", false, new List<BriefingItemRef> { Key("k1") }, new List<BriefingItemRef> { Key("k2") })),
            Quest("b", Obj("b1", false, new List<BriefingItemRef> { Key("k3") })));

        var m = KeysTab.Build(map, null, Gear(new[] { "k3" }, new[] { "k2" }));

        Assert.Equal(3, m.Count);
        Assert.Null(m.Empty);
        Assert.Equal(new[] { "없는 열쇠", "보관함에 있는 열쇠", "지닌 열쇠" }, m.Sections.Select(s => s.Title));
        Assert.Equal(new[] { "k1", "k2", "k3" }, m.Sections.Select(s => s.Cells.Single().Tpl));
        Assert.Equal(new[] { "0/1", "0/1", "1/1" }, m.Sections.Select(s => s.Cells.Single().Count));
        Assert.Equal(new[] { true, true, false }, m.Sections.Select(s => s.Cells.Single().Short));
        Assert.Equal("Quest b", m.Sections[2].Cells.Single().TipQuests);
    }

    [Fact]
    public void Any_one_key_of_a_group_is_enough_and_the_owned_one_is_shown()
    {
        var map = Map(Quest("a", Obj("a1", false, new List<BriefingItemRef> { Key("k1"), Key("k2") })));

        var cell = KeysTab.Build(map, null, Gear(new[] { "k2" })).Sections.Single().Cells.Single();

        Assert.Equal("k2", cell.Tpl);
        Assert.Equal("1/1", cell.Count);
        Assert.Equal(1, cell.Alternatives);
        Assert.Equal("또는: Key k1", cell.TipAlternatives);
    }

    [Fact]
    public void Same_group_from_two_quests_is_one_cell()
    {
        var map = Map(Quest("a", Obj("a1", false, new List<BriefingItemRef> { Key("k1") })),
            Quest("b", Obj("b1", false, new List<BriefingItemRef> { Key("k1") })));

        var m = KeysTab.Build(map, null, null);

        Assert.Equal(1, m.Count);
        var section = m.Sections.Single();
        Assert.Equal("지니지 않은 열쇠", section.Title); // 보관함을 못 읽으면 없는지 알 수 없다
        Assert.Equal("Quest a, Quest b", section.Cells.Single().TipQuests);
    }

    [Fact]
    public void Done_objectives_and_other_quests_under_the_filter_are_skipped()
    {
        var map = Map(Quest("a", Obj("a1", true, new List<BriefingItemRef> { Key("k1") })),
            Quest("b", Obj("b1", false, new List<BriefingItemRef> { Key("k2") })));

        Assert.Equal("k2", KeysTab.Build(map, null, null).Sections.Single().Cells.Single().Tpl);
        var filtered = KeysTab.Build(map, "a", null);
        Assert.Equal(0, filtered.Count);
        Assert.Empty(filtered.Sections);
        Assert.Equal("이 퀘스트는 열쇠가 필요 없습니다", filtered.Empty);
        Assert.Equal("필요한 열쇠 없음", KeysTab.Build(null, null, null).Empty);
    }

    [Fact]
    public void Parses_required_keys_and_defaults_to_empty()
    {
        const string json = """
            { "schemaVersion": 1, "maps": [ { "key": "m", "quests": [ { "questId": "q", "objectives": [
              { "conditionId": "c1", "requiredKeys": [ [ { "tpl": "k1", "name": "Key 1" } ], [ { "tpl": "k2", "name": "Key 2" }, { "tpl": "k3", "name": "Key 3" } ] ] },
              { "conditionId": "c2" } ] } ] } ] }
            """;

        var result = BriefingParser.Parse(json);

        Assert.Equal(ParseOutcome.Ok, result.Outcome);
        var objectives = result.Response.Maps.Single().Quests.Single().Objectives;
        Assert.Equal(new[] { 1, 2 }, objectives[0].RequiredKeys.Select(g => g.Count));
        Assert.Equal("Key 3", objectives[0].RequiredKeys[1][1].Name);
        Assert.Empty(objectives[1].RequiredKeys);
    }
}
