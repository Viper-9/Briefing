using EFT.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Briefing.Ui;

// 맵 칩과 오른쪽 열 탭을 게임 상단 탭처럼 반응하게 한다. 마우스를 올리면 옅은 바탕, ButtonOver 소리, 손 커서,
// 누르면 ButtonClick 소리(EFT.UI.ButtonFeedback 과 같다). 바탕은 투명한 targetGraphic 색만 바꾸고,
// 고른 탭의 Selected 바탕은 그 위에 그려지므로 신경 쓰지 않아도 된다. 프리팹에는 붙이지 않는다.
internal sealed class TabFeel : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    private static readonly Color HoverBg = new Color(1f, 1f, 1f, 0.08f);
    // 맵 칩을 누르면 줄이 새로 만들어져 새 칩이 바로 마우스 올림을 받는다. 클릭 직후의 올림 소리는 거른다.
    private const float EnterSoundQuiet = 0.3f;
    private static float _lastClick = -1f;

    private Graphic _bg;
    private Color _idle;
    private bool _hovered;

    internal static void Attach(GameObject go)
    {
        if (go.GetComponent<TabFeel>() != null) return;
        var feel = go.AddComponent<TabFeel>();
        var button = go.GetComponent<Button>();
        feel._bg = button != null ? button.targetGraphic : go.GetComponent<Graphic>();
        if (feel._bg != null) feel._idle = feel._bg.color;
    }

    public void OnPointerEnter(PointerEventData e) => UiKit.Guard("tab hover", () =>
    {
        SetHover(true);
        if (Time.realtimeSinceStartup - _lastClick > EnterSoundQuiet) Play(EUISoundType.ButtonOver);
        CursorSwitcher.SetCursor(ECursorType.Hover);
    });

    public void OnPointerExit(PointerEventData e) => UiKit.Guard("tab hover", () =>
    {
        SetHover(false);
        CursorSwitcher.SetCursor(ECursorType.Idle);
    });

    public void OnPointerClick(PointerEventData e) => UiKit.Guard("tab click", () =>
    {
        if (e.button != PointerEventData.InputButton.Left) return;
        _lastClick = Time.realtimeSinceStartup;
        Play(EUISoundType.ButtonClick);
    });

    // 맵 칩은 누르는 순간 파괴되므로 커서가 손 모양으로 남지 않게 되돌린다.
    private void OnDisable()
    {
        if (!_hovered) return;
        SetHover(false);
        UiKit.Guard("tab cursor", () => CursorSwitcher.SetCursor(ECursorType.Idle));
    }

    private void SetHover(bool on)
    {
        _hovered = on;
        if (_bg != null) _bg.color = on ? HoverBg : _idle;
    }

    private static void Play(EUISoundType sound)
    {
        if (Comfort.Common.Singleton<GUISounds>.Instantiated) Comfort.Common.Singleton<GUISounds>.Instance.PlayUISound(sound);
    }
}
