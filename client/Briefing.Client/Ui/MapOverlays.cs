using System;
using System.Collections.Generic;
using Briefing.Core.Map;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Briefing.Ui;

// 지도 위 구역, 잠긴 문, 탈출구, 퀘스트 마커와 말풍선. MapScene 이 정한 것만 찍는다.
// 마우스를 올릴 때마다 다시 만들면 올린 마커가 사라져 내림·올림이 되풀이되므로, Signature 가 같으면 색과 알파만 바꾼다.
// 아이콘과 마커는 확대해도 화면에서 같은 크기로 보이게 배율로 나눈다.
internal sealed class MapOverlays
{
    private const float DimMarkerAlpha = 0.35f;
    private const float OtherFloorAlpha = 0.45f;
    private const float TipGap = 16f; // 아이콘 가운데에서 말풍선 아래 끝까지

    private readonly RectTransform _column;
    private readonly Transform _areas;
    private readonly Transform _doors;
    private readonly Transform _exits;
    private readonly Transform _markers;
    private readonly GameObject _doorTemplate;
    private readonly GameObject _exitTemplate;
    private readonly GameObject _markerTemplate;
    private readonly Color _ring;
    private readonly RectTransform _tip;
    private readonly TMP_Text _tipTitle;
    private readonly TMP_Text _tipDetail;
    private readonly Dictionary<string, Sprite> _exitIcons = new Dictionary<string, Sprite>();
    private readonly Action<string> _onHot;
    private readonly Action<string> _onClick;
    private readonly List<GameObject> _spawned = new List<GameObject>();
    private readonly List<RectTransform> _icons = new List<RectTransform>();          // 배율로 나눠 크기를 지키는 것
    private readonly List<(GameObject Go, Image Ring, CanvasGroup Group)> _markerViews = new List<(GameObject, Image, CanvasGroup)>();
    private readonly List<AreaGraphic> _areaViews = new List<AreaGraphic>();
    private string _signature;
    private double _scale = 1;
    private string _hotMarker;   // 마우스를 올려 둔 마커의 퀘스트
    private string _hotLost;     // 올려 둔 채 마커가 파괴돼 내림 알림을 못 받은 퀘스트. 다음 Draw 끝에서 풀어 준다

    // onHot 은 마커에 마우스를 올리면 questId, 내리면 null. 둘 다 null 이어도 된다.
    internal MapOverlays(Transform screenRoot, Action<string> onHot, Action<string> onClick)
    {
        _onHot = onHot;
        _onClick = onClick;
        _column = (RectTransform)UiKit.Child(screenRoot, UiPaths.MapViewport).parent;
        _areas = UiKit.Child(screenRoot, UiPaths.MapAreas);
        _doors = UiKit.Child(screenRoot, UiPaths.MapDoors);
        _exits = UiKit.Child(screenRoot, UiPaths.MapExits);
        _markers = UiKit.Child(screenRoot, UiPaths.MapMarkers);
        _doorTemplate = Template(screenRoot, UiPaths.DoorTemplate);
        _exitTemplate = Template(screenRoot, UiPaths.ExitTemplate);
        _markerTemplate = Template(screenRoot, UiPaths.MarkerTemplate);
        _ring = _markerTemplate.GetComponent<Image>().color;
        UiKit.Get<Image>(_markerTemplate.transform, UiPaths.MarkerFill);
        UiKit.Get<TMP_Text>(_markerTemplate.transform, UiPaths.MarkerNumber);
        _tip = (RectTransform)UiKit.Child(screenRoot, UiPaths.MapTooltip);
        _tipTitle = UiKit.Get<TMP_Text>(_tip, UiPaths.TooltipTitle);
        _tipDetail = UiKit.Get<TMP_Text>(_tip, UiPaths.TooltipDetail);
        _exitIcons["pmc"] = UiKit.Get<Image>(screenRoot, UiPaths.IconPmc).sprite;
        _exitIcons["shared"] = UiKit.Get<Image>(screenRoot, UiPaths.IconShared).sprite;
        _exitIcons["scav"] = UiKit.Get<Image>(screenRoot, UiPaths.IconScav).sprite;
        _exitIcons["transit"] = UiKit.Get<Image>(screenRoot, UiPaths.IconTransit).sprite;
        UiKit.Get<Image>(screenRoot, UiPaths.IconDoor);
        _doorTemplate.SetActive(false);
        _exitTemplate.SetActive(false);
        _markerTemplate.SetActive(false);
        _tip.gameObject.SetActive(false);
    }

    private static GameObject Template(Transform root, string path)
    {
        var go = UiKit.Child(root, path).gameObject;
        if (go.GetComponent<Image>() == null) throw new InvalidOperationException($"{path} has no Image");
        return go;
    }

    internal void Clear()
    {
        foreach (var go in _spawned) if (go != null) UnityEngine.Object.Destroy(go);
        _spawned.Clear();
        _icons.Clear();
        _markerViews.Clear();
        _areaViews.Clear();
        _signature = null;
        if (_hotMarker != null) _hotLost = _hotMarker; // 파괴된 마커에는 OnPointerExit 가 오지 않는다
        _hotMarker = null;
        HideTip();
    }

    internal void Draw(MapSceneModel scene)
    {
        if (scene.Signature == _signature)
        {
            Emphasize(scene);
            return;
        }
        Clear();
        _signature = scene.Signature;
        foreach (var a in scene.Areas) Add("map area", () => AddArea(a));
        foreach (var d in scene.Doors) Add("map door", () => AddDoor(d));
        foreach (var e in scene.Exits) Add("map exit", () => AddExit(e));
        foreach (var m in scene.Markers) Add("map marker", () => AddMarker(m));
        Emphasize(scene);
        SetScale(_scale);
        if (_hotLost != null)
        {
            // 강조가 남지 않게 내림을 대신 알린다. 그사이 다른 퀘스트로 옮겨 갔으면 그대로 둔다.
            var lost = _hotLost;
            _hotLost = null;
            if (BriefingState.HotQuestId == lost) _onHot?.Invoke(null); // 부른 쪽이 다시 그리지만 Signature 가 같아 강조만 바뀐다
        }
    }

    // 실패하면 그 항목이 만든 것을 바로 지워 템플릿 색에 번호 1 인 마커가 남지 않게 한다.
    // 그러면 그 종류는 개수가 장면과 달라져 Emphasize 가 건너뛴다.
    private void Add(string what, Action add)
    {
        int spawned = _spawned.Count, icons = _icons.Count, markers = _markerViews.Count, areas = _areaViews.Count;
        UiKit.Guard(what, () =>
        {
            try { add(); }
            catch
            {
                for (var i = spawned; i < _spawned.Count; i++) if (_spawned[i] != null) UnityEngine.Object.Destroy(_spawned[i]);
                _spawned.RemoveRange(spawned, _spawned.Count - spawned);
                _icons.RemoveRange(icons, _icons.Count - icons);
                _markerViews.RemoveRange(markers, _markerViews.Count - markers);
                _areaViews.RemoveRange(areas, _areaViews.Count - areas);
                throw; // 로그는 Guard 가 남긴다
            }
        });
    }

    // 순서는 Draw 와 같다. 실패해서 빠진 것이 있으면 개수가 달라 건너뛴다.
    private void Emphasize(MapSceneModel scene)
    {
        if (scene.Markers.Count == _markerViews.Count)
        {
            for (var i = 0; i < _markerViews.Count; i++)
            {
                var (go, ring, group) = _markerViews[i];
                if (go == null) continue;
                var look = scene.Markers[i].Emphasis;
                ring.color = look == Emphasis.Hot ? Color.white : _ring;
                group.alpha = look == Emphasis.Dim ? DimMarkerAlpha : 1f;
                if (look == Emphasis.Hot) go.transform.SetAsLastSibling(); // 강조한 마커를 맨 위로
            }
        }
        if (scene.Areas.Count == _areaViews.Count)
            for (var i = 0; i < _areaViews.Count; i++)
                if (_areaViews[i] != null) _areaViews[i].SetLook(scene.Areas[i].Emphasis);
    }

    // 확대해도 아이콘, 마커, 구역 테두리가 화면에서 같은 크기로 보이게 한다.
    internal void SetScale(double scale)
    {
        _scale = scale;
        var inverse = new Vector3((float)(1 / scale), (float)(1 / scale), 1f);
        foreach (var rt in _icons) if (rt != null) rt.localScale = inverse;
        foreach (var g in _areaViews) if (g != null) g.SetScale(scale);
        HideTip(); // 아이콘이 움직였다
    }

    private void AddArea(AreaDraw a)
    {
        var go = new GameObject("Area_" + a.N, typeof(RectTransform));
        go.layer = _areas.gameObject.layer;
        _spawned.Add(go);
        var rt = (RectTransform)go.transform;
        rt.SetParent(_areas, false);
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        var g = go.AddComponent<AreaGraphic>();
        g.Set(a.Polygon, UiKit.Hex(a.Color));
        g.SetScale(_scale);
        _areaViews.Add(g);
    }

    private void AddDoor(DoorDraw d)
    {
        var go = Spawn(_doorTemplate, _doors, "Door", d.At);
        var label = d.Label;
        Relay(go, () => ShowTip((RectTransform)go.transform, label, null), HideTip, null);
    }

    private void AddExit(ExitDraw e)
    {
        var go = Spawn(_exitTemplate, _exits, "Exit", e.At);
        var image = go.GetComponent<Image>();
        image.sprite = _exitIcons.TryGetValue(e.Kind ?? "", out var icon) ? icon : _exitIcons["pmc"];
        image.color = new Color(1, 1, 1, e.OtherFloor ? OtherFloorAlpha : 1f);
        var title = e.Title;
        var detail = e.Detail;
        Relay(go, () => ShowTip((RectTransform)go.transform, title, detail), HideTip, null);
    }

    private void AddMarker(MarkerDraw m)
    {
        var go = Spawn(_markerTemplate, _markers, "Marker_" + m.N, m.At);
        UiKit.Get<Image>(go.transform, UiPaths.MarkerFill).color = UiKit.Hex(m.Color);
        UiKit.Get<TMP_Text>(go.transform, UiPaths.MarkerNumber).text = m.N.ToString(System.Globalization.CultureInfo.InvariantCulture);
        var group = go.AddComponent<CanvasGroup>(); // 고리, 원, 번호를 함께 흐리게
        _markerViews.Add((go, go.GetComponent<Image>(), group));
        var questId = m.QuestId;
        Relay(go, () => { _hotMarker = questId; _onHot?.Invoke(questId); },
            () => { _hotMarker = null; _onHot?.Invoke(null); }, () => _onClick?.Invoke(questId));
    }

    private GameObject Spawn(GameObject template, Transform parent, string name, MapUv at)
    {
        var go = UnityEngine.Object.Instantiate(template, parent, false);
        _spawned.Add(go);
        go.name = name;
        go.SetActive(true);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = new Vector2((float)at.U, 1f - (float)at.V);
        rt.anchoredPosition = Vector2.zero;
        _icons.Add(rt);
        return go;
    }

    private static void Relay(GameObject go, Action enter, Action exit, Action click)
    {
        var relay = go.AddComponent<HoverRelay>();
        relay.Enter = enter;
        relay.Exit = exit;
        relay.Click = click;
    }

    // 아이콘 바로 위에 띄우고, 칸 밖으로 나가면 안으로 당긴다.
    private void ShowTip(RectTransform icon, string title, string detail)
    {
        _tipTitle.text = title ?? "";
        _tipDetail.text = detail ?? "";
        _tipDetail.gameObject.SetActive(!string.IsNullOrEmpty(detail));
        _tip.gameObject.SetActive(true);
        _tip.SetAsLastSibling();
        LayoutRebuilder.ForceRebuildLayoutImmediate(_tip);
        var at = (Vector2)_column.InverseTransformPoint(icon.position);
        var size = _tip.rect.size;
        var area = _column.rect;
        var x = Mathf.Clamp(at.x, area.xMin + size.x / 2, Mathf.Max(area.xMin + size.x / 2, area.xMax - size.x / 2));
        var y = Mathf.Min(at.y + TipGap, area.yMax - size.y);
        // 앵커가 칸 가운데라 칸 로컬 좌표가 곧 anchoredPosition 이다.
        _tip.anchoredPosition = new Vector2(x - area.center.x, y - area.center.y);
    }

    private void HideTip()
    {
        if (_tip != null) _tip.gameObject.SetActive(false);
    }
}
