using System;
using System.Threading.Tasks;
using SPT.Common.Http;

namespace Briefing.Probes;

// 0단계 실측: 클라이언트 → QuestCodex 컨트롤러·정적 파일 도달 여부를 로그로 남긴다.
internal static class ServerProbe
{
    internal static async Task RunAsync()
    {
        Log.Info($"[probe] backend host = {RequestHandler.Host}, session = {RequestHandler.SessionId}");
        await Try("/questcodex/api/ping");
        await Try("/questcodex/maps/bigmap/map.json");
    }

    private static async Task Try(string path)
    {
        try
        {
            var body = await RequestHandler.GetJsonAsync(path);
            var head = body == null ? "<null>" : body.Substring(0, Math.Min(200, body.Length));
            Log.Info($"[probe] GET {path} -> {body?.Length ?? 0} chars: {head}");
        }
        catch (Exception e)
        {
            Log.Warn($"[probe] GET {path} failed: {e.GetType().Name}: {e.Message}");
        }
    }
}
