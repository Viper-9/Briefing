using System.IO;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

namespace Briefing;

[BepInPlugin("com.viper.briefing", "Briefing", "0.0.1")]
public class Plugin : BaseUnityPlugin
{
    internal static Plugin Instance;
    internal static ConfigEntry<KeyboardShortcut> ProbeKey;
    internal static ConfigEntry<float> SideMargin, TopMargin, BottomMargin;

    internal static string BundlePath => Path.Combine(Path.GetDirectoryName(Instance.Info.Location)!, "briefing.bundle");

    private void Awake()
    {
        Instance = this;
        Log.Source = Logger;
        ProbeKey = Config.Bind("Debug", "Probe key", new KeyboardShortcut(KeyCode.F11),
            "0단계 실측: 누르면 게임 API 값을 BepInEx 로그에 남긴다");
        SideMargin = Config.Bind("Layout", "Side margin", 20f, "브리핑 패널의 좌우 여백(기준 해상도 단위). 탭을 다시 열 때 적용된다");
        TopMargin = Config.Bind("Layout", "Top margin", 8f, "탭 줄 아래쪽 끝과 패널 위쪽 사이 여백(기준 해상도 단위). 탭을 다시 열 때 적용된다");
        BottomMargin = Config.Bind("Layout", "Bottom margin", 40f, "화면 아래쪽 메뉴 막대를 피하는 하단 여백(기준 해상도 단위). 탭을 다시 열 때 적용된다");
        Tab.BriefingTabHost.ViewFactory = () => new Ui.BundleBriefingView();
        var harmony = new Harmony("com.viper.briefing");
        // 클래스마다 따로 등록한다: 프로브 패치가 실패해도 핵심 패치는 살아남는다.
        PatchOne(harmony, typeof(Patches.InventoryScreenShowPatch));
        PatchOne(harmony, typeof(Probes.RaidStartProbe));
        foreach (var m in harmony.GetPatchedMethods()) Log.Info($"patched {m.DeclaringType?.Name}.{m.Name}");
        Log.Info($"Briefing {Info.Metadata.Version} loaded, bundle: {BundlePath}");
        _ = Probes.ServerProbe.RunAsync();
    }

    private static void PatchOne(Harmony harmony, System.Type patchClass)
    {
        try
        {
            harmony.CreateClassProcessor(patchClass).Patch();
        }
        catch (System.Exception e)
        {
            Log.Error($"patch {patchClass.Name} failed: {e}");
        }
    }

    private void Update()
    {
        if (Input.GetKeyDown(ProbeKey.Value.MainKey)) Probes.Probes.Run();
    }
}
