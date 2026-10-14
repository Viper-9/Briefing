using System;
using EFT;

namespace Briefing.Game;

// 게임 로케일에서 이름을 찾는다. 키가 없으면 LocalizedValue 가 키를 그대로 돌려주므로 그때는 null.
internal static class GameNames
{
    // 수집을 요구하는 제출 퀘스트는 응답 퀘스트 목록에 없을 때가 많아 이름을 게임에서 찾는다.
    internal static string Quest(string questId)
    {
        try
        {
            if (string.IsNullOrEmpty(questId)) return null;
            var key = questId + " name";
            var name = LocalizationManager.Instance.LocalizedValue(key);
            return string.IsNullOrWhiteSpace(name) || name == key ? null : name;
        }
        catch (Exception) { return null; }
    }
}
