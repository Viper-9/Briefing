using System.Collections.Generic;
using System.Linq;
using Briefing.Core.Contract;
using Briefing.Core.Screen;

namespace Briefing.Tests;

// 장착 판정: 조건 종류별 ✓ / ◐ / ⚠, 대안 안의 tpl 묶음, 구경 all, 착용 금지, 대표 이름과 대안 순서, 종합 판정, 못 읽었을 때.
[Collection("Texts")]
public class GearCheckTests
{
    private static BriefingGearRow Row(string label, params (string Name, string[] Tpls)[] alts) => new BriefingGearRow
    {
        Label = label,
        Names = alts.Select(a => a.Name).ToList(),
        Tpls = alts.Select(a => a.Tpls.ToList()).ToList(),
    };

    private static GearSnapshot Gear(IEnumerable<GearWeapon> weapons = null, IEnumerable<string> worn = null,
        IEnumerable<string> stash = null, IEnumerable<string> stashCalibers = null, bool stashRead = true) => new GearSnapshot
    {
        EquipmentRead = true,
        StashRead = stashRead,
        Weapons = (weapons ?? Enumerable.Empty<GearWeapon>()).ToList(),
        Worn = new HashSet<string>(worn ?? Enumerable.Empty<string>()),
        Stash = new HashSet<string>(stash ?? Enumerable.Empty<string>()),
        StashCalibers = new HashSet<string>(stashCalibers ?? Enumerable.Empty<string>()),
    };

    private static readonly BriefingGearRow Smg = Row("weapon", ("MP5", new[] { "mp5" }), ("MP9", new[] { "mp9" }), ("Vector", new[] { "vec" }));

    [Fact]
    public void Equipped_weapon_is_the_lead_and_comes_first()
    {
        var check = GearCheck.Row(Smg, Gear(new[] { new GearWeapon("mp9", "Caliber9x19PARA", null) }));

        Assert.Equal(GearMark.Equipped, check.Mark);
        Assert.Equal("MP9", check.Lead);
        Assert.Equal("장착 중", check.Tag);
        Assert.Equal(new[] { "MP9", "MP5", "Vector" }, check.Alternatives.Select(a => a.Name));
        Assert.Equal(new[] { GearMark.Equipped, GearMark.Unknown, GearMark.Unknown }, check.Alternatives.Select(a => a.Mark));
    }

    [Fact]
    public void Stash_weapon_gives_half_mark()
    {
        var check = GearCheck.Row(Smg, Gear(stash: new[] { "vec", "mp5" }));

        Assert.Equal(GearMark.Stash, check.Mark);
        Assert.Equal("MP5", check.Lead); // 보관함에 있는 첫 대안(계약 순서)
        Assert.Equal("보관함", check.Tag);
        Assert.Equal(new[] { "MP5", "Vector", "MP9" }, check.Alternatives.Select(a => a.Name));
        Assert.Equal(new[] { GearMark.Stash, GearMark.Stash, GearMark.Unknown }, check.Alternatives.Select(a => a.Mark));
    }

    [Fact]
    public void Unread_stash_never_gives_half_mark()
    {
        var check = GearCheck.Row(Smg, Gear(stash: new[] { "mp5" }, stashRead: false));

        Assert.Equal(GearMark.Missing, check.Mark);
        Assert.Equal("MP5", check.Lead); // 첫 대안
        Assert.Null(check.Tag);
        Assert.All(check.Alternatives, a => Assert.Equal(GearMark.Unknown, a.Mark));
    }

    [Fact]
    public void Caliber_matches_equipped_weapon_code_and_all_is_always_met()
    {
        var row = Row("caliber", ("Caliber556x45NATO", new string[0]), ("Caliber762x39", new string[0]));
        var ak = new GearWeapon("ak", "Caliber762x39", null);

        var equipped = GearCheck.Row(row, Gear(new[] { ak }));
        Assert.Equal(GearMark.Equipped, equipped.Mark);
        Assert.Equal("762x39", equipped.Lead); // Caliber 접두어를 뗀 이름
        Assert.Equal(new[] { "762x39", "556x45NATO" }, equipped.Alternatives.Select(a => a.Name));

        var stash = GearCheck.Row(row, Gear(stashCalibers: new[] { "Caliber556x45NATO" }));
        Assert.Equal(GearMark.Stash, stash.Mark);
        Assert.Equal("556x45NATO", stash.Lead);

        var any = GearCheck.Row(Row("caliber", ("all", new string[0])), Gear());
        Assert.Equal(GearMark.Equipped, any.Mark);
        Assert.Equal("모든 구경", any.Lead);
    }

    [Fact]
    public void Weapon_mods_must_all_be_on_one_equipped_weapon()
    {
        var row = Row("weaponMod", ("Salvo + mount", new[] { "salvo", "mount" }), ("Hexagon", new[] { "hex" }));
        var split = Gear(new[] { new GearWeapon("a", null, new[] { "salvo" }), new GearWeapon("b", null, new[] { "mount" }) });
        var one = Gear(new[] { new GearWeapon("a", null, new[] { "x", "mount", "salvo" }) });

        Assert.Equal(GearMark.Missing, GearCheck.Row(row, split).Mark);
        var check = GearCheck.Row(row, one);
        Assert.Equal(GearMark.Equipped, check.Mark);
        Assert.Equal("Salvo + mount", check.Lead);
        Assert.Equal(GearMark.Stash, GearCheck.Row(row, Gear(stash: new[] { "salvo", "mount" })).Mark); // 둘 다 보관함에 있어야
        Assert.Equal(GearMark.Missing, GearCheck.Row(row, Gear(stash: new[] { "salvo" })).Mark);
    }

    [Fact]
    public void Wear_needs_every_item_of_one_alternative()
    {
        var row = Row("equipment", ("Helmet + visor", new[] { "helm", "visor" }), ("Cap", new[] { "cap" }));

        Assert.Equal(GearMark.Missing, GearCheck.Row(row, Gear(worn: new[] { "helm" })).Mark);
        var both = GearCheck.Row(row, Gear(worn: new[] { "helm", "visor", "vest" }));
        Assert.Equal(GearMark.Equipped, both.Mark);
        Assert.Equal("Helmet + visor", both.Lead);
        Assert.Equal(GearMark.Equipped, GearCheck.Row(row, Gear(worn: new[] { "cap" })).Mark);
        Assert.Equal("Cap", GearCheck.Row(row, Gear(worn: new[] { "cap" })).Lead);
    }

    [Fact]
    public void Empty_and_group_is_never_met()
    {
        var row = Row("equipment", ("Broken", new string[0]));

        Assert.Equal(GearMark.Missing, GearCheck.Row(row, Gear(worn: new[] { "x" })).Mark);
    }

    [Fact]
    public void Forbidden_is_met_when_nothing_listed_is_worn()
    {
        var row = Row("forbidden", ("Gzhel", new[] { "gzhel" }), ("Korund", new[] { "korund" }), ("Slick", new[] { "slick" }));

        var clear = GearCheck.Row(row, Gear(worn: new[] { "helm" }, stash: new[] { "gzhel" }));
        Assert.Equal(GearMark.Equipped, clear.Mark);
        Assert.Equal("해당 장비 착용 안 함", clear.Lead);
        Assert.Null(clear.Tag);
        Assert.All(clear.Alternatives, a => Assert.Equal(GearMark.Unknown, a.Mark));

        var worn = GearCheck.Row(row, Gear(worn: new[] { "slick", "korund" }));
        Assert.Equal(GearMark.Missing, worn.Mark);
        Assert.Equal("착용 중: Korund", worn.Lead); // 목록 순서로 첫 착용 아이템
        Assert.Equal(new[] { "Korund", "Slick", "Gzhel" }, worn.Alternatives.Select(a => a.Name));
        Assert.Equal(new[] { GearMark.Missing, GearMark.Missing, GearMark.Unknown }, worn.Alternatives.Select(a => a.Mark));
    }

    [Fact]
    public void Unread_equipment_gives_no_marks()
    {
        var unread = new GearSnapshot { EquipmentRead = false, StashRead = true, Stash = new HashSet<string> { "mp9" } };

        foreach (var gear in new[] { unread, null })
        {
            var check = GearCheck.Row(Smg, gear);
            Assert.Equal(GearMark.Unknown, check.Mark);
            Assert.Equal("MP5", check.Lead);
            Assert.Null(check.Tag);
            Assert.Equal(new[] { "MP5", "MP9", "Vector" }, check.Alternatives.Select(a => a.Name));
            var forbidden = GearCheck.Row(Row("forbidden", ("Gzhel", new[] { "gzhel" })), gear);
            Assert.Equal(GearMark.Unknown, forbidden.Mark);
            Assert.Equal("Gzhel", forbidden.Lead);
        }
    }

    [Fact]
    public void Unknown_label_is_shown_without_a_mark()
    {
        var check = GearCheck.Row(Row("skill", ("Something", new[] { "x" })), Gear(worn: new[] { "x" }));

        Assert.Equal(GearMark.Unknown, check.Mark);
        Assert.Equal("Something", check.Lead);
    }

    [Fact]
    public void Overall_verdict()
    {
        Assert.Equal(GearMark.Equipped, GearCheck.Overall(new[] { GearMark.Equipped, GearMark.Equipped }));
        Assert.Equal(GearMark.Stash, GearCheck.Overall(new[] { GearMark.Equipped, GearMark.Stash }));
        Assert.Equal(GearMark.Missing, GearCheck.Overall(new[] { GearMark.Stash, GearMark.Missing, GearMark.Equipped }));
        Assert.Equal(GearMark.Equipped, GearCheck.Overall(new[] { GearMark.Equipped, GearMark.Unknown }));
        Assert.Equal(GearMark.Unknown, GearCheck.Overall(new[] { GearMark.Unknown }));
        Assert.Equal(GearMark.Unknown, GearCheck.Overall(new GearMark[0]));
    }

    [Fact]
    public void Display_names()
    {
        Assert.Equal("9x19PARA", GearCheck.DisplayName("caliber", "Caliber9x19PARA"));
        Assert.Equal("12g", GearCheck.DisplayName("caliber", "12g"));
        Assert.Equal("모든 구경", GearCheck.DisplayName("caliber", "all"));
        Assert.Equal("Mosin", GearCheck.DisplayName("weapon", "Mosin "));
        Assert.Equal("Caliber9x19PARA", GearCheck.DisplayName("weapon", "Caliber9x19PARA"));
    }
}
