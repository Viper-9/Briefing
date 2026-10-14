using System;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Briefing.Core.Fetch;
using EFT;
using SPT.Common.Http;
using SPT.Common.Utils;

namespace Briefing.Net;

// RequestHandler.GetJsonAsync 는 2xx 가 아니면 본문을 버리고, 4번까지 재시도하며, 시간 제한이 없다.
// 그러면 404 본문으로 QuestCodex 가 없는 것과 profileNotFound 를 가를 수 없어서, SPT 의 HttpClient 와
// CreateNewHttpRequest(세션 쿠키)로 직접 한 번 보내고 시간 제한을 건다.
// 메인 스레드에서만 부른다. await 뒤도 메인 스레드라 Cache 를 한 스레드에서만 만진다.
internal static class BriefingClient
{
    // 첫 요청은 서버가 언어별 카탈로그를 새로 만들어 15~20초까지 걸린다.
    internal static readonly TimeSpan Timeout = TimeSpan.FromSeconds(20);
    internal static readonly BriefingCache Cache = new BriefingCache();

    private static Task<FetchResult> _inFlight;

    internal static bool Loading => _inFlight != null && !_inFlight.IsCompleted;

    // 레이드 중에는 새로 요청하지 않고 이미 나간 요청만 기다린다.
    internal static Task<FetchResult> Pending => Loading ? _inFlight : null;

    // 탭을 빠르게 껐다 켜도 요청은 하나만 나간다.
    internal static Task<FetchResult> RefreshAsync()
    {
        if (Loading) return _inFlight;
        _inFlight = RunAsync();
        return _inFlight;
    }

    private static async Task<FetchResult> RunAsync()
    {
        FetchResult result;
        var lang = "?";
        try
        {
            lang = BriefingRequest.LangFor(LocalizationManager.Instance.Culture);
            var path = BriefingRequest.PathFor(RequestHandler.SessionId, lang);
            var (status, body) = await SendAsync(path);
            // 280 KB 파싱은 수십 ms 걸려서 스레드 풀에서 한다. Core 는 Unity 를 만지지 않는다.
            result = await Task.Run(() => FetchClassifier.Classify(status, body));
            if (result.Kind == FetchKind.Ok)
                Log.Info($"briefing ok: lang {lang}, {result.Response.Maps.Count} maps, {body.Length} chars, questCodex {result.Response.QuestCodexVersion}");
            else
                Log.Warn($"briefing {result.Kind} ({result.Detail}): lang {lang}, http {status}, body {Scrub(Head(body))}");
        }
        catch (OperationCanceledException)
        {
            result = FetchResult.Failed("timeout");
            Log.Warn($"briefing timed out after {Timeout.TotalSeconds:0}s (lang {lang})");
        }
        catch (Exception e)
        {
            result = FetchResult.Failed($"{e.GetType().Name}: {e.Message}");
            Log.Warn($"briefing request failed (lang {lang}): {Scrub(e.ToString())}");
        }
        Cache.Record(result, DateTimeOffset.UtcNow);
        return result;
    }

    // MapFiles 도 이것으로 지도 파일을 받는다.
    internal static async Task<(int status, string body)> SendAsync(string path)
    {
        using var cts = new CancellationTokenSource(Timeout);
        using var request = RequestHandler.HttpClient.CreateNewHttpRequest(HttpMethod.Get, path);
        // 기본값 ResponseContentRead 라 본문까지 받아야 끝나므로 시간 제한이 본문 수신까지 걸린다.
        using var response = await RequestHandler.HttpClient.HttpClient.SendAsync(request, cts.Token);
        var bytes = await response.Content.ReadAsByteArrayAsync();
        if (Zlib.IsCompressed(bytes)) bytes = Zlib.Decompress(bytes); // SPT 클라이언트와 같게
        return ((int)response.StatusCode, bytes == null ? "" : Encoding.UTF8.GetString(bytes));
    }

    // 본문이나 예외 문구에 요청 주소가 들어 있을 수 있어 로그에 남기기 전에 세션 ID 를 가린다.
    internal static string Scrub(string text)
    {
        var id = RequestHandler.SessionId;
        return string.IsNullOrEmpty(id) || string.IsNullOrEmpty(text) ? text : text.Replace(id, "<profile>");
    }

    private static string Head(string body) =>
        string.IsNullOrEmpty(body) ? "<empty>" : body.Length <= 200 ? body : body.Substring(0, 200) + "...";
}
