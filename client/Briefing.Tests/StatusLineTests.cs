using System;
using Briefing.Core.Fetch;
using Briefing.Core.Screen;

namespace Briefing.Tests;

public class StatusLineTests
{
    private static readonly DateTimeOffset T0 = new DateTimeOffset(2026, 10, 11, 12, 0, 0, TimeSpan.Zero);

    private static BriefingCache CacheWith(params (FetchResult result, int minute)[] records)
    {
        var cache = new BriefingCache();
        foreach (var (result, minute) in records) cache.Record(result, T0.AddMinutes(minute));
        return cache;
    }

    private static FetchResult Ok() => FetchClassifier.Classify(200, FixtureFiles.Read(FixtureFiles.Active, "kr"));

    [Fact]
    public void First_request_in_flight_shows_loading_without_list()
    {
        var s = StatusLine.For(new BriefingCache(), loading: true, T0);

        Assert.Equal("브리핑을 불러오는 중...", s.Text);
        Assert.False(s.ShowRetry);
        Assert.False(s.ShowList);
    }

    [Fact]
    public void Success_hides_the_bar_even_while_refreshing()
    {
        var cache = CacheWith((Ok(), 0));

        foreach (var loading in new[] { false, true })
        {
            var s = StatusLine.For(cache, loading, T0.AddMinutes(3));
            Assert.Null(s.Text);
            Assert.False(s.ShowRetry);
            Assert.True(s.ShowList);
        }
    }

    [Fact]
    public void Failure_after_success_keeps_list_and_shows_age()
    {
        var cache = CacheWith((Ok(), 0), (FetchResult.Failed("timeout"), 5));

        var s = StatusLine.For(cache, loading: false, T0.AddMinutes(7).AddSeconds(30));

        Assert.Equal("7분 전 데이터", s.Text);
        Assert.True(s.ShowRetry);
        Assert.True(s.ShowList);
    }

    [Fact]
    public void Failure_within_a_minute_of_success_says_just_now()
    {
        var cache = CacheWith((Ok(), 0), (FetchResult.Failed("http 500"), 0));

        Assert.Equal("1분 이내에 받은 데이터", StatusLine.For(cache, false, T0.AddSeconds(59)).Text);
    }

    [Fact]
    public void Failure_without_data_shows_message_and_retry()
    {
        var s = StatusLine.For(CacheWith((FetchResult.Failed("timeout"), 0)), false, T0);

        Assert.Equal("브리핑을 불러오지 못했습니다", s.Text);
        Assert.True(s.ShowRetry);
        Assert.False(s.ShowList);
    }

    [Fact]
    public void Retry_in_flight_after_failure_shows_loading_and_keeps_old_list()
    {
        var cache = CacheWith((Ok(), 0), (FetchResult.Failed("timeout"), 5));

        var s = StatusLine.For(cache, loading: true, T0.AddMinutes(6));

        Assert.Equal("브리핑을 불러오는 중...", s.Text);
        Assert.False(s.ShowRetry);
        Assert.True(s.ShowList);
    }

    [Theory]
    [InlineData(FetchKind.QuestCodexMissing, "QuestCodex가 필요합니다. SPT 서버에 QuestCodex를 설치하세요 (SPT_Runtime/user/mods/QuestCodex)")]
    [InlineData(FetchKind.QuestCodexOutdated, "QuestCodex 1.8.0 이상으로 업데이트하세요")]
    [InlineData(FetchKind.BriefingOutdated, "Briefing을 업데이트하세요. QuestCodex가 더 새로운 응답 형식을 보냅니다")]
    public void Install_and_version_problems_block_the_list(FetchKind kind, string text)
    {
        var cache = CacheWith((Ok(), 0), (FetchResult.Of(kind, "x"), 1));

        var s = StatusLine.For(cache, false, T0.AddMinutes(2));

        Assert.Equal(text, s.Text);
        Assert.False(s.ShowRetry);
        Assert.False(s.ShowList);
    }

    [Fact]
    public void Never_requested_and_idle_offers_retry()
    {
        var s = StatusLine.For(new BriefingCache(), loading: false, T0);

        Assert.Equal("브리핑을 불러오지 못했습니다", s.Text);
        Assert.True(s.ShowRetry);
        Assert.False(s.ShowList);
    }

    // 레이드 중에는 요청하지 않으므로 다시 시도 버튼이 없다.
    [Fact]
    public void In_raid_with_menu_data_says_pre_raid_data_without_retry()
    {
        foreach (var cache in new[] { CacheWith((Ok(), 0)), CacheWith((Ok(), 0), (FetchResult.Failed("timeout"), 5)) })
        {
            var s = StatusLine.For(cache, loading: false, T0.AddMinutes(30), inRaid: true);

            Assert.Equal("레이드 전 데이터 · 진행은 실시간", s.Text);
            Assert.False(s.ShowRetry);
            Assert.True(s.ShowList);
        }
    }

    [Fact]
    public void In_raid_without_menu_data_says_so_without_retry()
    {
        foreach (var cache in new[] { new BriefingCache(), CacheWith((FetchResult.Failed("timeout"), 0)) })
        {
            var s = StatusLine.For(cache, loading: false, T0, inRaid: true);

            Assert.Equal("메뉴에서 브리핑을 받지 못했습니다", s.Text);
            Assert.False(s.ShowRetry);
            Assert.False(s.ShowList);
        }
        Assert.Equal("브리핑을 불러오는 중...", StatusLine.For(new BriefingCache(), loading: true, T0, inRaid: true).Text);
    }

    [Fact]
    public void In_raid_install_problems_keep_their_message()
    {
        var s = StatusLine.For(CacheWith((FetchResult.Of(FetchKind.QuestCodexMissing, "http 404"), 0)), false, T0, inRaid: true);

        Assert.StartsWith("QuestCodex가 필요합니다", s.Text);
        Assert.False(s.ShowRetry);
        Assert.False(s.ShowList);
    }
}
