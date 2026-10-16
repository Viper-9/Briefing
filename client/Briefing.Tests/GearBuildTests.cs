using System.Collections.Generic;
using System.Linq;
using Briefing.Core.Contract;
using Briefing.Core.Screen;

namespace Briefing.Tests;

// build 줄: 무기 조건과 부착물이 한 총에 같이 있어야 ✓. 아니면 ⚠ 이유(다른 총에 달림, 인정 안 되는 같은 종류 부품, 부착물 없음).
[Collection("Texts")]
public class GearBuildTests
{
    private const string SilencerParent = "550aa4cd4bdc2dd8348b456c";
    private const string ScopeParent = "55818ae44bdc2dde698b456c";

    // 취향 차이 - Part 1 과 같은 모양: AK-105 + 소음기 대안 둘
    private static BriefingGearRow Ak105Silenced(params string[] calibers) => new BriefingGearRow
    {
        Label = GearCheck.Build,
        Names = new List<string> { "Hybrid 46", "PBS-1" },
        Tpls = new List<List<string>> { new List<string> { "hybrid" }, new List<string> { "pbs" } },
        Weapons = calibers.Length > 0 ? new List<BriefingItemRef>() : new List<BriefingItemRef> { new BriefingItemRef { Tpl = "ak105", Name = "AK-105" } },
        Calibers = calibers.ToList(),
        ModParents = new List<string> { SilencerParent },
    };

    private static GearWeapon Weapon(string tpl, string caliber = "Caliber545x39", params GearPart[] parts) =>
        new GearWeapon(tpl, caliber, parts.Select(p => p.Tpl), parts);

    private static GearPart Part(string tpl, string parent, string name) => new GearPart(tpl, parent, name);

    private static GearSnapshot Gear(IEnumerable<GearWeapon> weapons, IEnumerable<string> stash = null, bool equipmentRead = true) => new GearSnapshot
    {
        EquipmentRead = equipmentRead,
        StashRead = true,
        Weapons = weapons.ToList(),
        Stash = new HashSet<string>(stash ?? Enumerable.Empty<string>()),
    };

    [Fact]
    public void Accepted_mod_on_the_required_weapon_is_equipped()
    {
        var check = GearCheck.CheckBuild(Ak105Silenced(), Gear(new[] { Weapon("ak105", parts: Part("pbs", SilencerParent, "PBS-1")) }));

        Assert.Equal(GearMark.Equipped, check.Weapon.Mark);
        Assert.Equal(GearMark.Equipped, check.Mods.Mark);
        Assert.Equal("PBS-1", check.Mods.Lead);
        Assert.Null(check.Reason);
    }

    [Fact]
    public void Accepted_mod_on_another_weapon_does_not_count()
    {
        var gear = Gear(new[] { Weapon("ak105"), Weapon("m4", "Caliber556x45NATO", Part("hybrid", SilencerParent, "Hybrid 46")) });
        var check = GearCheck.CheckBuild(Ak105Silenced(), gear);

        Assert.Equal(GearMark.Equipped, check.Weapon.Mark);
        Assert.Equal(GearMark.Missing, check.Mods.Mark);
        Assert.Equal("인정 부착물이 다른 총에 달려 있음", check.Reason);
    }

    [Fact]
    public void Unaccepted_part_of_the_same_kind_is_named()
    {
        var gear = Gear(new[] { Weapon("ak105", parts: new[] { Part("modsil", SilencerParent, "Mod Silencer"), Part("scope", ScopeParent, "Scope") }) });
        var check = GearCheck.CheckBuild(Ak105Silenced(), gear);

        Assert.Equal(GearMark.Missing, check.Mods.Mark);
        Assert.Equal("인정 안 되는 부착물: Mod Silencer", check.Reason);
    }

    [Fact]
    public void Required_weapon_without_any_accepted_mod_gets_a_plain_reason()
    {
        var check = GearCheck.CheckBuild(Ak105Silenced(), Gear(new[] { Weapon("ak105") }));

        Assert.Equal(GearMark.Missing, check.Mods.Mark);
        Assert.Equal("조건 무기에 인정 부착물 없음", check.Reason);
    }

    [Fact]
    public void Without_the_required_weapon_there_is_no_reason_and_stash_mods_give_half_mark()
    {
        var check = GearCheck.CheckBuild(Ak105Silenced(), Gear(new[] { Weapon("m4", "Caliber556x45NATO") }, stash: new[] { "ak105", "hybrid" }));

        Assert.Equal(GearMark.Stash, check.Weapon.Mark);
        Assert.Equal(GearMark.Stash, check.Mods.Mark);
        Assert.Equal("Hybrid 46", check.Mods.Lead);
        Assert.Null(check.Reason);
    }

    [Fact]
    public void Group_needs_every_part_on_the_same_weapon()
    {
        var row = Ak105Silenced();
        row.Names = new List<string> { "PBS-1 + PSO" };
        row.Tpls = new List<List<string>> { new List<string> { "pbs", "pso" } };
        var half = GearCheck.CheckBuild(row, Gear(new[] { Weapon("ak105", parts: Part("pbs", SilencerParent, "PBS-1")) }));
        var full = GearCheck.CheckBuild(row, Gear(new[] { Weapon("ak105", parts: new[] { Part("pbs", SilencerParent, "PBS-1"), Part("pso", ScopeParent, "PSO") }) }));

        Assert.Equal(GearMark.Missing, half.Mods.Mark);
        Assert.Equal("조건 무기에 인정 부착물 없음", half.Reason); // 달린 PBS-1 은 인정 목록에 있으니 이름을 대지 않는다
        Assert.Equal(GearMark.Equipped, full.Mods.Mark);
    }

    [Fact]
    public void Duplicate_alternatives_from_the_data_appear_once()
    {
        var row = Ak105Silenced();
        row.Names = new List<string> { "WAVE QD", "Hybrid 46", "WAVE QD" };
        row.Tpls = new List<List<string>> { new List<string> { "wave" }, new List<string> { "hybrid" }, new List<string> { "wave" } };
        var check = GearCheck.CheckBuild(row, Gear(new[] { Weapon("ak105") }));

        Assert.Equal(new[] { "WAVE QD", "Hybrid 46" }, check.Mods.Alternatives.Select(a => a.Name));
    }

    // 모든 탄환을 고려하기 - Part 1 과 같은 모양: 아무 총 + 10발 탄창 대안
    private static BriefingGearRow AnyWeaponMags() => new BriefingGearRow
    {
        Label = GearCheck.Build,
        Names = new List<string> { "10발 AK 7.62x39", "10발 Saiga 545" },
        Tpls = new List<List<string>> { new List<string> { "ak762mag" }, new List<string> { "saiga545" } },
        ModParents = new List<string> { MagazineParent },
    };

    private const string MagazineParent = "5448bc234bdc2d3c308b4569";

    [Fact]
    public void Any_weapon_build_is_quiet_on_a_weapon_that_cannot_take_accepted_parts()
    {
        var val = new GearWeapon("val", "Caliber9x39", new[] { "valmag" }, new[] { Part("valmag", MagazineParent, "VAL 20발") },
            mountable: new[] { "valmag", "valmag30" });
        var check = GearCheck.CheckBuild(AnyWeaponMags(), Gear(new[] { val }));

        Assert.Equal(GearMark.Missing, check.Mods.Mark);
        Assert.Null(check.Reason);
    }

    [Fact]
    public void Any_weapon_build_names_the_wrong_part_when_the_weapon_can_take_an_accepted_one()
    {
        var ak = new GearWeapon("ak105", "Caliber545x39", new[] { "6l23" }, new[] { Part("6l23", MagazineParent, "6L23") },
            mountable: new[] { "6l23", "saiga545" });
        var check = GearCheck.CheckBuild(AnyWeaponMags(), Gear(new[] { ak }));

        Assert.Equal("인정 안 되는 부착물: 6L23", check.Reason);
    }

    [Fact]
    public void Named_weapon_keeps_the_reason_even_if_no_slot_lists_the_part()
    {
        // Hybrid 46 처럼 어댑터를 거쳐야 달리는 부품은 맨총 슬롯 목록에 없다
        var ak = new GearWeapon("ak105", "Caliber545x39", new string[0], null, mountable: new[] { "6l23" });
        var check = GearCheck.CheckBuild(Ak105Silenced(), Gear(new[] { ak }));

        Assert.Equal("조건 무기에 인정 부착물 없음", check.Reason);
    }

    [Fact]
    public void No_weapon_condition_means_any_weapon()
    {
        var row = Ak105Silenced();
        row.Weapons = new List<BriefingItemRef>();
        var check = GearCheck.CheckBuild(row, Gear(new[] { Weapon("m4", "Caliber556x45NATO", Part("hybrid", SilencerParent, "Hybrid 46")) }));

        Assert.Null(check.Weapon);
        Assert.Equal(GearMark.Equipped, check.Mods.Mark);
    }

    [Fact]
    public void Caliber_condition_picks_the_weapon_by_caliber()
    {
        var gear = Gear(new[] { Weapon("m4", "Caliber556x45NATO", Part("hybrid", SilencerParent, "Hybrid 46")), Weapon("ak74") });
        var check = GearCheck.CheckBuild(Ak105Silenced("Caliber545x39"), gear);

        Assert.Equal(GearCheck.Caliber, check.WeaponLabel);
        Assert.Equal(GearMark.Equipped, check.Weapon.Mark);
        Assert.Equal(GearMark.Missing, check.Mods.Mark);
        Assert.Equal("인정 부착물이 다른 총에 달려 있음", check.Reason);
    }

    [Fact]
    public void Unread_equipment_marks_nothing()
    {
        var check = GearCheck.CheckBuild(Ak105Silenced(), Gear(new GearWeapon[0], equipmentRead: false));

        Assert.Equal(GearMark.Unknown, check.Weapon.Mark);
        Assert.Equal(GearMark.Unknown, check.Mods.Mark);
        Assert.Null(check.Reason);
    }

    [Fact]
    public void Gear_tab_spreads_a_build_row_into_weapon_mods_and_reason_lines()
    {
        var response = new BriefingResponse();
        response.Anywhere.Gear.Add(new BriefingGear { QuestId = "q", Rows = new List<BriefingGearRow> { Ak105Silenced() } });
        var tab = GearTab.Build(response, null, null, Gear(new[] { Weapon("ak105") }), id => "Quest");
        var lines = tab.Gear.Single().Rules;

        Assert.Equal(new[] { "무기", "필수 부착물", "확인" }, lines.Select(l => l.Label));
        Assert.Equal(new[] { "q/0/w", "q/0", "q/0/r" }, lines.Select(l => l.Key));
        Assert.Equal(new[] { GearMark.Equipped, GearMark.Missing, GearMark.Missing }, lines.Select(l => l.Mark));
        Assert.True(lines[2].Warn);
        Assert.Null(lines[2].More);
        Assert.Equal(GearMark.Missing, tab.Gear.Single().Verdict);
    }

    [Fact]
    public void Build_rows_with_different_weapons_are_not_merged()
    {
        var other = Ak105Silenced();
        other.Weapons = new List<BriefingItemRef> { new BriefingItemRef { Tpl = "ak74", Name = "AK-74" } };
        var response = new BriefingResponse();
        response.Anywhere.Gear.Add(new BriefingGear { QuestId = "q", Rows = new List<BriefingGearRow> { Ak105Silenced(), other, Ak105Silenced() } });
        var tab = GearTab.Build(response, null, null, Gear(new GearWeapon[0]), id => "Quest");

        Assert.Equal(4, tab.Gear.Single().Rules.Count); // 두 build 줄 x (무기 + 부착물), 이유 줄 없음
    }
}
