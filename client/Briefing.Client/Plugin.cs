using System.IO;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;

namespace Briefing;

// 버전은 Directory.Build.props 의 <Version> 에서 온다. 빌드 때 BuildInfo.g.cs 로 만들어진다.
[BepInPlugin("com.viper.briefing", "Briefing", BuildInfo.Version)]
public class Plugin : BaseUnityPlugin
{
    internal static Plugin Instance;
    internal static ConfigEntry<float> SideMargin, TopMargin, BottomMargin;
    internal static ConfigEntry<float> LeftColumnWidth, RightColumnWidth;

    internal static string BundlePath => Path.Combine(Path.GetDirectoryName(Instance.Info.Location)!, "briefing.bundle");

    private void Awake()
    {
        Instance = this;
        Log.Source = Logger;
        SideMargin = Config.Bind("Layout", "Side margin", 20f, "브리핑 패널의 좌우 여백(기준 해상도 단위). 탭을 다시 열 때 적용된다");
        TopMargin = Config.Bind("Layout", "Top margin", 8f, "탭 줄 아래쪽 끝과 패널 위쪽 사이 여백(기준 해상도 단위). 탭을 다시 열 때 적용된다");
        BottomMargin = Config.Bind("Layout", "Bottom margin", 40f, "화면 아래쪽 메뉴 막대를 피하는 하단 여백(기준 해상도 단위). 탭을 다시 열 때 적용된다");
        LeftColumnWidth = Config.Bind("Layout", "Left column width", 0f, "왼쪽 열(퀘스트 목록) 폭(기준 해상도 단위). 0 = 기본 폭(540). 열 사이 손잡이를 끌면 저장된다");
        RightColumnWidth = Config.Bind("Layout", "Right column width", 0f, "오른쪽 열 폭(기준 해상도 단위). 0 = 기본 폭(470). 열 사이 손잡이를 끌면 저장된다");
        Tab.BriefingTabHost.ViewFactory = () => new Ui.BundleBriefingView();
        var harmony = new Harmony("com.viper.briefing");
        // 하나가 실패해도 나머지는 걸리도록 클래스마다 따로 등록한다.
        PatchOne(harmony, typeof(Patches.InventoryScreenShowPatch));
        PatchOne(harmony, typeof(Patches.RaidStartPatch));
        PatchOne(harmony, typeof(Patches.TabReturnPatch));
        PatchOne(harmony, typeof(Patches.TabClickPatch));
        foreach (var m in harmony.GetPatchedMethods()) Log.Info($"patched {m.DeclaringType?.Name}.{m.Name}");
        Log.Info($"Briefing {Info.Metadata.Version} loaded, bundle: {BundlePath}");
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
}
