using System;
using Briefing.Core.Screen;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Briefing.Ui;

// 열 사이 간격에 끌기 손잡이 두 개를 런타임에 만든다. 끌면 ColLeft·ColRight 의 preferredWidth 가 바뀌고 남는 폭은 지도 칸이 가진다.
// 지도는 Viewport 크기가 바뀌면 MapInput 이 알아서 다시 맞추므로 여기서 부르지 않는다.
// 폭은 끌기를 놓을 때 BepInEx 설정에 저장한다. 계산은 ColumnWidths 가 한다.
internal sealed class ColumnResize
{
    private readonly RectTransform _columns;
    private readonly LayoutElement _left;
    private readonly LayoutElement _right;

    // 경로가 없으면 예외. 호출하는 쪽이 Guard 로 감싼다.
    internal ColumnResize(Transform screenRoot)
    {
        _columns = (RectTransform)UiKit.Child(screenRoot, UiPaths.Columns);
        _left = UiKit.Get<LayoutElement>(screenRoot, UiPaths.ColLeft);
        _right = UiKit.Get<LayoutElement>(screenRoot, UiPaths.ColRight);
        // 자식 때문에 왼쪽 열이 flexible 이 되어 남는 폭을 지도와 반씩 나눠 가진다. 남는 폭은 지도만 갖게 한다.
        _left.flexibleWidth = 0f;
        _right.flexibleWidth = 0f;
        // 손잡이는 열 가장자리 바깥 간격 위에 둔다. 열의 자식이라 열을 따라 움직인다.
        MakeHandle((RectTransform)_left.transform, true);
        MakeHandle((RectTransform)_right.transform, false);
        Apply();
    }

    // 처음과 탭을 다시 열 때 설정 값을 적용한다. 이상한 값은 ColumnWidths 가 고친다.
    internal void Apply()
    {
        var saved = (Plugin.LeftColumnWidth?.Value ?? 0f, Plugin.RightColumnWidth?.Value ?? 0f);
        var (left, right) = ColumnWidths.Resolve(saved.Item1, saved.Item2, Total());
        _left.preferredWidth = left;
        _right.preferredWidth = right;
    }

    // 탭이 막 켜져 레이아웃 전이면 캔버스를 한 번 갱신해 본다. 그래도 모르면 0 이고 지도 칸 제한을 건너뛴다.
    private float Total()
    {
        var w = _columns.rect.width;
        if (w > 0) return w;
        Canvas.ForceUpdateCanvases();
        return _columns.rect.width;
    }

    private void MakeHandle(RectTransform column, bool isLeft)
    {
        var go = new GameObject(isLeft ? "ResizeHandleLeft" : "ResizeHandleRight", typeof(RectTransform));
        var rt = (RectTransform)go.transform;
        rt.SetParent(column, false);
        go.AddComponent<LayoutElement>().ignoreLayout = true; // 레이아웃 그룹에 끼지 않게
        var gap = ColumnWidths.Gap;
        rt.anchorMin = new Vector2(isLeft ? 1f : 0f, 0f);
        rt.anchorMax = new Vector2(isLeft ? 1f : 0f, 1f);
        rt.pivot = new Vector2(isLeft ? 0f : 1f, 0.5f);
        rt.sizeDelta = new Vector2(gap, 0f);
        rt.anchoredPosition = Vector2.zero;
        var image = go.AddComponent<Image>();
        image.color = Idle;
        image.raycastTarget = true;
        var handle = go.AddComponent<ColumnHandle>();
        handle.Image = image;
        handle.Columns = _columns;
        handle.DragStarted = () => (_left.preferredWidth, _right.preferredWidth);
        handle.Dragged = (start, dx) =>
        {
            if (isLeft) _left.preferredWidth = ColumnWidths.DragLeft(start.Item1, start.Item2, _columns.rect.width, dx);
            else _right.preferredWidth = ColumnWidths.DragRight(start.Item1, start.Item2, _columns.rect.width, dx);
        };
        handle.DragEnded = () =>
        {
            // 끄는 중에는 파일을 쓰지 않고 놓을 때 끈 쪽 열만 저장한다.
            if (isLeft) Plugin.LeftColumnWidth.Value = _left.preferredWidth;
            else Plugin.RightColumnWidth.Value = _right.preferredWidth;
        };
        handle.DoubleClicked = () =>
        {
            // 더블클릭하면 그쪽 열을 기본 폭으로. 저장 값 0 이 기본 폭이다.
            if (isLeft) Plugin.LeftColumnWidth.Value = 0f;
            else Plugin.RightColumnWidth.Value = 0f;
            Apply();
        };
        go.transform.SetAsLastSibling();
    }

    // 평소에는 거의 투명하고 마우스를 올리면 옅게 밝아진다.
    internal static readonly Color Idle = new Color(1f, 1f, 1f, 0.02f);
    internal static readonly Color Hover = new Color(1f, 1f, 1f, 0.22f);
}

// 손잡이에 런타임에 붙는다. 마우스 올림, 왼쪽 끌기, 더블클릭을 받는다.
internal sealed class ColumnHandle : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerClickHandler
{
    internal Image Image;
    internal RectTransform Columns; // 끄는 동안 움직이지 않는 부모라 좌표 기준으로 쓴다
    internal Func<(float, float)> DragStarted; // 시작할 때의 (왼쪽 폭, 오른쪽 폭)
    internal Action<(float, float), float> Dragged; // (시작 폭, 오른쪽으로 끈 거리)
    internal Action DragEnded;
    internal Action DoubleClicked;

    private bool _over;
    private bool _dragging;
    private bool _ready;
    private float _startX;
    private (float, float) _startWidths;

    public void OnPointerEnter(PointerEventData e)
    {
        _over = true;
        Paint();
    }

    public void OnPointerExit(PointerEventData e)
    {
        _over = false;
        Paint();
    }

    public void OnBeginDrag(PointerEventData e)
    {
        _dragging = e.button == PointerEventData.InputButton.Left;
        _ready = false;
        if (!_dragging) return;
        UiKit.Guard("column drag start", () =>
        {
            _ready = LocalX(e, out _startX); // 못 잡으면 이번 끌기는 무시한다
            if (_ready) _startWidths = DragStarted();
            Paint();
        });
    }

    public void OnDrag(PointerEventData e)
    {
        if (!_dragging || !_ready) return;
        UiKit.Guard("column drag", () =>
        {
            if (LocalX(e, out var x)) Dragged(_startWidths, x - _startX);
        });
    }

    public void OnEndDrag(PointerEventData e)
    {
        var wasReady = _dragging && _ready;
        _dragging = false;
        _ready = false;
        UiKit.Guard("column drag end", () =>
        {
            Paint();
            if (wasReady) DragEnded();
        });
    }

    public void OnPointerClick(PointerEventData e)
    {
        if (e.button == PointerEventData.InputButton.Left && e.clickCount == 2 && DoubleClicked != null)
            UiKit.Guard("column reset", DoubleClicked);
    }

    private void Paint()
    {
        if (Image != null) Image.color = _over || _dragging ? ColumnResize.Hover : ColumnResize.Idle;
    }

    // 화면 좌표를 Columns 안의 x 로. MapView.ToCanvas 와 같은 방식이다.
    private bool LocalX(PointerEventData e, out float x)
    {
        x = 0f;
        var parent = Columns.GetComponentInParent<Canvas>();
        var canvas = parent == null ? null : parent.rootCanvas;
        var cam = canvas == null || canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(Columns, e.position, cam, out var local)) return false;
        x = local.x;
        return true;
    }
}
