using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

// 에디터 전용. 시험용 BriefingScreen 프리팹을 코드로 만든다.
// 메뉴 Briefing > Create Test Prefab, 또는 -executeMethod CreateTestPrefab.Create
public static class CreateTestPrefab
{
    private const string PrefabPath = "Assets/Briefing/BriefingScreen.prefab";

    [MenuItem("Briefing/Create Test Prefab")]
    public static void Create()
    {
        if (File.Exists(PrefabPath))
        {
            if (Application.isBatchMode)
            {
                if (!System.Environment.GetCommandLineArgs().Contains("-forcePrefab"))
                {
                    Debug.Log("CreateTestPrefab: " + PrefabPath + " already exists; skipped (pass -forcePrefab to overwrite)");
                    return;
                }
            }
            else if (!EditorUtility.DisplayDialog("Create Test Prefab", PrefabPath + " already exists. Overwrite it?", "Overwrite", "Cancel"))
            {
                return;
            }
        }

        Directory.CreateDirectory("Assets/Briefing");

        var root = new GameObject("BriefingScreen", typeof(RectTransform));
        var rt = (RectTransform)root.transform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        var h = root.AddComponent<HorizontalLayoutGroup>();
        h.spacing = 14;
        h.padding = new RectOffset(20, 20, 20, 20);
        h.childControlWidth = true;
        h.childControlHeight = true;
        h.childForceExpandHeight = true;

        AddColumn(root.transform, "ColLeft", 28, "왼쪽 열");
        AddColumn(root.transform, "ColMap", 44, "지도");
        AddColumn(root.transform, "ColRight", 28, "오른쪽 열");

        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        var importer = AssetImporter.GetAtPath(PrefabPath);
        if (importer == null)
        {
            Debug.LogError("No AssetImporter for " + PrefabPath);
            return;
        }
        importer.assetBundleName = "briefing.bundle";
        importer.SaveAndReimport();
        Debug.Log("Created: " + PrefabPath);
    }

    private static void AddColumn(Transform parent, string name, float flexWidth, string text)
    {
        var col = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        col.transform.SetParent(parent, false);
        col.GetComponent<Image>().color = new Color32(0x18, 0x1A, 0x1A, 217); // alpha 0.85
        col.GetComponent<LayoutElement>().flexibleWidth = flexWidth;

        var label = new GameObject("Text", typeof(RectTransform));
        label.transform.SetParent(col.transform, false);
        var lrt = (RectTransform)label.transform;
        lrt.anchorMin = Vector2.zero;
        lrt.anchorMax = Vector2.one;
        lrt.offsetMin = Vector2.zero;
        lrt.offsetMax = Vector2.zero;
        var tmp = label.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = 24;
    }
}
