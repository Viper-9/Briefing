using System;

namespace Briefing.Core.Map;

// QuestCodex 가 웹에 내주는 지도 파일 경로. mapFolder 는 응답의 maps[].mapFolder(변형이 반영된 폴더),
// svg 는 map.json 층의 파일 이름. 브리핑과 같은 서버(RequestHandler.Host)로 보낸다.
public static class MapRequest
{
    public static string DefPath(string mapFolder) => "/questcodex/maps/" + Uri.EscapeDataString(mapFolder ?? "") + "/map.json";

    public static string SvgPath(string mapFolder, string svg) =>
        "/questcodex/maps/" + Uri.EscapeDataString(mapFolder ?? "") + "/" + Uri.EscapeDataString(svg ?? "");
}
