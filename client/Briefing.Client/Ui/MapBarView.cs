using System;
using System.Collections.Generic;
using System.Globalization;
using Briefing.Core.Screen;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Briefing.Ui;

// 맵 칩 줄. 칩 템플릿을 맵마다 복제한다. 고른 칩은 Selected 바탕을 켜고, 목표가 없는 맵은 흐리게 그린다.
// 평소 글자색은 프리팹 값을 따르고, 고른 칩은 밝은 바탕 위라 글자색을 코드에서 정한다.
internal sealed class MapBarView
{
    private static readonly Color SelectedName = UiKit.Hex("#141615");
    private static readonly Color SelectedCount = UiKit.Hex("#3c403d");
    private static readonly Color SelectedRaid = UiKit.Hex("#6b5214"); // 밝은 바탕 위에서는 평소 금색이 흐려 보인다
    private const float DimAlpha = 0.45f;

    private readonly Transform _bar;
    private readonly GameObject _template;
    private readonly Color _nameColor;
    private readonly Color _countColor;
    private readonly Color _raidColor;
    private readonly Action<string> _onPick;
    private readonly List<GameObject> _chips = new List<GameObject>();

    internal MapBarView(Transform screenRoot, Action<string> onPick)
    {
        _onPick = onPick;
        _bar = UiKit.Child(screenRoot, UiPaths.MapBar);
        _template = UiKit.Child(screenRoot, UiPaths.ChipTemplate).gameObject;
        _nameColor = UiKit.Get<TMP_Text>(_template.transform, UiPaths.ChipName).color;
        _countColor = UiKit.Get<TMP_Text>(_template.transform, UiPaths.ChipCount).color;
        _raidColor = UiKit.Get<TMP_Text>(_template.transform, UiPaths.ChipRaid).color;
        UiKit.Child(_template.transform, UiPaths.ChipSelected);
        if (_template.GetComponent<Button>() == null) throw new InvalidOperationException($"{UiPaths.ChipTemplate} has no Button");
        _template.SetActive(false);
        UiKit.DestroySamples(_bar);
    }

    internal void Render(IReadOnlyList<MapChip> chips)
    {
        Clear();
        foreach (var chip in chips)
        {
            var go = UnityEngine.Object.Instantiate(_template, _bar, false);
            _chips.Add(go); // 도중에 예외가 나도 Clear 가 지울 수 있게 바로 넣는다
            go.SetActive(true); // QuestListView.Spawn 처럼 켠 뒤에 채운다
            go.name = "Chip_" + chip.Key;
            var t = go.transform;
            var name = UiKit.Get<TMP_Text>(t, UiPaths.ChipName);
            name.text = chip.Name;
            name.color = chip.Selected ? SelectedName : _nameColor;
            var count = UiKit.Get<TMP_Text>(t, UiPaths.ChipCount);
            count.text = chip.Count.ToString(CultureInfo.InvariantCulture);
            count.color = chip.Selected ? SelectedCount : _countColor;
            var raid = UiKit.Get<TMP_Text>(t, UiPaths.ChipRaid);
            raid.text = Texts.RaidTag;
            raid.color = chip.Selected ? SelectedRaid : _raidColor;
            raid.gameObject.SetActive(chip.InRaid);
            UiKit.Child(t, UiPaths.ChipSelected).gameObject.SetActive(chip.Selected);
            var group = go.GetComponent<CanvasGroup>();
            if (group != null) group.alpha = chip.Dim && !chip.Selected ? DimAlpha : 1f;
            var key = chip.Key;
            go.GetComponent<Button>().onClick.AddListener(() => UiKit.Guard("map chip", () => _onPick(key)));
            TabFeel.Attach(go);
        }
    }

    internal void Clear()
    {
        foreach (var c in _chips) if (c != null) UnityEngine.Object.Destroy(c);
        _chips.Clear();
    }
}
