using System;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Briefing.Core.Contract;

public enum ParseOutcome
{
    Ok,
    NotBriefing,        // JSON 이 아니거나, 객체가 아니거나, schemaVersion 이 정수가 아니거나, 모양이 계약과 다름
    QuestCodexOutdated, // schemaVersion < 지원 버전
    BriefingOutdated,   // schemaVersion > 지원 버전
}

public sealed class ParseResult
{
    public ParseResult(ParseOutcome outcome, int? schemaVersion, BriefingResponse response)
    {
        Outcome = outcome;
        SchemaVersion = schemaVersion;
        Response = response;
    }

    public ParseOutcome Outcome { get; }
    public int? SchemaVersion { get; }
    public BriefingResponse Response { get; } // Ok 일 때만
}

// schemaVersion 을 먼저 보고 지원하는 버전일 때만 모델로 바꾼다. 어떤 입력에도 예외를 던지지 않는다.
public static class BriefingParser
{
    public const int SupportedSchemaVersion = 1;

    private static readonly JsonSerializer Serializer = JsonSerializer.Create(new JsonSerializerSettings
    {
        MissingMemberHandling = MissingMemberHandling.Ignore, // 모르는 필드는 무시
        NullValueHandling = NullValueHandling.Ignore,         // JSON null 은 건너뛰어 기본값(빈 컬렉션)을 지킨다
        ObjectCreationHandling = ObjectCreationHandling.Replace,
    });

    public static ParseResult Parse(string json)
    {
        var root = ReadObject(json);
        var version = root?["schemaVersion"];
        if (version == null || version.Type != JTokenType.Integer) return new ParseResult(ParseOutcome.NotBriefing, null, null);

        var v = version.Value<int>();
        if (v < SupportedSchemaVersion) return new ParseResult(ParseOutcome.QuestCodexOutdated, v, null);
        if (v > SupportedSchemaVersion) return new ParseResult(ParseOutcome.BriefingOutdated, v, null);
        try
        {
            return new ParseResult(ParseOutcome.Ok, v, root.ToObject<BriefingResponse>(Serializer));
        }
        catch (Exception)
        {
            // maps 가 배열이 아닌 경우 등. 변환 예외 종류가 여럿이라 전부 받는다.
            return new ParseResult(ParseOutcome.NotBriefing, v, null);
        }
    }

    // 서버 오류 본문 {"error":"<코드>", …} 의 코드. 그런 모양이 아니면 null.
    public static string ErrorCode(string json)
    {
        var error = ReadObject(json)?["error"];
        return error != null && error.Type == JTokenType.String ? error.Value<string>() : null;
    }

    private static JObject ReadObject(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            // generatedAt 의 오프셋을 잃지 않게 DateTimeOffset 으로 읽는다.
            using var reader = new JsonTextReader(new StringReader(json)) { DateParseHandling = DateParseHandling.DateTimeOffset };
            return JToken.ReadFrom(reader) as JObject;
        }
        catch (Exception)
        {
            return null;
        }
    }
}
