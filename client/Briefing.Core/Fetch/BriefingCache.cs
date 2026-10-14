using System;
using Briefing.Core.Contract;

namespace Briefing.Core.Fetch;

// 응답을 메모리에만 둔다. 요청이 실패해도 마지막 성공 응답과 그 시각은 남아서
// 상태 줄은 "n분 전 데이터"로, 레이드 안에서는 이 응답으로 그린다.
public sealed class BriefingCache
{
    public BriefingResponse Last { get; private set; }
    public DateTimeOffset? LastSuccessAt { get; private set; }
    public FetchResult LastResult { get; private set; }

    public void Record(FetchResult result, DateTimeOffset now)
    {
        LastResult = result;
        if (result.Kind != FetchKind.Ok) return;
        Last = result.Response;
        LastSuccessAt = now;
    }
}
