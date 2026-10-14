using System;
using EFT.Quests;
using EFT.UI.DragAndDrop;
using UnityEngine;

namespace Briefing.Game;

// 게임 내장 아이콘.
// 손 아이콘은 QuestListItem 이 _typeIcon 에 쓰는 퀘스트 종류 아이콘 중 PickUp 이다(UI Fixes 모드가 쓰는 그림과 같지만 그 모드는 필요 없다).
// 못 찾으면 아이콘만 빠진다.
internal static class GameIcons
{
    private static Sprite _hand;
    private static bool _handFailed;

    internal static Sprite Hand()
    {
        if (_hand != null || _handFailed) return _hand;
        try
        {
            _hand = EFTHardSettings.Instance.StaticIcons.QuestIconTypeSprites[EQuestIconType.PickUp];
            if (_hand == null) throw new InvalidOperationException("quest icon sprite is null");
        }
        catch (Exception e)
        {
            _handFailed = true;
            Log.Warn($"briefing hand icon unavailable: {e.Message}");
        }
        return _hand;
    }

    // 칸 오른쪽 아래의 FIR 동그라미와 퀘스트 아이템 아이콘. 보관함 격자와 같은 그림을 QuestItemViewPanel 의
    // _foundInRaidSprite·_questItemSprite 에서 가져온다. 아이템 뷰가 아직 없을 수 있어서 못 찾으면 다음 그리기 때 다시 찾되,
    // 칸마다 FindObjectsOfTypeAll 을 부르지 않도록 그리기 한 번에 한 번만 찾는다.
    private static Sprite _foundInRaid;
    private static Sprite _questItem;
    private static bool _searchedThisRender;
    private static bool _warned;
    private static bool _loggedFound;
    // 하나가 끝내 없으면 그리기마다 FindObjectsOfTypeAll 이 돌기 때문에 20번에서 그만 찾는다. 못 찾은 쪽은 대체 표시를 쓴다.
    private const int MaxSearches = 20;
    private static int _searches;

    // 오른쪽 열을 그릴 때 칸을 만들기 전에 한 번 부른다.
    internal static void BeginRender() => _searchedThisRender = false;

    internal static Sprite FoundInRaid() => Marks(true);

    internal static Sprite QuestItem() => Marks(false);

    private static Sprite Marks(bool foundInRaid)
    {
        if (_foundInRaid == null || _questItem == null) Search();
        return foundInRaid ? _foundInRaid : _questItem;
    }

    private static void Search()
    {
        if (_searchedThisRender || _searches >= MaxSearches) return;
        _searchedThisRender = true;
        _searches++;
        try
        {
            foreach (var panel in Resources.FindObjectsOfTypeAll<QuestItemViewPanel>())
            {
                if (panel == null) continue;
                if (_foundInRaid == null) _foundInRaid = panel._foundInRaidSprite;
                if (_questItem == null) _questItem = panel._questItemSprite;
                if (_foundInRaid != null && _questItem != null) break;
            }
        }
        catch (Exception e)
        {
            Log.Warn($"briefing cell mark sprites lookup failed: {e.Message}");
        }
        if (_foundInRaid == null || _questItem == null)
        {
            if (!_warned)
            {
                _warned = true;
                Log.Warn($"briefing cell mark sprites not found yet (fir {_foundInRaid != null}, quest item {_questItem != null}) — falling back");
            }
            if (_searches >= MaxSearches) Log.Warn($"briefing cell mark sprites: giving up after {MaxSearches} lookups");
        }
        else if (!_loggedFound)
        {
            _loggedFound = true;
            Log.Info($"briefing cell mark sprites: fir {_foundInRaid.name} {_foundInRaid.rect.width}x{_foundInRaid.rect.height}, " +
                     $"quest item {_questItem.name} {_questItem.rect.width}x{_questItem.rect.height}");
        }
    }
}
