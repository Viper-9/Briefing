using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Briefing.Core.Contract;
using Briefing.Core.Map;
using Briefing.Game;

namespace Briefing.Map;

// 첫 지도를 그린 뒤 나머지 맵을 하나씩 받아 Sprite 까지 만들어 둔다(DynamicMaps 의 PrecacheCoroutine 과 같은 생각).
// 게임을 켜 둔 동안 한 번만 돌고, 맵 목록은 시작할 때 정한 그대로 간다. 메인 스레드에서만 부른다.
// 사용자가 같은 층을 열어도 MapFiles·SvgSprites 가 진행 중인 작업에 합류하므로 두 번 만들지 않는다. 실패는 로그만 남긴다.
// 테셀레이션이 CPU 와 메모리를 쓰므로 레이드 중에는 돌지 않는다. 레이드 중에 시작하면 미뤄 두고,
// 도는 중에 레이드에 들어가면 다음 층 앞에서 멈췄다가 메뉴에서 탭을 열 때 이어 간다.
internal static class MapPrecache
{
    private static bool _started;
    private static bool _deferred;         // 레이드 때문에 미뤘거나 멈췄다. 메뉴에서 Resume 이 다시 시작한다
    private static string _deferredFolder; // 미룰 때 받은 currentFolder

    // currentFolder 는 방금 그린 맵의 폴더. 두 번째 호출부터는 아무것도 하지 않는다.
    internal static void Start(BriefingResponse response, string currentFolder)
    {
        if (_started || response == null) return;
        var svg = SvgLoader.Get();
        if (svg == null) return; // 지도 칸이 안내 문구를 띄운다
        if (RaidInfo.InRaid())
        {
            if (!_deferred) Log.Info("map precache deferred: in raid");
            _deferred = true;
            _deferredFolder = currentFolder;
            return;
        }
        _deferred = false;
        _started = true;
        try
        {
            var folders = MapPrecachePlan.Folders(response, currentFolder);
            _ = RunAsync(svg, folders);
        }
        catch (Exception e)
        {
            Log.Error($"map precache start failed: {e}");
        }
    }

    // 메뉴에서 탭을 열 때 부른다.
    internal static void Resume(BriefingResponse response)
    {
        if (!_deferred || response == null || RaidInfo.InRaid()) return;
        Start(response, _deferredFolder);
    }

    // 만든 층은 캐시에 남아 있어 이어 갈 때 건너뛴다.
    private static void Pause(Stopwatch sw, int built)
    {
        _started = false;
        _deferred = true;
        _deferredFolder = null;
        Log.Info($"map precache paused for the raid after {sw.ElapsedMilliseconds} ms ({built} built) — resumes in the menu");
    }

    private static async Task RunAsync(ISvgBackend svg, IReadOnlyList<string> folders)
    {
        var sw = Stopwatch.StartNew();
        int built = 0, skipped = 0, failed = 0;
        try
        {
            Log.Info($"map precache start: {folders.Count} map(s)");
            await Task.Yield(); // 지도 칸 그리기가 끝난 뒤에 돈다
            foreach (var folder in folders)
            {
                try
                {
                    var def = await MapFiles.DefAsync(folder);
                    if (def == null)
                    {
                        failed++;
                        Log.Warn($"map precache {folder}: no map definition, skipped");
                        continue;
                    }
                    foreach (var layer in MapPrecachePlan.LayerOrder(def))
                    {
                        if (RaidInfo.InRaid())
                        {
                            Pause(sw, built);
                            return;
                        }
                        try
                        {
                            if (svg.TryGet(folder, layer.Svg, out _))
                            {
                                skipped++;
                                continue;
                            }
                            var text = await MapFiles.SvgAsync(folder, layer.Svg);
                            await Task.Yield(); // 층마다 적어도 한 프레임은 양보한다
                            var sprite = text == null ? null : await svg.BuildAsync(folder, layer.Svg, text);
                            if (sprite != null) built++;
                            else failed++; // 로그는 MapFiles·SvgSprites 가 남겼다
                        }
                        catch (Exception e)
                        {
                            failed++;
                            Log.Warn($"map precache {folder}/{layer.Svg} failed: {e.GetType().Name}: {e.Message}");
                        }
                    }
                }
                catch (Exception e)
                {
                    failed++;
                    Log.Warn($"map precache {folder} failed: {e.GetType().Name}: {e.Message}");
                }
            }
        }
        catch (Exception e)
        {
            Log.Error($"map precache failed: {e}");
        }
        Log.Info($"map precache done in {sw.ElapsedMilliseconds} ms: {built} built, {skipped} already cached, {failed} failed");
    }
}
