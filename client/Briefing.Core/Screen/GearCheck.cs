using System;
using System.Collections.Generic;
using System.Linq;
using Briefing.Core.Contract;

namespace Briefing.Core.Screen;

// Equipped ✓ 장착 중, Stash ◐ 보관함에 있음, Missing ⚠ 없음. Unknown 은 장착을 못 읽었거나 모르는 라벨이라 아무것도 표시하지 않는다.
public enum GearMark
{
    Unknown,
    Equipped,
    Stash,
    Missing,
}

// 장착 무기. Caliber 는 템플릿 ammoCaliber 값(예: Caliber556x45NATO), Parts 는 슬롯에 달린 부착물 tpl 전부(무기 자신은 빼고).
// Details 는 같은 부착물의 부모 id 와 이름으로, build 줄의 ⚠ 이유에만 쓴다. 없으면 빈 목록.
// Mountable 은 무기와 달린 부품의 슬롯 필터에 적힌 tpl 전부다. 못 읽었으면 null 이고, 그때는 달 수 있다고 본다.
public sealed class GearWeapon
{
    public GearWeapon(string tpl, string caliber, IEnumerable<string> parts, IEnumerable<GearPart> details = null, IEnumerable<string> mountable = null)
    {
        Tpl = tpl;
        Caliber = caliber;
        Parts = new HashSet<string>(parts ?? Enumerable.Empty<string>());
        Details = (details ?? Enumerable.Empty<GearPart>()).ToList();
        Mountable = mountable == null ? null : new HashSet<string>(mountable);
    }

    public string Tpl { get; }
    public string Caliber { get; }
    public HashSet<string> Parts { get; }
    public IReadOnlyList<GearPart> Details { get; }
    public HashSet<string> Mountable { get; }
}

public sealed class GearPart
{
    public GearPart(string tpl, string parentId, string name)
    {
        Tpl = tpl;
        ParentId = parentId;
        Name = name;
    }

    public string Tpl { get; }
    public string ParentId { get; }
    public string Name { get; }
}

// 게임에서 읽은 장비. Client 의 GearReader 가 채운다.
// EquipmentRead 가 거짓이면 판정하지 않고, StashRead 가 거짓이면(레이드 중이거나 못 읽음) ◐ 를 쓰지 않는다.
// Worn 은 장비 슬롯 아이템과 거기 붙은 부품이다. 가방·조끼 안 내용물, 보안 컨테이너, 주머니, 인식표는 넣지 않는다.
// Stash 는 보관함 안 모든 아이템 tpl(무기 부품과 컨테이너 속까지), StashCalibers 는 보관함 무기의 구경 코드.
public sealed class GearSnapshot
{
    public bool EquipmentRead { get; set; }
    public bool StashRead { get; set; }
    public List<GearWeapon> Weapons { get; set; } = new List<GearWeapon>();
    public HashSet<string> Worn { get; set; } = new HashSet<string>();
    public HashSet<string> Stash { get; set; } = new HashSet<string>();
    public HashSet<string> StashCalibers { get; set; } = new HashSet<string>();
}

// "외 N종" 상자의 한 줄. 맞는 대안은 Equipped·Stash, 착용 금지 목록에서 지금 입은 것은 Missing, 나머지는 Unknown.
public sealed class GearAlt
{
    public GearAlt(string name, GearMark mark)
    {
        Name = name;
        Mark = mark;
    }

    public string Name { get; }
    public GearMark Mark { get; }
}

// Lead 는 줄에 보일 이름이다. ✓·◐ 면 맞는 첫 대안, ⚠ 면 첫 대안, 착용 금지는 판정 문구.
// Alternatives 는 상자에 넣을 전체 목록이고 맞는 것이 먼저 온다.
public sealed class GearRowCheck
{
    public GearRowCheck(GearMark mark, string lead, string tag, IReadOnlyList<GearAlt> alternatives)
    {
        Mark = mark;
        Lead = lead;
        Tag = tag;
        Alternatives = alternatives;
    }

    public GearMark Mark { get; }
    public string Lead { get; }
    public string Tag { get; }
    public IReadOnlyList<GearAlt> Alternatives { get; }
}

// build 줄의 판정. Weapon 은 무기 조건이 없으면 null 이고 WeaponLabel 은 weapon 이나 caliber.
// Reason 은 조건 무기를 장착했는데 부착물이 ✓ 가 아닐 때만 있다.
public sealed class GearBuildCheck
{
    public GearBuildCheck(string weaponLabel, GearRowCheck weapon, GearRowCheck mods, string reason)
    {
        WeaponLabel = weaponLabel;
        Weapon = weapon;
        Mods = mods;
        Reason = reason;
    }

    public string WeaponLabel { get; }
    public GearRowCheck Weapon { get; }
    public GearRowCheck Mods { get; }
    public string Reason { get; }
}

// 줄마다 따로 판정한다. 서버가 퀘스트 단위로 줄을 합쳐 보내 목표 경계를 알 수 없으므로 근사치다.
// 무기와 부착물을 한 총에서 봐야 하는 조건은 서버가 build 줄로 묶어 보내고 CheckBuild 가 판정한다.
public static class GearCheck
{
    public const string Weapon = "weapon";
    public const string Caliber = "caliber";
    public const string WeaponMod = "weaponMod";
    public const string Equipment = "equipment";
    public const string Forbidden = "forbidden";
    public const string Build = "build";
    public const string AnyCaliber = "all";

    public static GearRowCheck Row(BriefingGearRow row, GearSnapshot gear)
    {
        var names = row.Names.Select(n => DisplayName(row.Label, n)).ToList();
        if (row.Label == Forbidden) return ForbiddenRow(row, names, gear);
        var known = gear != null && gear.EquipmentRead && IsKnownLabel(row.Label);
        if (!known) return Unmarked(names);

        var marks = new List<GearMark>();
        for (var i = 0; i < names.Count; i++) marks.Add(AltMark(row, i, gear));
        return Assemble(names, marks);
    }

    // build 줄. 부착물은 무기 조건에 맞는 장착 무기에 달려 있어야 ✓ 이고, 보관함은 총과 따로 있어도 ◐ 로 본다.
    // 조건 무기를 장착했는데 ✓ 가 아니면 Reason 에 이유를 넣는다.
    public static GearBuildCheck CheckBuild(BriefingGearRow row, GearSnapshot gear)
    {
        var weaponRow = WeaponRow(row);
        var weapon = weaponRow == null ? null : Row(weaponRow, gear);
        row = DistinctMods(row);
        var names = row.Names.Select(n => n?.Trim() ?? "").ToList();
        if (gear == null || !gear.EquipmentRead) return new GearBuildCheck(weaponRow?.Label, weapon, Unmarked(names), null);

        var fits = gear.Weapons.Where(w => Fits(row, w)).ToList();
        var marks = new List<GearMark>();
        for (var i = 0; i < names.Count; i++)
        {
            var tpls = i < row.Tpls.Count ? row.Tpls[i] : null;
            if (tpls == null || tpls.Count == 0) marks.Add(GearMark.Missing);
            else if (fits.Any(w => tpls.All(w.Parts.Contains))) marks.Add(GearMark.Equipped);
            else marks.Add(gear.StashRead && tpls.All(gear.Stash.Contains) ? GearMark.Stash : GearMark.Missing);
        }
        var mods = Assemble(names, marks);
        return new GearBuildCheck(weaponRow?.Label, weapon, mods, mods.Mark == GearMark.Equipped ? null : BuildReason(row, gear, fits));
    }

    // 원본 데이터에 같은 부착물이 두 번 들어 있는 경우가 있다(Daniel Defense WAVE QD 등). 이름과 tpl 묶음이 같으면 처음 것만 둔다.
    private static BriefingGearRow DistinctMods(BriefingGearRow row)
    {
        var seen = new HashSet<string>();
        var names = new List<string>();
        var tpls = new List<List<string>>();
        for (var i = 0; i < row.Names.Count; i++)
        {
            var group = i < row.Tpls.Count ? row.Tpls[i] : null;
            if (!seen.Add(row.Names[i] + "\u0000" + string.Join("\u0001", group ?? new List<string>()))) continue;
            names.Add(row.Names[i]);
            tpls.Add(group);
        }
        if (names.Count == row.Names.Count) return row;
        return new BriefingGearRow
        {
            Label = row.Label, Names = names, Tpls = tpls,
            Weapons = row.Weapons, Calibers = row.Calibers, ModParents = row.ModParents,
        };
    }

    // 무기 조건을 weapon 이나 caliber 줄로 바꿔 Row 판정을 그대로 쓴다. 조건이 없으면 null.
    private static BriefingGearRow WeaponRow(BriefingGearRow row)
    {
        if (row.Weapons.Count > 0)
            return new BriefingGearRow
            {
                Label = Weapon,
                Names = row.Weapons.Select(w => w.Name).ToList(),
                Tpls = row.Weapons.Select(w => new List<string> { w.Tpl }).ToList(),
            };
        if (row.Calibers.Count > 0)
            return new BriefingGearRow { Label = Caliber, Names = row.Calibers.ToList(), Tpls = row.Calibers.Select(_ => new List<string>()).ToList() };
        return null;
    }

    private static bool AnyWeapon(BriefingGearRow row)
        => row.Weapons.Count == 0 && row.Calibers.All(c => c?.Trim() == AnyCaliber);

    private static bool Fits(BriefingGearRow row, GearWeapon weapon)
        => (row.Weapons.Count == 0 || row.Weapons.Any(w => w.Tpl == weapon.Tpl))
           && (row.Calibers.Count == 0 || row.Calibers.Any(c => c?.Trim() == AnyCaliber || c?.Trim() == weapon.Caliber));

    // 다른 총에 인정 부착물이 달림 > 조건 무기에 인정 안 되는 같은 종류 부품이 달림 > 그냥 없음 순으로 본다.
    private static string BuildReason(BriefingGearRow row, GearSnapshot gear, List<GearWeapon> fits)
    {
        var groups = row.Tpls.Where(g => g != null && g.Count > 0).ToList();
        var accepted = new HashSet<string>(groups.SelectMany(g => g));
        // 아무 총이면 장착한 총이 인정 부품을 아예 못 받을 수 있다(9x39 총에 AK 탄창). 받을 수 있는 총만 본다.
        // 무기나 구경을 정한 조건은 어댑터를 거쳐야 달리는 부품도 있어서 슬롯 목록으로 거르지 않는다.
        var candidates = AnyWeapon(row) ? fits.Where(w => w.Mountable == null || accepted.Any(w.Mountable.Contains)).ToList() : fits;
        if (candidates.Count == 0) return null;
        if (gear.Weapons.Except(fits).Any(w => groups.Any(g => g.All(w.Parts.Contains)))) return Texts.BuildOnOtherWeapon;
        var wrong = candidates.SelectMany(w => w.Details)
            .Where(p => row.ModParents.Contains(p.ParentId) && !accepted.Contains(p.Tpl) && !string.IsNullOrWhiteSpace(p.Name))
            .Select(p => p.Name).Distinct().ToList();
        return wrong.Count > 0 ? Texts.BuildNotAccepted(string.Join(", ", wrong)) : Texts.BuildNoMods;
    }

    private static GearRowCheck Unmarked(List<string> names)
        => new GearRowCheck(GearMark.Unknown, names.FirstOrDefault() ?? "", null, names.Select(n => new GearAlt(n, GearMark.Unknown)).ToList());

    // 대안별 판정을 줄 판정으로 묶는다. 맞는 대안이 먼저 오고, 대표 이름은 ✓·◐ 면 맞는 첫 대안, ⚠ 면 첫 대안.
    private static GearRowCheck Assemble(List<string> names, List<GearMark> marks)
    {
        var mark = marks.Contains(GearMark.Equipped) ? GearMark.Equipped : marks.Contains(GearMark.Stash) ? GearMark.Stash : GearMark.Missing;
        var leadIndex = mark == GearMark.Missing ? 0 : marks.IndexOf(mark);
        var alts = new List<GearAlt>();
        for (var i = 0; i < names.Count; i++)
            if (marks[i] != GearMark.Missing) alts.Add(new GearAlt(names[i], marks[i]));
        for (var i = 0; i < names.Count; i++)
            if (marks[i] == GearMark.Missing) alts.Add(new GearAlt(names[i], GearMark.Unknown));
        return new GearRowCheck(mark, names.Count > 0 ? names[leadIndex] : "", Tag(mark), alts);
    }

    // ⚠ 가 하나라도 있으면 ⚠, 없고 ◐ 가 있으면 ◐, 나머지는 ✓. Unknown 줄은 빼고 보며 남는 게 없으면 Unknown.
    public static GearMark Overall(IEnumerable<GearMark> marks)
    {
        var known = marks.Where(m => m != GearMark.Unknown).ToList();
        if (known.Count == 0) return GearMark.Unknown;
        if (known.Contains(GearMark.Missing)) return GearMark.Missing;
        return known.Contains(GearMark.Stash) ? GearMark.Stash : GearMark.Equipped;
    }

    // 구경은 코드에서 Caliber 접두어를 떼고, all 은 "모든 구경"으로. 다른 라벨은 공백만 다듬는다.
    public static string DisplayName(string label, string name)
    {
        var n = name?.Trim() ?? "";
        if (label != Caliber) return n;
        if (n == AnyCaliber) return Texts.AnyCaliber;
        return n.StartsWith("Caliber", StringComparison.Ordinal) ? n.Substring("Caliber".Length) : n;
    }

    private static bool IsKnownLabel(string label) => label == Weapon || label == Caliber || label == WeaponMod || label == Equipment;

    private static GearMark AltMark(BriefingGearRow row, int i, GearSnapshot gear)
    {
        if (row.Label == Caliber)
        {
            var code = row.Names[i]?.Trim();
            if (code == AnyCaliber || gear.Weapons.Any(w => w.Caliber == code)) return GearMark.Equipped;
            return gear.StashRead && gear.StashCalibers.Contains(code) ? GearMark.Stash : GearMark.Missing;
        }
        var tpls = i < row.Tpls.Count ? row.Tpls[i] : null;
        if (tpls == null || tpls.Count == 0) return GearMark.Missing; // 비어 있으면 맞춘 것으로 보지 않는다
        bool equipped;
        switch (row.Label)
        {
            case Weapon:
                equipped = tpls.All(t => gear.Weapons.Any(w => w.Tpl == t));
                break;
            case WeaponMod:
                equipped = gear.Weapons.Any(w => tpls.All(w.Parts.Contains)); // 한 무기에 다 달려 있어야 한다
                break;
            default:
                equipped = tpls.All(gear.Worn.Contains);
                break;
        }
        if (equipped) return GearMark.Equipped;
        return gear.StashRead && tpls.All(gear.Stash.Contains) ? GearMark.Stash : GearMark.Missing;
    }

    // 착용 금지 목록 중 입은 게 없으면 ✓, 있으면 ⚠ 와 처음 찾은 아이템 이름. 보관함은 보지 않고 태그도 없다.
    private static GearRowCheck ForbiddenRow(BriefingGearRow row, List<string> names, GearSnapshot gear)
    {
        if (gear == null || !gear.EquipmentRead)
            return new GearRowCheck(GearMark.Unknown, names.FirstOrDefault() ?? "", null,
                names.Select(n => new GearAlt(n, GearMark.Unknown)).ToList());
        var worn = new List<int>();
        for (var i = 0; i < names.Count; i++)
        {
            var tpls = i < row.Tpls.Count ? row.Tpls[i] : null;
            if (tpls != null && tpls.Any(gear.Worn.Contains)) worn.Add(i);
        }
        var alts = worn.Select(i => new GearAlt(names[i], GearMark.Missing))
            .Concat(Enumerable.Range(0, names.Count).Where(i => !worn.Contains(i)).Select(i => new GearAlt(names[i], GearMark.Unknown)))
            .ToList();
        return worn.Count == 0
            ? new GearRowCheck(GearMark.Equipped, Texts.ForbiddenClear, null, alts)
            : new GearRowCheck(GearMark.Missing, Texts.WearingNow(names[worn[0]]), null, alts);
    }

    private static string Tag(GearMark mark) =>
        mark == GearMark.Equipped ? Texts.TagEquipped : mark == GearMark.Stash ? Texts.TagStash : null;
}
