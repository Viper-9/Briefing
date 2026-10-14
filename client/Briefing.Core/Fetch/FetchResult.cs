using Briefing.Core.Contract;

namespace Briefing.Core.Fetch;

public enum FetchKind
{
    Ok,
    QuestCodexMissing,  // 404 인데 본문이 profileNotFound 가 아님, 즉 라우트가 없음
    QuestCodexOutdated, // schemaVersion 이 낮음
    BriefingOutdated,   // schemaVersion 이 높음
    Failed,             // 그 밖의 서버 오류, 시간 초과, 연결 실패, 이상한 본문. 이전 응답이 있으면 그걸 보여 준다
}

// Detail 은 로그용이다(서버 오류 코드, "timeout", "http 502" 등).
public sealed class FetchResult
{
    private FetchResult(FetchKind kind, BriefingResponse response, string detail)
    {
        Kind = kind;
        Response = response;
        Detail = detail;
    }

    public FetchKind Kind { get; }
    public BriefingResponse Response { get; } // Ok 일 때만
    public string Detail { get; }

    public static FetchResult Ok(BriefingResponse response) => new FetchResult(FetchKind.Ok, response, null);
    public static FetchResult Of(FetchKind kind, string detail) => new FetchResult(kind, null, detail);
    public static FetchResult Failed(string detail) => new FetchResult(FetchKind.Failed, null, detail);
}
