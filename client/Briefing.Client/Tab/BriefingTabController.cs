using System;
using System.Threading.Tasks;
using Briefing.Ui;
using EFT.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Briefing.Tab;

// 게임 Tab 이 Select/Deselect 때 부르는 컨트롤러. 우리 패널을 켜고 끄기만 한다.
internal sealed class BriefingTabController : ITabController
{
    private readonly GameObject _panel;
    private readonly IBriefingView _view;
    private readonly Action _applyLayout;

    internal BriefingTabController(GameObject panel, IBriefingView view, Action applyLayout = null)
    {
        _applyLayout = applyLayout;
        _panel = panel;
        _view = view;
    }

    public void Show()
    {
        try { _applyLayout?.Invoke(); }
        catch (Exception e) { Log.Error($"briefing layout failed: {e}"); }
        _panel.SetActive(true);
        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)_panel.transform);
        try { _view.OnShown(); }
        catch (Exception e) { Log.Error($"briefing view OnShown failed: {e}"); }
    }

    public Task<bool> TryHide()
    {
        _panel.SetActive(false);
        try { _view.OnHidden(); }
        catch (Exception e) { Log.Error($"briefing view OnHidden failed: {e}"); }
        return Task.FromResult(true);
    }
}
