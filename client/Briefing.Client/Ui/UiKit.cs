using System;
using UnityEngine;

namespace Briefing.Ui;

// 프리팹 요소 찾기, 샘플 지우기, 색, 예외 막기. 경로가 틀리면 어느 경로인지 예외 메시지에 담는다.
internal static class UiKit
{
    internal static Transform Child(Transform root, string path)
    {
        var t = root.Find(path);
        if (t == null) throw new InvalidOperationException($"prefab path missing: {root.name}/{path} (see Ui/UiPaths.cs)");
        return t;
    }

    internal static T Get<T>(Transform root, string path) where T : Component
    {
        var c = Child(root, path).GetComponent<T>();
        if (c == null) throw new InvalidOperationException($"prefab path {root.name}/{path} has no {typeof(T).Name}");
        return c;
    }

    // 에디터 미리보기용 Sample* 자식을 지운다. Destroy 는 프레임 끝에 일어나므로 먼저 꺼 둔다.
    internal static void DestroySamples(Transform parent)
    {
        foreach (Transform child in parent)
        {
            if (!child.name.StartsWith(UiPaths.SamplePrefix, StringComparison.Ordinal)) continue;
            child.gameObject.SetActive(false);
            UnityEngine.Object.Destroy(child.gameObject);
        }
    }

    internal static Color Hex(string hex) => ColorUtility.TryParseHtmlString(hex, out var c) ? c : Color.white;

    // 버튼 처리기와 그리기는 게임 이벤트 안에서 돌아서 예외가 새면 캐릭터 화면이 망가질 수 있다.
    internal static void Guard(string what, Action action)
    {
        try { action(); }
        catch (Exception e) { Log.Error($"briefing {what} failed: {e}"); }
    }
}
