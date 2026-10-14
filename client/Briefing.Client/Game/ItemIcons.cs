using System;
using System.Collections.Generic;
using System.Diagnostics;
using Briefing.Core.Screen;
using Briefing.Ui;
using Comfort.Common;
using EFT;
using EFT.InventoryLogic;
using EFT.UI.DragAndDrop;
using UnityEngine.UI;

namespace Briefing.Game;

// 아이템은 ItemFactory.GetPresetItem 으로 만든다. 무기는 기본 프리셋이 부품째 나오고, 은신처 요구 아이템 아이콘도 같은 방법을 쓴다.
// 아이콘은 처음에 스프라이트가 비어 있다가 ItemIcon.Changed 뒤에 채워진다. 그 구독은 칸이 사라질 때 Fill 이 돌려준 함수로 푼다.
// 만든 아이템은 게임을 끌 때까지 tpl 별로 두고, 만들기에 실패한 tpl 도 기억해 다시 시도하지 않는다.
internal static class ItemIcons
{
    private static readonly Dictionary<string, Item> Presets = new Dictionary<string, Item>();
    private static readonly HashSet<string> Failed = new HashSet<string>();
    private static readonly HashSet<string> Logged = new HashSet<string>(); // 도착 시간 로그는 tpl 마다 한 번

    internal static Item Preset(string tpl)
    {
        if (string.IsNullOrEmpty(tpl) || Failed.Contains(tpl)) return null;
        if (Presets.TryGetValue(tpl, out var item)) return item;
        try
        {
            item = Singleton<ItemFactory>.Instance.GetPresetItem(tpl);
        }
        catch (Exception e)
        {
            Log.Warn($"item preset {tpl} failed: {e.Message}");
            item = null;
        }
        if (item == null)
        {
            Failed.Add(tpl);
            return null;
        }
        Presets[tpl] = item;
        return item;
    }

    // 못 읽으면 1×1.
    internal static (int W, int H) CellSize(Item item)
    {
        if (item == null) return (1, 1);
        try
        {
            var size = item.CalculateCellSize();
            return ItemGrid.Clamp(size.X, size.Y);
        }
        catch (Exception) { return (1, 1); }
    }

    // 게임 로케일의 "tpl ShortName". 키가 없으면 LocalizedValue 가 키를 그대로 돌려주므로 fallback 을 쓴다.
    internal static string ShortName(string tpl, string fallback)
    {
        try
        {
            if (string.IsNullOrEmpty(tpl)) return fallback;
            var key = tpl + " ShortName";
            var name = LocalizationManager.Instance.LocalizedValue(key);
            return string.IsNullOrWhiteSpace(name) || name == key ? fallback : name;
        }
        catch (Exception) { return fallback; }
    }

    // 아이콘이 이미 있으면 바로, 아니면 Changed 가 올 때 채운다. 실패하면 image 를 끈 채 두고 칸에는 짧은 이름만 보인다.
    // 돌려주는 함수는 구독 해제용이고 칸을 지울 때 부른다.
    internal static Action Fill(Image image, Item item)
    {
        image.enabled = false;
        if (item == null) return null;
        ItemIcon icon;
        try { icon = ItemViewFactory.LoadItemIcon(item); }
        catch (Exception e)
        {
            Log.Warn($"item icon {item.StringTemplateId} failed: {e.Message}");
            return null;
        }
        if (icon == null) return null;
        var tpl = item.StringTemplateId;
        if (Apply(image, icon)) return null;
        var clock = Stopwatch.StartNew();
        return icon.Changed.Subscribe(() => UiKit.Guard("item icon", () =>
        {
            if (!Apply(image, icon) || !Logged.Add(tpl)) return;
            Log.Info($"item icon {tpl}: arrived after {clock.ElapsedMilliseconds} ms, sprite {icon.Sprite.rect.width}x{icon.Sprite.rect.height}");
        }));
    }

    private static bool Apply(Image image, ItemIcon icon)
    {
        if (image == null || icon.Sprite == null) return false; // 칸이 이미 파괴됐으면 image 가 Unity null
        image.sprite = icon.Sprite;
        image.enabled = true;
        return true;
    }
}
