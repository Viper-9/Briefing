using System;

namespace Briefing.Core.Fetch;

// 프로필은 RequestHandler.SessionId, 언어는 LocalizationManager.Culture(예: kr).
// QuestCodex 의 언어 키가 게임 로케일 키와 같아서 그대로 보낸다.
public static class BriefingRequest
{
    public const string FallbackLang = "en";

    public static string LangFor(string culture) =>
        string.IsNullOrWhiteSpace(culture) ? FallbackLang : culture.Trim().ToLowerInvariant();

    public static string PathFor(string profileId, string lang) =>
        "/questcodex/api/briefing?profile=" + Uri.EscapeDataString(profileId ?? "") + "&lang=" + Uri.EscapeDataString(lang);
}
