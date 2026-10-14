using System;
using EFT.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Briefing.Game;

// 게임의 "건네주기" 버튼(QuestObjectiveView._handoverButton)을 복제해 모양, 마우스오버, 소리, 글꼴을 게임과 맞춘다.
// 견본은 게임 프리팹 에셋이라 고치지 않고 복제본만 만진다. 못 찾으면 null 이고 호출하는 쪽이 우리 버튼을 쓴다.
internal static class GameButtons
{
    private static DefaultUIButton _template;
    private static bool _warned;
    private static float _nextSearch; // Time.realtimeSinceStartup 기준

    // 탭을 임무 화면보다 먼저 열면 견본이 아직 메모리에 없을 수 있어서, 못 찾으면 5초마다 다시 찾는다.
    private static DefaultUIButton Template()
    {
        if (_template != null) return _template;
        if (Time.realtimeSinceStartup < _nextSearch) return null;
        _nextSearch = Time.realtimeSinceStartup + 5f;
        try
        {
            foreach (var view in Resources.FindObjectsOfTypeAll<QuestObjectivesView>())
            {
                var button = view != null && view._objectivePrefab != null ? view._objectivePrefab._handoverButton : null;
                if (button != null) { _template = button; return _template; }
            }
            if (!_warned) { _warned = true; Log.Warn("briefing game handover button template not found; using own buttons (will retry)"); }
        }
        catch (Exception e) { Log.Error($"briefing game button lookup failed: {e}"); }
        return null;
    }

    // replaced 자리에 복제본을 넣고 replaced 는 끈다. 실패하면 복제본을 지우고 null.
    internal static DetailButton Clone(Transform parent, GameObject replaced, float width, float height, int maxFontSize)
    {
        GameObject clone = null;
        try
        {
            var template = Template();
            if (template == null) return null;
            clone = UnityEngine.Object.Instantiate(template.gameObject, parent, false);
            clone.name = "Game" + replaced.name;
            clone.transform.SetSiblingIndex(replaced.transform.GetSiblingIndex());
            var le = clone.GetComponent<LayoutElement>();
            if (le == null) le = clone.AddComponent<LayoutElement>();
            le.minWidth = width;
            le.preferredWidth = width;
            le.minHeight = height;
            le.preferredHeight = height;
            var button = clone.GetComponent<DefaultUIButton>();
            if (button == null) throw new InvalidOperationException("template clone has no DefaultUIButton");
            button._fontSize = Math.Min(button._fontSize, maxFontSize);
            button.UpdateFontSize();
            clone.SetActive(true);
            replaced.SetActive(false);
            return new DetailButton(button, null, null);
        }
        catch (Exception e)
        {
            Log.Error($"briefing game button clone failed: {e}");
            if (clone != null) UnityEngine.Object.Destroy(clone);
            return null;
        }
    }
}

// 게임 버튼 복제본이든 우리 버튼이든 같은 방식으로 다루려고 감싼다.
internal sealed class DetailButton
{
    private readonly DefaultUIButton _game;
    private readonly Button _own;
    private readonly TMP_Text _ownLabel;

    internal DetailButton(DefaultUIButton game, Button own, TMP_Text ownLabel)
    {
        _game = game;
        _own = own;
        _ownLabel = ownLabel;
    }

    internal static DetailButton Own(Button own, TMP_Text label) => new DetailButton(null, own, label);

    internal void OnClick(Action action)
    {
        if (_game != null) _game.OnClick.AddListener(() => action());
        else _own.onClick.AddListener(() => action());
    }

    internal bool Interactable
    {
        set { if (_game != null) _game.Interactable = value; else _own.interactable = value; }
    }

    // SetHeaderText 는 로케일 키를 받으므로 글자를 그대로 쓰는 SetRawText 를 쓴다.
    internal void SetText(string text)
    {
        if (_game != null) _game.SetRawText(text, _game._fontSize);
        else _ownLabel.text = text;
    }
}
