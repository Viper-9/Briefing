using System;
using Briefing.Core.Fetch;

namespace Briefing.Core.Screen;

// 왼쪽 열 위 상태 줄. Text 가 null 이면 숨긴다. ShowList 가 false 면 목록 대신 안내만 보인다.
public sealed class StatusLine
{
    private StatusLine(string text, bool showRetry, bool showList)
    {
        Text = text;
        ShowRetry = showRetry;
        ShowList = showList;
    }

    public string Text { get; }
    public bool ShowRetry { get; }
    public bool ShowList { get; }

    // 레이드 중에는 서버에 요청하지 않으므로 다시 시도 버튼이 없다.
    public static StatusLine For(BriefingCache cache, bool loading, DateTimeOffset now, bool inRaid = false)
    {
        var last = cache.LastResult;
        var hasData = cache.Last != null;

        if (inRaid) return InRaid(last, hasData, loading);

        // 성공한 적이 있으면 새로 받는 중에도 아무것도 띄우지 않는다. 응답이 0.2초쯤이라 매번 깜빡이면 거슬린다.
        if (last != null && last.Kind == FetchKind.Ok) return new StatusLine(null, false, true);
        if (loading) return new StatusLine(Texts.Loading, false, hasData);
        if (last == null) return new StatusLine(Texts.LoadFailed, true, false);

        switch (last.Kind)
        {
            case FetchKind.QuestCodexMissing: return new StatusLine(Texts.QuestCodexMissing, false, false);
            case FetchKind.QuestCodexOutdated: return new StatusLine(Texts.QuestCodexOutdated, false, false);
            case FetchKind.BriefingOutdated: return new StatusLine(Texts.BriefingOutdated, false, false);
            default:
                return hasData
                    ? new StatusLine(Age(cache.LastSuccessAt.Value, now), true, true)
                    : new StatusLine(Texts.LoadFailed, true, false);
        }
    }

    // 메뉴에서 받은 응답이 있으면 그것으로 목록을 그리고 "레이드 전 데이터" 안내를 띄운다.
    // 없으면 목록 없이 안내만 띄운다. QuestCodex 가 없거나 버전이 안 맞으면 그 안내가 더 쓸모 있어서 그쪽을 띄운다.
    private static StatusLine InRaid(FetchResult last, bool hasData, bool loading)
    {
        if (hasData) return new StatusLine(Texts.RaidData, false, true);
        if (loading) return new StatusLine(Texts.Loading, false, false);
        switch (last?.Kind)
        {
            case FetchKind.QuestCodexMissing: return new StatusLine(Texts.QuestCodexMissing, false, false);
            case FetchKind.QuestCodexOutdated: return new StatusLine(Texts.QuestCodexOutdated, false, false);
            case FetchKind.BriefingOutdated: return new StatusLine(Texts.BriefingOutdated, false, false);
            default: return new StatusLine(Texts.RaidNoData, false, false);
        }
    }

    private static string Age(DateTimeOffset at, DateTimeOffset now)
    {
        var minutes = (int)Math.Floor((now - at).TotalMinutes);
        return minutes < 1 ? Texts.JustNow : Texts.MinutesAgo(minutes);
    }
}
