using System;
using System.IO;
using System.Linq;
using EFT.UI;
using TMPro;
using UnityEngine;

namespace Briefing.Ui;

// briefing.bundle 은 게임 실행 중 한 번만 읽어 프리팹을 캐시한다(같은 번들을 두 번 LoadFromFile 하면 실패한다).
internal static class BundleLoader
{
    private static GameObject _prefab;
    private static bool _failed;

    // 실패하면 null(번들 없음/로드 실패) 또는 예외. 부분적으로 만든 인스턴스는 지우고 던진다.
    internal static GameObject CreateScreen(InventoryScreen screen, RectTransform parent)
    {
        var prefab = LoadPrefab();
        if (prefab == null) return null;

        var go = UnityEngine.Object.Instantiate(prefab, parent, false);
        try
        {
            go.name = "BriefingScreen";
            var rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            SwapFonts(go, GameFontTemplate(screen));
            return go;
        }
        catch
        {
            UnityEngine.Object.Destroy(go);
            throw;
        }
    }

    // 임무 탭 라벨 = 게임 글꼴 원본. 한글이 들어 있는 TMP 글꼴이다.
    internal static TMP_Text GameFontTemplate(InventoryScreen screen) =>
        screen._tabDictionary[EInventoryTab.Notes].GetComponentsInChildren<TMP_Text>(true).FirstOrDefault();

    // 번들 안 TMP 텍스트는 에디터 글꼴(한글 없음)을 들고 온다 → 게임 글꼴로 바꾼다. 크기·정렬·색은 프리팹 값 유지.
    internal static void SwapFonts(GameObject root, TMP_Text template)
    {
        if (template == null) { Log.Warn("game font template not found; bundle fonts kept"); return; }
        var count = 0;
        foreach (var t in root.GetComponentsInChildren<TMP_Text>(true))
        {
            t.font = template.font;
            t.fontSharedMaterial = template.fontSharedMaterial;
            count++;
        }
        Log.Info($"swapped font on {count} text(s) to {template.font?.name}");
    }

    private static GameObject LoadPrefab()
    {
        if (_prefab != null || _failed) return _prefab;
        try
        {
            if (!File.Exists(Plugin.BundlePath)) throw new FileNotFoundException("briefing.bundle missing", Plugin.BundlePath);
            var bundle = AssetBundle.LoadFromFile(Plugin.BundlePath) ?? throw new Exception("AssetBundle.LoadFromFile returned null");
            _prefab = bundle.LoadAsset<GameObject>("BriefingScreen") ?? throw new Exception("BriefingScreen not in bundle");
            Log.Info("briefing.bundle loaded");
        }
        catch (Exception e)
        {
            _failed = true;
            Log.Error($"bundle load failed, no briefing tab: {e.Message}");
        }
        return _prefab;
    }
}
