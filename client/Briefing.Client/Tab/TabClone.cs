using System.Collections.Generic;
using System.Reflection;
using EFT.UI;
using HarmonyLib;
using TMPro;
using UnityEngine;

namespace Briefing.Tab;

// 게임 탭을 복제해 모양(바탕, 아이콘, 글꼴, 마우스 올림)은 그대로 쓰고 동작은 떼어 낸다.
internal static class TabClone
{
    private static readonly FieldInfo SelectionEventField = AccessTools.Field(typeof(global::Tab), "_onSelectionChanged");

    // 꺼진 임시 부모 밑에 복제해서 정리하기 전에 복제본의 Awake/OnEnable 이 돌지 않게 한다. 복제본은 꺼진 채 parent 밑으로 옮겨 돌려준다.
    // labels 에는 글자가 들어 있는 TMP 를 담는다(글자는 부르는 쪽이 정한다).
    internal static global::Tab Make(global::Tab source, Transform parent, string name, List<TMP_Text> labels)
    {
        var holder = new GameObject("BriefingTabHolder");
        holder.SetActive(false);
        try
        {
            var go = Object.Instantiate(source.gameObject, holder.transform, false);
            go.SetActive(false);
            go.name = name;
            var tab = go.GetComponent<global::Tab>();
            SelectionEventField?.SetValue(tab, null); // 원본의 이벤트 구독이 딸려 오지 않게
            foreach (var lt in go.GetComponentsInChildren<LocalizedText>(true)) Object.DestroyImmediate(lt);
            tab.LocalizedText = null;
            if (labels != null)
            {
                labels.Clear();
                foreach (var t in go.GetComponentsInChildren<TMP_Text>(true))
                    if (!string.IsNullOrWhiteSpace(t.text)) labels.Add(t);
            }
            go.transform.SetParent(parent, false);
            return tab;
        }
        finally
        {
            Object.Destroy(holder);
        }
    }
}
