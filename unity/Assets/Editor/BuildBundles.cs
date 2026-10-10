using System.IO;
using UnityEditor;
using UnityEngine;

// 에디터 전용(Editor 폴더) — 번들에 들어가지 않는다.
// 메뉴 Briefing > Build Bundles, 또는 명령줄: Unity.exe -batchmode -quit -projectPath unity -executeMethod BuildBundles.Build
public static class BuildBundles
{
    private const string OutDir = "Build";

    [MenuItem("Briefing/Build Bundles")]
    public static void Build()
    {
        Directory.CreateDirectory(OutDir);
        var manifest = BuildPipeline.BuildAssetBundles(OutDir,
            BuildAssetBundleOptions.ChunkBasedCompression | BuildAssetBundleOptions.StrictMode,
            BuildTarget.StandaloneWindows64);
        if (manifest == null) throw new System.Exception("asset bundle build failed");
        Debug.Log("Built: " + string.Join(", ", manifest.GetAllAssetBundles()));
    }
}
