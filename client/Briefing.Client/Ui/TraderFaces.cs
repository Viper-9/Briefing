using System;
using System.Threading;
using System.Threading.Tasks;
using EFT;
using UnityEngine;
using UnityEngine.UI;

namespace Briefing.Ui;

// 처음에는 이름 첫 글자를 보이고, GetAndAssignAvatar 가 true 를 돌려주면 얼굴로 바꾼다.
// 다시 그리거나 탭을 닫으면 CancelAll 한다. 게임 쪽은 취소됐거나 Image 가 파괴됐으면 아무것도 하지 않는다(디컴파일로 확인).
internal sealed class TraderFaces
{
    private readonly Func<Profile> _profile;
    private CancellationTokenSource _cts = new CancellationTokenSource();

    internal TraderFaces(Func<Profile> profile) => _profile = profile;

    internal void Assign(string traderId, Image face, GameObject initial)
    {
        face.gameObject.SetActive(false);
        initial.SetActive(true);
        if (string.IsNullOrEmpty(traderId)) return;
        try
        {
            var profile = _profile();
            if (profile == null || !profile.TradersInfo.TryGetValue(traderId, out var info) || info?.Settings == null) return;
            _ = LoadAsync(info.Settings, traderId, face, initial, _cts.Token);
        }
        catch (Exception e)
        {
            Log.Warn($"trader face {traderId} skipped: {e.Message}");
        }
    }

    internal void CancelAll()
    {
        _cts.Cancel();
        _cts.Dispose();
        _cts = new CancellationTokenSource();
    }

    private static async Task LoadAsync(GlobalConfiguration.TraderSettings settings, string traderId, Image face, GameObject initial, CancellationToken token)
    {
        try
        {
            if (!await settings.GetAndAssignAvatar(face, token)) return;
            if (token.IsCancellationRequested || face == null || initial == null) return; // 파괴됐으면 Unity null
            face.gameObject.SetActive(true);
            initial.SetActive(false);
        }
        catch (Exception e)
        {
            Log.Warn($"trader face {traderId} failed: {e.Message}");
        }
    }
}
