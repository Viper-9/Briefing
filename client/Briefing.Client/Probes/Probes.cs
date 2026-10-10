using System;
using System.Linq;
using System.Threading;
using Comfort.Common;
using EFT;
using EFT.InventoryLogic;
using EFT.UI;
using EFT.UI.DragAndDrop;
using HarmonyLib;
using SPT.Common.Http;
using UnityEngine;
using UnityEngine.UI;

namespace Briefing.Probes;

// 0단계 실측: F11 을 누르면 아래 값을 로그로 남긴다. 캐릭터 화면이 열려 있을 때 누르면 아이콘·얼굴도 화면에 붙여 본다.
internal static class Probes
{
    private const string RifleTpl = "5447a9cd4bdc2dbd208b4567"; // M4A1 — 무기(자식 슬롯 있는 아이템) 아이콘 시험
    private const string WiresTpl = "5c06779c86f77426e00dd782"; // Wires — 단일 아이템 아이콘 시험
    private const string PraporId = "54cb50c76803fa8b248b4571";

    internal static void Run()
    {
        Safe("language", () => $"LocalizationManager.Culture = {LocalizationManager.Instance.Culture}");
        Safe("session", () => $"RequestHandler.SessionId = {RequestHandler.SessionId}");

        var screen = UnityEngine.Object.FindObjectOfType<InventoryScreen>();
        var profile = screen != null
            ? (Profile)AccessTools.Field(typeof(InventoryScreen), "_profile").GetValue(screen)
            : Singleton<GameWorld>.Instance?.MainPlayer?.Profile;
        Safe("profile", () => $"profile.Id = {profile?.Id}, from {(screen != null ? "InventoryScreen._profile" : "MainPlayer")}");
        Safe("counters", () =>
        {
            var c = profile.TaskConditionCounters;
            var sample = string.Join("; ", c.Take(5).Select(kv => $"key={kv.Key} id={kv.Value.Id} sourceId={kv.Value.SourceId} type={kv.Value.Type} value={kv.Value.Value}"));
            return $"TaskConditionCounters = {c.Count} (first 5: {sample})";
        });
        Safe("raid", () =>
        {
            var world = Singleton<GameWorld>.Instance;
            return $"GameWorld = {(world != null)}, MainPlayer.Location = {world?.MainPlayer?.Location}, LocationId = {world?.LocationId}";
        });

        var panel = screen != null ? screen.GetComponent<Tab.BriefingTabHost>()?.Panel : null;
        if (panel == null) { Log.Info("[probe] briefing panel not open — icon/avatar probes skipped (open the briefing tab and press again)"); return; }
        Safe("icon wires", () => AttachIcon(panel, WiresTpl, new Vector2(40, -40)));
        Safe("icon rifle", () => AttachIcon(panel, RifleTpl, new Vector2(200, -40)));
        Safe("avatar prapor", () => AttachAvatar(panel, profile, PraporId, new Vector2(40, -200)));
    }

    private static string AttachIcon(GameObject panel, string tpl, Vector2 pos)
    {
        var item = Singleton<ItemFactory>.Instance.CreateItem(MongoID.Generate().ToString(), tpl, null);
        var icon = ItemViewFactory.LoadItemIcon(item);
        var image = NewImage(panel, $"ProbeIcon_{tpl}", pos, new Vector2(128, 64));
        void Apply() { image.sprite = icon.Sprite; image.preserveAspect = true; Log.Info($"[probe] icon {tpl} sprite = {icon.Sprite?.rect}"); }
        Apply();
        icon.Changed.Bind(Apply);
        return $"LoadItemIcon({item.TemplateId}) ok, size {item.CalculateCellSize()}";
    }

    private static string AttachAvatar(GameObject panel, Profile profile, string traderId, Vector2 pos)
    {
        var image = NewImage(panel, "ProbeAvatar", pos, new Vector2(96, 96));
        var info = profile.TradersInfo[traderId];
        _ = info.Settings.GetAndAssignAvatar(image, CancellationToken.None)
            .ContinueWith(t => Log.Info($"[probe] avatar assigned = {(t.IsFaulted ? t.Exception?.GetBaseException().Message : t.Result.ToString())}"));
        return $"TradersInfo[{traderId}] settings id = {info.Settings?.Id}";
    }

    private static Image NewImage(GameObject panel, string name, Vector2 pos, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(panel.transform, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0, 1);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        return go.GetComponent<Image>();
    }

    private static void Safe(string label, Func<string> probe)
    {
        try { Log.Info($"[probe] {label}: {probe()}"); }
        catch (Exception e) { Log.Warn($"[probe] {label} failed: {e.GetType().Name}: {e.Message}"); }
    }
}
