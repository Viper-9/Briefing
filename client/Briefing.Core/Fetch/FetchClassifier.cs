using Briefing.Core.Contract;

namespace Briefing.Core.Fetch;

// 404 는 라우트가 없을 때와 {"error":"profileNotFound"} 두 경우라 본문으로 가른다.
// 그 밖의 오류 코드(missingProfile, unknownLang, noCharacter, briefingBuildFailed)는 모두 Failed 로 두고 코드는 로그에 남긴다.
public static class FetchClassifier
{
    public static FetchResult Classify(int status, string body)
    {
        if (status == 200)
        {
            var parsed = BriefingParser.Parse(body);
            switch (parsed.Outcome)
            {
                case ParseOutcome.Ok: return FetchResult.Ok(parsed.Response);
                case ParseOutcome.QuestCodexOutdated: return FetchResult.Of(FetchKind.QuestCodexOutdated, $"schemaVersion {parsed.SchemaVersion}");
                case ParseOutcome.BriefingOutdated: return FetchResult.Of(FetchKind.BriefingOutdated, $"schemaVersion {parsed.SchemaVersion}");
                default: return FetchResult.Failed("invalidResponse");
            }
        }

        var code = BriefingParser.ErrorCode(body);
        if (status == 404 && code != "profileNotFound") return FetchResult.Of(FetchKind.QuestCodexMissing, "http 404");
        return FetchResult.Failed(code ?? $"http {status}");
    }
}
