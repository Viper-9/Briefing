using System;
using Briefing.Core.Screen;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Briefing.Ui;

// 무엇을 보일지는 StatusLine 이 정하고 여기서는 찍기만 한다.
internal sealed class StatusBarView
{
    private readonly GameObject _root;
    private readonly TMP_Text _text;
    private readonly GameObject _retry;

    internal StatusBarView(Transform screenRoot, Action onRetry)
    {
        var status = UiKit.Child(screenRoot, UiPaths.Status);
        _root = status.gameObject;
        _text = UiKit.Get<TMP_Text>(status, UiPaths.StatusText);
        _retry = UiKit.Child(status, UiPaths.StatusRetry).gameObject;
        UiKit.Get<TMP_Text>(status, UiPaths.StatusRetryLabel).text = Texts.Retry;
        UiKit.Get<Button>(status, UiPaths.StatusRetry).onClick.AddListener(() => UiKit.Guard("retry", onRetry));
        _root.SetActive(false);
    }

    internal void Render(StatusLine line)
    {
        _root.SetActive(line.Text != null);
        if (line.Text == null) return;
        _text.text = line.Text;
        _retry.SetActive(line.ShowRetry);
    }
}
