using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Briefing.Core.Contract;
using Briefing.Core.Map;
using Briefing.Core.Screen;
using Briefing.Map;
using TMPro;
using Unity.VectorGraphics;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Briefing.Ui;

// 가운데 지도 칸. 고른 맵의 map.json 과 층 SVG 를 받아 층마다 SVGImage 를 겹친다.
// 층 이미지는 지도 폴더가 바뀔 때만 다시 만들고, Render 는 층 모습, 층 버튼, 오버레이, 출처만 바꾼다.
// Content 는 기본 층 viewBox 비율로 칸 안에 꽉 차게 놓고, 확대·이동은 그 위에 얹는다.
internal sealed class MapView
{
    private static readonly Color FloorTextOn = UiKit.Hex("#161817");

    private readonly RectTransform _viewport;
    private readonly RectTransform _content;
    private readonly Transform _layers;
    private readonly Transform _floors;
    private readonly GameObject _floorTemplate;
    private readonly Color _floorText;
    private readonly TMP_Text _message;
    private readonly TMP_Text _credit;
    private readonly GameObject _toggles;   // 체크박스 줄과 확대 버튼은 지도 정의가 있을 때만 보인다
    private readonly GameObject _zoom;
    private readonly Button _exitsButton;   // 누르면 종류별 체크 목록(ExitsMenu)이 열린다
    private readonly GameObject _exitsMenu;
    private readonly GameObject _exitsBlocker; // 목록이 열린 동안만 켜지는 투명한 막. 밖을 누르면 닫는다
    private readonly Image _exitsCheck;
    private readonly Toggle _exitPmc;
    private readonly Toggle _exitScav;
    private readonly Toggle _exitTransit;
    private readonly Toggle _doorsToggle;
    private readonly Toggle _areasToggle;
    private readonly MapOverlays _overlays;
    private readonly Action _onChanged;
    private readonly List<(MapLayerDef Layer, SVGImage Image)> _images = new List<(MapLayerDef, SVGImage)>();
    private readonly List<GameObject> _floorButtons = new List<GameObject>();
    private string _folder;
    private MapDef _def;
    private int _load;            // 지도가 바뀌면 늘려서 이전 불러오기의 뒤처리를 버린다
    private string _floorsKey;    // 마우스를 올릴 때마다 Render 가 불리므로 층 버튼이 같으면 다시 만들지 않는다
    private BriefingResponse _response; // 미리 그려 두기가 나머지 맵을 고를 때 쓴다
    private BriefingMap _map;     // Focus 가 쓴다
    private ISet<string> _shown;
    private string _pendingFocus; // 지도 정의를 받기 전에 들어온 초점 요청
    private bool _failed;         // _folder 를 못 불러와 실패 문구가 떠 있다
    private bool _starting;       // 캐시된 정의면 LoadAsync 가 Load 안에서 바로 끝난다
    private Task _layersTask;     // Retry 가 겹쳐 시작하지 않게 둔다
    private const float ToggleSpacing = 6f; // 프리팹 LayerToggles 의 spacing
    private RectTransform _stackHost;
    private bool _stacked;
    private bool _dragReady;     // 시작점을 못 잡았으면 이번 끌기는 움직이지 않는다
    private Vector2 _dragFrom;
    private MapViewport _dragView;

    // onChanged 는 지도 정의를 받았거나 층·체크박스를 바꿨을 때 불리고, 부른 쪽이 Render 로 다시 그린다.
    // onHot 은 마커에 마우스를 올리면 questId, 내리면 null.
    internal MapView(Transform screenRoot, Action onChanged, Action<string> onHot = null, Action<string> onMarkerClick = null)
    {
        _onChanged = onChanged;
        _viewport = (RectTransform)UiKit.Child(screenRoot, UiPaths.MapViewport);
        _content = (RectTransform)UiKit.Child(screenRoot, UiPaths.MapContent);
        _layers = UiKit.Child(screenRoot, UiPaths.MapLayers);
        _floors = UiKit.Child(screenRoot, UiPaths.MapFloors);
        _floorTemplate = UiKit.Child(screenRoot, UiPaths.FloorTemplate).gameObject;
        _floorText = UiKit.Get<TMP_Text>(_floorTemplate.transform, UiPaths.FloorLabel).color;
        UiKit.Child(_floorTemplate.transform, UiPaths.FloorSelected);
        UiKit.Child(_floorTemplate.transform, UiPaths.FloorDot);
        if (_floorTemplate.GetComponent<Button>() == null) throw new InvalidOperationException($"{UiPaths.FloorTemplate} has no Button");
        _message = UiKit.Get<TMP_Text>(screenRoot, UiPaths.MapMessage);
        _credit = UiKit.Get<TMP_Text>(screenRoot, UiPaths.MapCredit);
        _toggles = UiKit.Child(screenRoot, UiPaths.MapToggles).gameObject;
        _zoom = UiKit.Child(screenRoot, UiPaths.MapZoom).gameObject;
        _exitsButton = UiKit.Get<Button>(screenRoot, UiPaths.ToggleExits);
        _exitsMenu = UiKit.Child(screenRoot, UiPaths.ExitsMenu).gameObject;
        _exitsBlocker = UiKit.Child(screenRoot, UiPaths.ExitsBlocker).gameObject;
        _exitsCheck = UiKit.Get<Image>(_exitsButton.transform, UiPaths.ExitsCheck);
        _exitPmc = LayerToggle(screenRoot, UiPaths.ExitsPmc, Texts.ExitKind("pmc"), BriefingState.ShowExitPmc, v => { BriefingState.ShowExitPmc = v; UpdateExitsCheck(); });
        _exitScav = LayerToggle(screenRoot, UiPaths.ExitsScav, Texts.ExitKind("scav"), BriefingState.ShowExitScav, v => { BriefingState.ShowExitScav = v; UpdateExitsCheck(); });
        _exitTransit = LayerToggle(screenRoot, UiPaths.ExitsTransit, Texts.ExitKind("transit"), BriefingState.ShowExitTransit, v => { BriefingState.ShowExitTransit = v; UpdateExitsCheck(); });
        _exitsButton.onClick.AddListener(() => UiKit.Guard("map exits menu", () => SetExitsMenu(!_exitsMenu.activeSelf)));
        UiKit.Get<Button>(screenRoot, UiPaths.ExitsBlocker).onClick.AddListener(() => UiKit.Guard("map exits menu close", () => SetExitsMenu(false)));
        _doorsToggle = LayerToggle(screenRoot, UiPaths.ToggleDoors, Texts.LayerDoors, BriefingState.ShowDoors, v => BriefingState.ShowDoors = v);
        _areasToggle = LayerToggle(screenRoot, UiPaths.ToggleAreas, Texts.LayerAreas, BriefingState.ShowAreas, v => BriefingState.ShowAreas = v);
        UiKit.Get<Button>(screenRoot, UiPaths.ZoomIn).onClick.AddListener(() => UiKit.Guard("map zoom in", () => ZoomAtCenter(1.25)));
        UiKit.Get<Button>(screenRoot, UiPaths.ZoomOut).onClick.AddListener(() => UiKit.Guard("map zoom out", () => ZoomAtCenter(0.8)));
        _overlays = new MapOverlays(screenRoot, onHot, onMarkerClick);
        UiKit.Guard("map toggle stack", () => _stackHost = CreateStackHost()); // 실패하면 가로 한 줄로만 둔다
        _floorTemplate.SetActive(false);
        UiKit.DestroySamples(_floors);
        _credit.text = "";

        var input = _viewport.gameObject.AddComponent<MapInput>();
        input.Resized = () =>
        {
            Fit();
            UiKit.Guard("map toggle layout", UpdateToggleLayout);
        };
        input.Wheel = OnWheel;
        input.DragStarted = OnDragStarted;
        input.Dragged = OnDragged;
        input.DoubleClicked = () => SetZoom(MapProjection.FitView());
        ShowMessage(null);
        SetControls(false);
    }

    // 칸이 좁으면 체크박스를 세로로 쌓는다. LayoutGroup 은 한 오브젝트에 하나뿐이고 같은 프레임에 바꿔 끼울 수도 없어서,
    // 세로용 VerticalLayoutGroup 을 LayerToggles 아래에 미리 만들어 두고 체크박스를 그리로 옮긴다.
    // 이 그릇은 ignoreLayout 에 오른쪽 위 기준이라 같은 모서리에서 아래로 쌓인다.
    private RectTransform CreateStackHost()
    {
        var go = new GameObject("Stack", typeof(RectTransform));
        go.layer = _toggles.layer;
        var rt = (RectTransform)go.transform;
        rt.SetParent(_toggles.transform, false);
        rt.anchorMin = rt.anchorMax = rt.pivot = Vector2.one;
        rt.anchoredPosition = Vector2.zero;
        go.AddComponent<LayoutElement>().ignoreLayout = true;
        var group = go.AddComponent<VerticalLayoutGroup>();
        group.spacing = ToggleSpacing;
        group.childAlignment = TextAnchor.UpperRight;
        group.childControlWidth = group.childControlHeight = true;
        group.childForceExpandWidth = group.childForceExpandHeight = false;
        var fit = go.AddComponent<ContentSizeFitter>();
        fit.horizontalFit = fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        return rt;
    }

    // 가로 한 줄로 둘지 세로로 쌓을지 정한다. 한 줄 폭은 지금 배치와 상관없이 체크박스 선호 폭의 합으로 구해야 깜빡이지 않는다.
    // 레이아웃 계산 전이라 폭이 0 이면 건너뛴다.
    private void UpdateToggleLayout()
    {
        if (_stackHost == null || _toggles == null || !_toggles.activeInHierarchy) return;
        var column = (RectTransform)_toggles.transform.parent;
        var toggles = new[] { _exitsButton.transform, _doorsToggle.transform, _areasToggle.transform }; // ExitsMenu 는 레이아웃 밖이라 폭에 넣지 않는다
        var row = 0f;
        foreach (var t in toggles) row += LayoutUtility.GetPreferredWidth((RectTransform)t);
        if (row <= 0) return;
        row += ToggleSpacing * (toggles.Length - 1);
        var floors = _floors.gameObject.activeInHierarchy ? LayoutUtility.GetPreferredWidth((RectTransform)_floors) : 0f;
        var stack = MapControls.Stack(column.rect.width, floors, row, MapControls.Margin);
        if (stack == _stacked) return;
        _stacked = stack;
        var parent = stack ? (Transform)_stackHost : _toggles.transform;
        foreach (var t in toggles)
        {
            t.SetParent(parent, false);
            t.SetAsLastSibling(); // 순서를 지킨다
        }
        if (!stack) _stackHost.SetAsLastSibling();
    }

    private Toggle LayerToggle(Transform root, string path, string label, bool isOn, Action<bool> store)
    {
        var toggle = UiKit.Get<Toggle>(root, path);
        UiKit.Get<TMP_Text>(toggle.transform, UiPaths.ToggleLabel).text = label;
        toggle.SetIsOnWithoutNotify(isOn);
        toggle.onValueChanged.AddListener(v => UiKit.Guard("map layer toggle", () =>
        {
            store(v);
            _onChanged();
        }));
        return toggle;
    }

    // 폴더가 바뀌면 새로 불러오고, 다 불러오면 onChanged 로 다시 그리게 한다. shown 은 마커를 찍을 퀘스트다.
    internal void Render(BriefingResponse response, BriefingMap map, ISet<string> shown, string emphasis)
    {
        var keyBefore = BriefingState.MapKey;
        if (map != null) BriefingState.UseMap(map.Key); // 응답이 잠깐 없을 때는 층과 확대를 지우지 않는다
        var keyChanged = BriefingState.MapKey != keyBefore;
        if (keyChanged) UiKit.Guard("map exits menu close", () => SetExitsMenu(false));
        _map = map;
        _response = response;
        _shown = shown;
        var folder = map != null && map.HasMap ? map.MapFolder : null;
        if (folder != _folder) Load(folder);
        else if (keyChanged && _def != null) UiKit.Guard("map zoom reset", ApplyZoom); // 같은 폴더의 다른 맵이면 Load 가 돌지 않으니 초기화한 확대를 여기서 적용한다
        if (map == null)
        {
            ShowMessage(null);
            return;
        }
        if (folder == null)
        {
            ShowMessage(Texts.MapNone);
            Precache(null); // 그릴 층이 없으니 바로 미리 그려 두기를 시작한다
            return;
        }
        if (_def == null) return; // 불러오는 중이거나 실패한 문구는 Load 쪽이 띄운다
        SyncToggles();
        MapSceneModel scene = null;
        UiKit.Guard("map scene", () => scene = MapScene.Build(new MapSceneInput
        {
            Response = response,
            Map = map,
            Def = _def,
            ShownQuestIds = shown,
            Level = BriefingState.MapLevel,
            ShowExitPmc = BriefingState.ShowExitPmc,
            ShowExitScav = BriefingState.ShowExitScav,
            ShowExitTransit = BriefingState.ShowExitTransit,
            ShowDoors = BriefingState.ShowDoors,
            ShowAreas = BriefingState.ShowAreas,
            EmphasisQuestId = emphasis,
        }));
        // Build 가 실패하면 직전 그림을 그대로 둔다. 빈 지도보다 낫고, 맵이 바뀌었다면 Load 가 이미 지웠다.
        if (scene == null) return;
        UiKit.Guard("map layers", () => ApplyLevel(scene.Level));
        UiKit.Guard("map floors", () => RenderFloors(scene.Floors));
        UiKit.Guard("map overlays", () => _overlays.Draw(scene));
        _credit.text = scene.Credit ?? "";
        UiKit.Guard("map toggle layout", UpdateToggleLayout);
    }

    // 그 퀘스트가 보이는 층으로 바꾸고, 마커와 구역이 화면 밖이면 보이게 옮긴다. 확대는 하지 않는다.
    // 지도 정의가 아직 없으면 받은 뒤에 한다. 층이 바뀌었으니 부른 쪽이 이어서 다시 그린다.
    internal void Focus(string questId)
    {
        if (_def == null)
        {
            _pendingFocus = questId;
            return;
        }
        _pendingFocus = null;
        if (_map == null) return;
        var focus = MapScene.Focus(_def, MapScene.Markers(_map, _shown), questId);
        if (focus == null) return;
        BriefingState.MapLevel = focus.Level;
        var size = _content.sizeDelta;
        var area = _viewport.rect.size;
        if (size.x <= 0 || size.y <= 0) return;
        SetZoom(MapScene.Reveal(BriefingState.MapZoom, focus, size.x, size.y, area.x, area.y));
    }

    // 지도 정의가 오기 전에 줄을 접었으면 미뤄 둔 초점 요청을 버린다.
    internal void CancelFocus() => _pendingFocus = null;

    private void SyncToggles()
    {
        SetLabel(_exitsButton.transform, Texts.LayerExits); // 게임 언어를 바꿨을 수 있어 매번 다시 쓴다
        SetToggleLabel(_exitPmc, Texts.ExitKind("pmc"));
        SetToggleLabel(_exitScav, Texts.ExitKind("scav"));
        SetToggleLabel(_exitTransit, Texts.ExitKind("transit"));
        SetToggleLabel(_doorsToggle, Texts.LayerDoors);
        SetToggleLabel(_areasToggle, Texts.LayerAreas);
        _exitPmc.SetIsOnWithoutNotify(BriefingState.ShowExitPmc);
        _exitScav.SetIsOnWithoutNotify(BriefingState.ShowExitScav);
        _exitTransit.SetIsOnWithoutNotify(BriefingState.ShowExitTransit);
        _doorsToggle.SetIsOnWithoutNotify(BriefingState.ShowDoors);
        _areasToggle.SetIsOnWithoutNotify(BriefingState.ShowAreas);
        UpdateExitsCheck();
    }

    private static void SetToggleLabel(Toggle toggle, string label) => SetLabel(toggle.transform, label);

    private static void SetLabel(Transform owner, string label) => UiKit.Get<TMP_Text>(owner, UiPaths.ToggleLabel).text = label;

    // 일부만 켜져 있으면 흐린 체크.
    private void UpdateExitsCheck()
    {
        var state = MapControls.ExitsCheck(BriefingState.ShowExitPmc, BriefingState.ShowExitScav, BriefingState.ShowExitTransit);
        _exitsCheck.enabled = state != CheckState.None;
        var color = _exitsCheck.color;
        color.a = state == CheckState.Some ? 0.4f : 1f;
        _exitsCheck.color = color;
    }

    // 목록과 막은 같이 열고 닫는다. 막이 켜진 동안은 지도 끌기와 휠이 막힌다.
    private void SetExitsMenu(bool open)
    {
        _exitsMenu.SetActive(open);
        _exitsBlocker.SetActive(open);
        if (open) UiKit.Guard("map exits menu sorting", RaiseExitsMenu);
    }

    // 프리팹을 만들 때는 하위 Canvas 가 루트라 overrideSorting 이 저장되지 않는다. 그래서 게임 캔버스 아래에 붙은 뒤인 열 때
    // 막은 +1, 목록은 +2 로 올려 마커와 체크박스 위에 그리고 클릭도 먼저 받게 한다.
    private void RaiseExitsMenu()
    {
        var host = _exitsMenu.transform.parent.GetComponentInParent<Canvas>();
        if (host == null) return;
        Raise(_exitsBlocker, host, 1);
        Raise(_exitsMenu, host, 2);
    }

    private static void Raise(GameObject go, Canvas host, int above)
    {
        var canvas = go.GetComponent<Canvas>();
        if (canvas == null) canvas = go.AddComponent<Canvas>();
        if (go.GetComponent<GraphicRaycaster>() == null) go.AddComponent<GraphicRaycaster>();
        canvas.overrideSorting = true;
        canvas.sortingLayerID = host.sortingLayerID;
        canvas.sortingOrder = host.sortingOrder + above;
    }

    // 탭을 숨길 때 부른다.
    internal void CloseMenus() => SetExitsMenu(false);

    private void Load(string folder)
    {
        _load++;
        _pendingFocus = null; // 다른 지도의 초점 요청이다
        _folder = folder;
        _def = null;
        _failed = false;
        _layersTask = null;
        ClearMap();
        if (folder == null) return;
        ShowMessage(Texts.MapLoading);
        // 정의가 캐시돼 있으면 LoadAsync 가 여기서 바로 끝나고 부른 Render 가 이어서 그리므로 onChanged 를 부르지 않는다.
        _starting = true;
        try { _ = LoadAsync(folder, _load); }
        finally { _starting = false; }
    }

    // 탭을 다시 열 때 빠진 것만 다시 받는다. map.json 이 실패했으면 폴더를 비워 다음 Render 가 처음부터 불러오게 하고,
    // 정의는 있는데 Sprite 가 없는 층이 있으면 그 층만 받는다.
    internal void Retry()
    {
        if (_failed)
        {
            _failed = false;
            _folder = null;
            return;
        }
        if (_def == null || _folder == null || _content == null) return;
        if (_layersTask != null && !_layersTask.IsCompleted) return;
        if (_def.Layers.All(Drawn)) return;
        _layersTask = LoadLayersAsync(_folder, _def, _load);
    }

    private bool Stale(int load) => load != _load || _content == null; // 화면이 파괴됐으면 _content 가 Unity null

    private bool Drawn(MapLayerDef layer) => _images.Any(x => x.Layer == layer && x.Image != null && x.Image.sprite != null);

    private async Task LoadAsync(string folder, int load)
    {
        try
        {
            var def = await MapFiles.DefAsync(folder);
            if (Stale(load)) return;
            if (def == null)
            {
                Failed();
                Precache(folder);
                return;
            }
            _def = def;
            ShowMessage(null);
            SetControls(true);
            foreach (var layer in def.Layers) _images.Add((layer, CreateLayer(layer)));
            Fit();
            if (_pendingFocus != null) UiKit.Guard("map focus", () => Focus(_pendingFocus));
            if (!_starting) _onChanged(); // Render 안에서 바로 끝났으면 그 Render 가 그린다
            if (Stale(load)) return;
            _layersTask = LoadLayersAsync(folder, def, load);
        }
        catch (Exception e)
        {
            Log.Error($"map load {folder} failed: {e}");
            if (Stale(load)) return;
            Failed();
            Precache(folder); // 밀려난 불러오기라면 지금 고른 맵 쪽이 시작한다
        }
    }

    // 첫 지도의 층 처리가 끝나면 성공이든 실패든 미리 그려 두기를 시작한다. 두 번째부터는 MapPrecache 가 무시한다.
    private void Precache(string folder) => UiKit.Guard("map precache", () => MapPrecache.Start(_response, folder));

    // 받기는 한꺼번에 시작하고 만들기는 한 프레임에 한 층씩, 기본 층부터. 이미 그린 층은 건너뛴다.
    // 끝났는데 그려진 층이 하나도 없으면 실패로 본다.
    private async Task LoadLayersAsync(string folder, MapDef def, int load)
    {
        try
        {
            var order = MapPrecachePlan.LayerOrder(def).Where(l => !Drawn(l)).ToList();
            var texts = order.Select(l => SvgSprites.TryGet(folder, l.Svg, out _) ? null : MapFiles.SvgAsync(folder, l.Svg)).ToList();
            for (var i = 0; i < order.Count; i++)
            {
                var layer = order[i];
                if (!SvgSprites.TryGet(folder, layer.Svg, out var sprite))
                {
                    var text = await texts[i];
                    await Task.Yield(); // 받은 프레임 말고 다음 프레임에 만든다
                    if (Stale(load)) return;
                    if (text != null)
                    {
                        // UiKit.Guard 로는 await 를 감쌀 수 없어 직접 잡는다.
                        try { sprite = await SvgSprites.BuildAsync(folder, layer.Svg, text); }
                        catch (Exception e) { Log.Error($"briefing map layer {folder}/{layer.Svg} failed: {e}"); }
                    }
                }
                if (Stale(load)) return;
                var image = _images.FirstOrDefault(x => x.Layer == layer).Image;
                if (sprite == null || image == null) continue; // 실패한 층은 비워 두고 층 버튼은 남긴다
                image.sprite = sprite;
                image.enabled = true;
            }
            if (Stale(load) || def.Layers.Any(Drawn)) return;
            Log.Warn($"map {folder}: no layer could be drawn");
            Failed();
        }
        catch (Exception e)
        {
            Log.Error($"map layers {folder} failed: {e}");
            if (!Stale(load)) Failed();
        }
        finally
        {
            if (!Stale(load)) Precache(folder); // 밀려난 불러오기는 시작하지 않는다
        }
    }

    // 실패 문구를 띄우고 이번에 그린 것을 지운다. 실패는 캐시하지 않으므로 다시 열면 Retry 가 다시 불러온다.
    private void Failed()
    {
        _failed = true;
        _def = null;
        ClearMap();
        ShowMessage(Texts.MapLoadFailed);
    }

    private void ClearMap()
    {
        foreach (var (_, image) in _images) if (image != null) UnityEngine.Object.Destroy(image.gameObject);
        _images.Clear();
        ClearFloors();
        _overlays.Clear();
        _credit.text = "";
        SetControls(false);
    }

    private void SetControls(bool visible)
    {
        if (!visible) SetExitsMenu(false); // 막은 체크박스 줄 밖에 있다
        _toggles.SetActive(visible);
        _zoom.SetActive(visible);
    }

    private SVGImage CreateLayer(MapLayerDef layer)
    {
        var go = new GameObject("Layer_" + layer.Level, typeof(RectTransform));
        go.layer = _layers.gameObject.layer;
        var rt = (RectTransform)go.transform;
        rt.SetParent(_layers, false);
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        var image = go.AddComponent<SVGImage>();
        image.raycastTarget = false;
        image.preserveAspect = false; // 층마다 viewBox 가 조금 달라도 같은 칸에 늘인다(웹과 같다)
        image.enabled = false;        // sprite 가 없으면 흰 사각형이 그려진다
        return image;
    }

    private void ApplyLevel(int level)
    {
        foreach (var (layer, image) in _images)
        {
            if (image == null) continue;
            var look = MapProjection.LayerStyle(layer.Level, level, _def.DefaultLevel);
            image.gameObject.SetActive(look.Visible);
            var b = (float)look.Brightness;
            image.color = new Color(b, b, b, (float)look.Opacity);
        }
    }

    private void RenderFloors(IReadOnlyList<FloorDraw> floors)
    {
        var key = string.Join(",", floors.Select(f => $"{f.Level}:{f.Name}:{f.Selected}:{f.Dot}"));
        if (key == _floorsKey && _floorButtons.Count == floors.Count) return;
        ClearFloors();
        _floorsKey = key;
        foreach (var f in floors)
        {
            var go = UnityEngine.Object.Instantiate(_floorTemplate, _floors, false);
            _floorButtons.Add(go);
            go.SetActive(true);
            go.name = "Floor_" + f.Level;
            var label = UiKit.Get<TMP_Text>(go.transform, UiPaths.FloorLabel);
            label.text = f.Name;
            label.color = f.Selected ? FloorTextOn : _floorText;
            UiKit.Child(go.transform, UiPaths.FloorSelected).gameObject.SetActive(f.Selected);
            UiKit.Child(go.transform, UiPaths.FloorDot).gameObject.SetActive(f.Dot);
            var pick = f.Level;
            go.GetComponent<Button>().onClick.AddListener(() => UiKit.Guard("map floor", () =>
            {
                BriefingState.MapLevel = pick;
                _onChanged();
            }));
        }
    }

    private void ClearFloors()
    {
        foreach (var go in _floorButtons) if (go != null) UnityEngine.Object.Destroy(go);
        _floorButtons.Clear();
        _floorsKey = null;
    }

    // 기본 층 viewBox 비율로 칸 안에 꽉 차게. Content 는 pivot 이 왼쪽 위라 확대도 웹처럼 왼쪽 위 기준이다.
    private void Fit()
    {
        if (_def == null || _content == null) return;
        var box = MapProjection.DefaultLayer(_def).ViewBox;
        var area = _viewport.rect.size;
        if (area.x <= 0 || area.y <= 0) return;
        var ratio = (float)(box.Width / box.Height);
        var width = Mathf.Min(area.x, area.y * ratio);
        _content.sizeDelta = new Vector2(width, width / ratio);
        ApplyZoom();
    }

    private void SetZoom(MapViewport v)
    {
        BriefingState.MapZoom = v;
        ApplyZoom();
    }

    private void ApplyZoom()
    {
        var v = BriefingState.MapZoom;
        var size = _content.sizeDelta;
        _content.anchoredPosition = new Vector2(-size.x / 2 + (float)v.X, size.y / 2 - (float)v.Y);
        _content.localScale = new Vector3((float)v.Scale, (float)v.Scale, 1f);
        _overlays.SetScale(v.Scale);
    }

    // 커서 아래 지점을 고정한 채 1.25배 또는 0.8배.
    private void OnWheel(PointerEventData e)
    {
        if (_def == null || e.scrollDelta.y == 0 || !ToCanvas(e.position, out var c)) return;
        SetZoom(MapProjection.ZoomAt(BriefingState.MapZoom, e.scrollDelta.y > 0 ? 1.25 : 0.8, c.x, c.y));
    }

    // +/- 버튼은 칸 가운데 기준.
    private void ZoomAtCenter(double factor)
    {
        if (_def == null) return;
        var size = _content.sizeDelta;
        SetZoom(MapProjection.ZoomAt(BriefingState.MapZoom, factor, size.x / 2, size.y / 2));
    }

    private void OnDragStarted(PointerEventData e)
    {
        _dragReady = ToCanvas(e.position, out _dragFrom); // 못 잡으면 이전 시작점으로 튀지 않게 이번 끌기는 무시한다
        if (_dragReady) _dragView = BriefingState.MapZoom;
    }

    private void OnDragged(PointerEventData e)
    {
        if (!_dragReady || _def == null || !ToCanvas(e.position, out var c)) return;
        SetZoom(new MapViewport(_dragView.Scale, _dragView.X + (c.x - _dragFrom.x), _dragView.Y + (c.y - _dragFrom.y)));
    }

    // 화면 좌표를 웹 MapCanvas 의 (cx, cy) 로. 1배 Content 의 왼쪽 위가 원점이고 오른쪽·아래로 커진다.
    // Viewport 는 pivot 이 가운데라 1배 Content 의 왼쪽 위는 (-w/2, h/2) 다.
    private bool ToCanvas(Vector2 screen, out Vector2 c)
    {
        c = default;
        var parent = _viewport.GetComponentInParent<Canvas>();
        var canvas = parent == null ? null : parent.rootCanvas;
        var cam = canvas == null || canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_viewport, screen, cam, out var local)) return false;
        var size = _content.sizeDelta;
        c = new Vector2(local.x + size.x / 2, size.y / 2 - local.y);
        return true;
    }

    private void ShowMessage(string text)
    {
        _message.gameObject.SetActive(text != null);
        _message.text = text ?? "";
    }
}
