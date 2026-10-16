using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Briefing.Core.Screen;
using EFT;
using EFT.InventoryLogic;
using EFT.UI;

namespace Briefing.Game;

// 장착은 그릴 때마다 읽고, 보관함은 아이템이 수천 개라 탭이 켜질 때 한 번만 훑어 둔다.
// 레이드 중에는 보관함에서 꺼낼 수 없으니 읽지 않는다. 실패하면 그 부분만 못 읽은 것으로 둔다.
// 부품만 볼 때는 Item.TraverseTree 에서 슬롯만 따라가서 가방·조끼 안 내용물을 뺀다.
internal sealed class GearReader
{
    internal static readonly EquipmentSlot[] WeaponSlots =
        { EquipmentSlot.FirstPrimaryWeapon, EquipmentSlot.SecondPrimaryWeapon, EquipmentSlot.Holster };
    // 착용으로 보지 않는 슬롯
    private static readonly EquipmentSlot[] NotWorn = { EquipmentSlot.SecuredContainer, EquipmentSlot.Pockets, EquipmentSlot.Dogtag };

    private readonly Func<Profile> _profile;
    private HashSet<string> _stash;          // 못 읽었거나 레이드 중이면 null
    private HashSet<string> _stashCalibers;
    private bool _equipmentWarned;
    private bool _loadoutWarned; // 장착 읽기 실패 로그에 가려지지 않게 따로 둔다
    private bool _stashWarned;
    private bool _stashLogged;
    private bool _stashRaidLogged;

    internal GearReader(Func<Profile> profile) => _profile = profile;

    // 보관함 안 모든 아이템(무기 부품과 컨테이너 속까지)의 tpl 과 보관함 무기의 구경을 모은다.
    internal void ScanStash()
    {
        _stash = null;
        _stashCalibers = null;
        try
        {
            if (TasksPanel.InRaid())
            {
                var inventory = _profile()?.Inventory;
                if (!_stashRaidLogged)
                {
                    _stashRaidLogged = true;
                    Log.Info($"gear stash: in raid, not read (Stash {(inventory?.Stash != null ? "present" : "null")})");
                }
                return;
            }
            var stash = _profile()?.Inventory?.Stash;
            if (stash == null) throw new InvalidOperationException("no stash");
            var clock = Stopwatch.StartNew();
            var items = new List<Item>();
            Item.TraverseTree(stash, items, (item, container) => true);
            var tpls = new HashSet<string>();
            var calibers = new HashSet<string>();
            foreach (var item in items)
            {
                if (item == null || item == stash) continue;
                tpls.Add(item.StringTemplateId);
                var caliber = CaliberOf(item);
                if (caliber != null) calibers.Add(caliber);
            }
            _stash = tpls;
            _stashCalibers = calibers;
            if (!_stashLogged)
            {
                _stashLogged = true;
                Log.Info($"gear stash: {items.Count} items, {tpls.Count} templates, {calibers.Count} weapon calibers in {clock.ElapsedMilliseconds} ms");
            }
        }
        catch (Exception e)
        {
            if (!_stashWarned) Log.Warn($"gear stash unreadable: {e.Message}");
            _stashWarned = true;
        }
    }

    // 장착 무기(주무기 1·2, 권총)와 착용 tpl. 못 읽으면 null.
    internal EquippedGear ReadEquipment()
    {
        try
        {
            var equipment = _profile()?.Inventory?.Equipment;
            if (equipment == null) throw new InvalidOperationException("no equipment");
            var weapons = new List<EquippedWeapon>();
            foreach (var slot in WeaponSlots)
            {
                var item = equipment.GetSlot(slot)?.ContainedItem;
                if (item == null) continue;
                var parts = new List<Item>();
                Item.TraverseTree(item, parts, (i, container) => container is Slot);
                var mods = parts.Where(p => p != null && p != item).ToList();
                weapons.Add(new EquippedWeapon(slot, item.StringTemplateId, CaliberOf(item),
                    new HashSet<string>(mods.Select(p => p.StringTemplateId)), mods.Select(PartOf).ToList(), MountableOf(item)));
            }
            var worn = new HashSet<string>();
            foreach (EquipmentSlot slot in Enum.GetValues(typeof(EquipmentSlot)))
            {
                if (NotWorn.Contains(slot)) continue;
                var item = equipment.GetSlot(slot)?.ContainedItem;
                if (item == null) continue;
                var parts = new List<Item>();
                Item.TraverseTree(item, parts, (i, container) => container is Slot); // 방탄판·부착물까지, 격자 안 내용물은 빼고
                foreach (var p in parts) if (p != null) worn.Add(p.StringTemplateId);
            }
            return new EquippedGear(weapons, worn);
        }
        catch (Exception e)
        {
            if (!_equipmentWarned) Log.Warn($"gear equipment unreadable: {e.Message}");
            _equipmentWarned = true;
            return null;
        }
    }

    // GearCheck 에 넘길 값. 장착은 지금 읽고 보관함은 마지막 ScanStash 결과를 쓴다.
    internal GearSnapshot Snapshot()
    {
        var gear = ReadEquipment();
        var snapshot = new GearSnapshot { EquipmentRead = gear != null, StashRead = _stash != null };
        if (gear != null)
        {
            snapshot.Weapons = gear.Weapons.Select(w => new GearWeapon(w.Tpl, w.Caliber, w.Parts, w.Details, w.Mountable)).ToList();
            snapshot.Worn = gear.Worn;
        }
        if (_stash != null)
        {
            snapshot.Stash = _stash;
            snapshot.StashCalibers = _stashCalibers;
        }
        return snapshot;
    }

    // 장비 탭 맨 위 줄: 주무기 1, 주무기 2, 권총, 헬멧, 방탄복(없으면 조끼). 못 읽으면 null.
    internal IReadOnlyList<LoadoutSlot> Loadout()
    {
        try
        {
            var equipment = _profile()?.Inventory?.Equipment;
            if (equipment == null) return null;
            Item In(EquipmentSlot slot) => equipment.GetSlot(slot)?.ContainedItem;
            var armor = In(EquipmentSlot.ArmorVest);
            var rig = armor == null ? In(EquipmentSlot.TacticalVest) : null;
            return new[]
            {
                new LoadoutSlot(Texts.SlotPrimary1, In(EquipmentSlot.FirstPrimaryWeapon)),
                new LoadoutSlot(Texts.SlotPrimary2, In(EquipmentSlot.SecondPrimaryWeapon)),
                new LoadoutSlot(Texts.SlotHolster, In(EquipmentSlot.Holster)),
                new LoadoutSlot(Texts.SlotHelmet, In(EquipmentSlot.Headwear)),
                rig != null ? new LoadoutSlot(Texts.SlotRig, rig) : new LoadoutSlot(Texts.SlotArmor, armor),
            };
        }
        catch (Exception e)
        {
            if (!_loadoutWarned) Log.Warn($"gear loadout unreadable: {e.Message}");
            _loadoutWarned = true;
            return null;
        }
    }

    // build 줄의 ⚠ 이유에 쓰는 부품 정보. 이름은 게임 로케일의 짧은 이름.
    private static GearPart PartOf(Item item)
        => new GearPart(item.StringTemplateId, item.Template?.ParentId?.ToString(), ItemIcons.ShortName(item.StringTemplateId, item.Template?.Name));

    // 무기와 달린 부품의 슬롯 필터에 적힌 id 전부. Weapon.AllSlots 가 달린 부품 안의 슬롯까지 펼쳐 준다.
    // 필터에는 부모 분류 id 가 섞일 수 있지만 무기 부품 슬롯은 대부분 tpl 목록이라 그대로 비교한다. 못 읽으면 null.
    private static List<string> MountableOf(Item item)
    {
        try
        {
            if (!(item is CompoundItem compound)) return null;
            var ids = new List<string>();
            foreach (var slot in compound.AllSlots)
            {
                if (slot?.Filters == null) continue;
                foreach (var filter in slot.Filters)
                {
                    if (filter?.Filter == null) continue;
                    foreach (var id in filter.Filter) ids.Add(id.ToString());
                }
            }
            return ids;
        }
        catch (Exception)
        {
            return null;
        }
    }

    // 서버 응답과 같은 모양(Caliber556x45NATO)이 필요해서 접두어를 떼는 Weapon.AmmoCaliber 대신 템플릿 값을 쓴다.
    private static string CaliberOf(Item item) => item is Weapon weapon ? (weapon.Template as WeaponTemplate)?.ammoCaliber : null;
}

internal sealed class EquippedWeapon
{
    internal EquippedWeapon(EquipmentSlot slot, string tpl, string caliber, HashSet<string> parts, List<GearPart> details, List<string> mountable)
    {
        Mountable = mountable;
        Slot = slot;
        Tpl = tpl;
        Caliber = caliber;
        Parts = parts;
        Details = details;
    }

    internal EquipmentSlot Slot { get; }
    internal string Tpl { get; }
    internal string Caliber { get; }
    internal HashSet<string> Parts { get; }
    internal List<GearPart> Details { get; }
    internal List<string> Mountable { get; }
}

// 아이콘은 실제 장착 아이템으로 그려서 무기는 지금 달린 부품이 그대로 보인다. 빈 슬롯은 Item 이 null.
internal sealed class LoadoutSlot
{
    internal LoadoutSlot(string label, Item item)
    {
        Label = label;
        Item = item;
    }

    internal string Label { get; }
    internal Item Item { get; }
}

internal sealed class EquippedGear
{
    internal EquippedGear(List<EquippedWeapon> weapons, HashSet<string> worn)
    {
        Weapons = weapons;
        Worn = worn;
    }

    internal List<EquippedWeapon> Weapons { get; }
    internal HashSet<string> Worn { get; }
}
