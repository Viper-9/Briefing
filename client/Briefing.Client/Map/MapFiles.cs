using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Briefing.Core.Map;
using Briefing.Net;

namespace Briefing.Map;

// map.json 과 층 SVG 를 BriefingClient.SendAsync 로 받아 게임을 끌 때까지 메모리에 둔다.
// 진행 중인 요청에는 합류하고, 실패로 끝난 것은 다음에 다시 받는다. 정적 파일이라 레이드 중에도 받는다.
// 메인 스레드에서만 부른다.
internal static class MapFiles
{
    private static readonly Dictionary<string, Task<MapDef>> Defs = new Dictionary<string, Task<MapDef>>();
    private static readonly Dictionary<string, Task<string>> Svgs = new Dictionary<string, Task<string>>();

    // 못 받았거나 쓸 수 없는 정의면 null. 예외로 끝나지 않는다.
    internal static Task<MapDef> DefAsync(string folder) => Cached(Defs, folder, () => FetchDefAsync(folder));

    internal static Task<string> SvgAsync(string folder, string svg) =>
        Cached(Svgs, folder + "/" + svg, () => FetchAsync(MapRequest.SvgPath(folder, svg)));

    private static Task<T> Cached<T>(Dictionary<string, Task<T>> cache, string key, Func<Task<T>> start) where T : class
    {
        if (cache.TryGetValue(key, out var task) && (!task.IsCompleted || (task.Status == TaskStatus.RanToCompletion && task.Result != null))) return task; // 실패나 null 로 끝난 것은 다시 받는다
        task = start();
        cache[key] = task;
        return task;
    }

    private static async Task<MapDef> FetchDefAsync(string folder)
    {
        var json = await FetchAsync(MapRequest.DefPath(folder));
        if (json == null) return null;
        var def = MapDefParser.Parse(json);
        if (def == null) Log.Warn($"map.json of {folder} is not a usable map definition");
        return def;
    }

    private static async Task<string> FetchAsync(string path)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            var (status, body) = await BriefingClient.SendAsync(path);
            if (status == 200)
            {
                Log.Info($"map file {path}: {body.Length} chars in {sw.ElapsedMilliseconds} ms");
                return body;
            }
            Log.Warn($"map file {path}: http {status}");
        }
        catch (OperationCanceledException)
        {
            Log.Warn($"map file {path}: timed out after {BriefingClient.Timeout.TotalSeconds:0}s");
        }
        catch (Exception e)
        {
            Log.Warn($"map file {path} failed: {e.GetType().Name}: {e.Message}");
        }
        return null;
    }
}
