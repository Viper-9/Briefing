using System;
using System.IO;
using Briefing.Core.Contract;
using Briefing.Core.Map;

namespace Briefing.Tests;

// QuestCodex 응답을 tools/trim-fixture.py 로 줄인 사본. 맵은 bigmap, factory4_day, labyrinth 만 남겼다.
// map.<폴더>.json 은 QuestCodex web/public/maps/<폴더>/map.json 사본이다(웹 mapProjection.test.ts 가 쓰는 두 지도).
internal static class FixtureFiles
{
    internal const string Active = "6a8328d2c1ae327dd00378cf"; // 진행 중 퀘스트 많음
    internal const string Empty = "6ac3d8a02594d2788836a657";  // 진행 중 퀘스트 없음

    internal static string Read(string profile, string lang) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", $"{profile}.{lang}.json"));

    internal static BriefingResponse Load(string profile, string lang)
    {
        var result = BriefingParser.Parse(Read(profile, lang));
        Assert.Equal(ParseOutcome.Ok, result.Outcome);
        return result.Response;
    }

    internal static MapDef LoadMap(string folder)
    {
        var def = MapDefParser.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", $"map.{folder}.json")));
        Assert.NotNull(def);
        return def;
    }
}
