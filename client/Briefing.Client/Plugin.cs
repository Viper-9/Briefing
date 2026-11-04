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
        SideMargin = Config.Bind("Layout", "Side margin", 20f, "Left and right margin of the briefing panel, in reference resolution units. Applied when the tab is reopened.");
        TopMargin = Config.Bind("Layout", "Top margin", 8f, "Gap between the bottom of the tab bar and the top of the panel, in reference resolution units. Applied when the tab is reopened.");
        BottomMargin = Config.Bind("Layout", "Bottom margin", 40f, "Bottom margin that keeps the panel clear of the menu bar at the bottom of the screen, in reference resolution units. Applied when the tab is reopened.");
        LeftColumnWidth = Config.Bind("Layout", "Left column width", 0f, "Width of the left column (quest list), in reference resolution units. 0 = default (540). Saved when you drag the handle between the columns.");
        RightColumnWidth = Config.Bind("Layout", "Right column width", 0f, "Width of the right column, in reference resolution units. 0 = default (470). Saved when you drag the handle between the columns.");
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
