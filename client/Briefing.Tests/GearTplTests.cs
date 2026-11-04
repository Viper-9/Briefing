using System.Collections.Generic;
using System.Linq;
using Briefing.Core.Contract;
using Briefing.Core.Screen;

namespace Briefing.Tests;

// 우클릭 메뉴가 쓸 tpl: 맞는 대안을 앞으로 옮겨도 이름과 짝이 맞아야 하고, 아이템이 아닌 줄(구경, 착용 금지 판정 문구, ⚠ 이유)에는 없다.
[Collection("Texts")]
public class GearTplTests
{
    private static BriefingGearRow Row(string label, params (string Name, string[] Tpls)[] alts) => new BriefingGearRow
    {
        Label = label,
        Names = alts.Select(a => a.Name).ToList(),
        Tpls = alts.Select(a => a.Tpls.ToList()).ToList(),
    };

    private static GearSnapshot Gear(IEnumerable<GearWeapon> weapons = null, IEnumerable<string> worn = null, IEnumerable<string> stash = null) => new GearSnapshot
    {
        EquipmentRead = true,
        StashRead = true,
        Weapons = (weapons ?? Enumerable.Empty<GearWeapon>()).ToList(),
        Worn = new HashSet<string>(worn ?? Enumerable.Empty<string>()),
        Stash = new HashSet<string>(stash ?? Enumerable.Empty<string>()),
    };

    private static readonly BriefingGearRow Smg = Row("weapon", ("MP5", new[] { "mp5" }), ("MP9", new[] { "mp9" }), ("Vector", new[] { "vec" }));

    [Fact]
    public void Tpls_stay_paired_with_names_after_the_match_moves_first()
    {
        var check = GearCheck.Row(Smg, Gear(new[] { new GearWeapon("mp9", "Caliber9x19PARA", null) }));

        Assert.Equal("mp9", check.LeadTpl);
        Assert.Equal(new[] { "mp9", "mp5", "vec" }, check.Alternatives.Select(a => a.Tpl));
    }

    [Fact]
    public void Missing_row_leads_with_the_first_tpl()
    {
        var check = GearCheck.Row(Smg, Gear());

        Assert.Equal(GearMark.Missing, check.Mark);
        Assert.Equal("mp5", check.LeadTpl);
    }

    [Fact]
    public void Unread_equipment_still_carries_tpls()
    {
        var check = GearCheck.Row(Smg, null);

        Assert.Equal(GearMark.Unknown, check.Mark);
        Assert.Equal("mp5", check.LeadTpl);
        Assert.Equal(new[] { "mp5", "mp9", "vec" }, check.Alternatives.Select(a => a.Tpl));
    }

    [Fact]
    public void Group_of_tpls_uses_the_first_one()
    {
        var row = Row("equipment", ("Set", new[] { "first", "second" }));

        Assert.Equal("first", GearCheck.Row(row, Gear()).LeadTpl);
    }

    [Fact]
    public void Caliber_row_has_no_tpl()
    {
        var row = Row("caliber", ("Caliber9x19PARA", new string[0]));

        var check = GearCheck.Row(row, Gear());

        Assert.Null(check.LeadTpl);
        Assert.All(check.Alternatives, a => Assert.Null(a.Tpl));
    }

    [Fact]
    public void Forbidden_lead_is_a_sentence_but_its_list_has_tpls()
    {
        var row = Row("forbidden", ("Korund", new[] { "korund" }), ("Slick", new[] { "slick" }));

        var worn = GearCheck.Row(row, Gear(worn: new[] { "slick" }));

        Assert.Null(worn.LeadTpl);
        Assert.Equal(new[] { "slick", "korund" }, worn.Alternatives.Select(a => a.Tpl));
        Assert.Null(GearCheck.Row(row, null).LeadTpl);
        Assert.Equal(new[] { "korund", "slick" }, GearCheck.Row(row, null).Alternatives.Select(a => a.Tpl));
    }

    [Fact]
    public void Build_row_carries_weapon_and_mod_tpls()
    {
        var row = new BriefingGearRow
        {
            Label = GearCheck.Build,
            Names = new List<string> { "Hybrid 46", "Hybrid 46", "PBS-1" },
            Tpls = new List<List<string>> { new List<string> { "hybrid" }, new List<string> { "hybrid" }, new List<string> { "pbs" } },
            Weapons = new List<BriefingItemRef> { new BriefingItemRef { Tpl = "ak105", Name = "AK-105" } },
        };

        var check = GearCheck.CheckBuild(row, Gear(stash: new[] { "pbs" }));

        Assert.Equal("ak105", check.Weapon.LeadTpl);
        Assert.Equal("pbs", check.Mods.LeadTpl);
        Assert.Equal(new[] { "pbs", "hybrid" }, check.Mods.Alternatives.Select(a => a.Tpl)); // 같은 부착물은 한 번만
        Assert.Equal("hybrid", GearCheck.CheckBuild(row, null).Mods.LeadTpl);
    }

    [Fact]
    public void Gear_lines_expose_the_lead_tpl()
    {
        var r = BriefingParser.Parse(@"{""schemaVersion"":1,""maps"":[{""key"":""a"",""name"":""A"",
            ""quests"":[{""questId"":""q"",""name"":""Quest""}],
            ""gear"":[{""questId"":""q"",""rows"":[
                {""label"":""weapon"",""names"":[""X"",""Y""],""tpls"":[[""x""],[""y""]]},
                {""label"":""build"",""names"":[""M""],""tpls"":[[""m""]],""weapons"":[{""tpl"":""w"",""name"":""W""}],""modParents"":[]}]}]}],""anywhere"":{}}").Response;

        var rules = GearTab.Build(r, r.Maps[0], null, Gear(new[] { new GearWeapon("w", null, null), new GearWeapon("y", null, null) }), id => id).Gear[0].Rules;

        Assert.Equal("y", rules[0].LeadTpl);
        Assert.Equal("w", rules[1].LeadTpl);  // build 의 무기 줄
        Assert.Equal("m", rules[2].LeadTpl);  // build 의 부착물 줄
        Assert.Null(rules[3].LeadTpl);        // ⚠ 이유 줄
    }
}
