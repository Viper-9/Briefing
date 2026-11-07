using System.Linq;
using Briefing.Core.Contract;
using Briefing.Core.Screen;

namespace Briefing.Tests;

// 보스 말풍선 한 줄(BossLine)과 응답의 bosses 읽기. 문구 사례는 QuestCodex 웹 MapMarkers.test.ts 와 같다.
[Collection("Texts")]
public class BossLineTests
{
    private static BriefingBoss Boss(string key = "bossBully", int mapChance = 30, bool conditional = false, bool rotating = false, string onlyOn = null)
        => new BriefingBoss { Key = key, Kind = "boss", MapChance = mapChance, Conditional = conditional, Rotating = rotating, OnlyOn = onlyOn };

    private static string En(BriefingBoss boss)
    {
        try
        {
            Texts.UseLanguage("en");
            return BossLine.Of(boss);
        }
        finally { Texts.UseLanguage("kr"); }
    }

    [Fact]
    public void Uses_only_the_map_wide_chance()
    {
        Assert.Equal("Reshala 30%", En(Boss()));
        Assert.Equal("르샬라 30%", BossLine.Of(Boss()));
    }

    [Fact]
    public void Cultist_priest_gets_night_except_on_night_factory()
    {
        Assert.Equal("Cultist Priest (night) 30%", En(Boss("sectantPriest")));
        Assert.Equal("Cultist Priest (high level, night) 30%", En(Boss("sectantPriest", onlyOn: "sandbox_high")));
        Assert.Equal("Cultist Priest (night) 30%", En(Boss("sectantPriest", onlyOn: "factory4_night")));
        Assert.Equal("광신도 사제 (야간) 30%", BossLine.Of(Boss("sectantPriest", onlyOn: "factory4_night")));
    }

    [Fact]
    public void Rotating_boss_keeps_the_chance_and_says_rotating()
    {
        Assert.Equal("Goons (rotating) 35%", En(Boss("bossKnight", 35, rotating: true)));
    }

    [Fact]
    public void Conditional_boss_has_no_number()
    {
        Assert.Equal("Reshala · needs a trigger", En(Boss(conditional: true)));
        Assert.Equal("르샬라 · 조건부", BossLine.Of(Boss(conditional: true)));
    }

    [Fact]
    public void Pair_map_label_comes_first()
    {
        Assert.Equal("타길라 (주간) 30%", BossLine.Of(Boss("bossTagilla", onlyOn: "factory4_day")));
        Assert.Equal("군즈 (저레벨, 순환) 30%", BossLine.Of(Boss("bossKnight", rotating: true, onlyOn: "sandbox")));
    }

    [Fact]
    public void Unknown_key_and_unknown_map_stay_as_they_are()
    {
        Assert.Equal("bossModded 12%", BossLine.Of(Boss("bossModded", 12)));
        Assert.Equal("르샬라 30%", BossLine.Of(Boss(onlyOn: "somewhere_else")));
    }

    [Fact]
    public void Russian_line()
    {
        try
        {
            Texts.UseLanguage("ru");
            Assert.Equal("Сектант Жрец (высокий уровень, ночь) 44%", BossLine.Of(Boss("sectantPriest", 44, onlyOn: "sandbox_high")));
            Assert.Equal("Решала · нужно условие", BossLine.Of(Boss(conditional: true)));
        }
        finally { Texts.UseLanguage("kr"); }
    }

    [Fact]
    public void Reads_bosses_from_the_response_and_defaults_to_empty()
    {
        const string json = @"{""schemaVersion"":1,""maps"":[
            {""key"":""factory4_day"",""bosses"":{""zones"":[{""zone"":""BotZone"",""position"":{""x"":1,""y"":2,""z"":3},
                ""bosses"":[{""key"":""bossTagilla"",""kind"":""boss"",""mapChance"":30,""conditional"":false,""rotating"":false,""onlyOn"":null},
                            {""key"":""sectantPriest"",""kind"":""boss"",""mapChance"":20,""conditional"":false,""rotating"":false,""onlyOn"":""factory4_night""}]}],
              ""unplaced"":[{""key"":""bossKnight"",""kind"":""boss"",""mapChance"":35,""conditional"":true,""rotating"":true}]}},
            {""key"":""woods""},
            {""key"":""bigmap"",""bosses"":null}]}";

        var result = BriefingParser.Parse(json);

        Assert.Equal(ParseOutcome.Ok, result.Outcome);
        var factory = result.Response.Maps[0].Bosses;
        var zone = factory.Zones.Single();
        Assert.Equal("BotZone", zone.Zone);
        Assert.Equal(3, zone.Position.Z);
        Assert.Equal(new[] { "bossTagilla", "sectantPriest" }, zone.Bosses.Select(b => b.Key));
        Assert.Null(zone.Bosses[0].OnlyOn);
        Assert.Equal("factory4_night", zone.Bosses[1].OnlyOn);
        var knight = factory.Unplaced.Single();
        Assert.True(knight.Conditional);
        Assert.True(knight.Rotating);
        Assert.Equal(35, knight.MapChance);
        // 예전 QuestCodex(필드 없음)와 null 은 빈 객체
        Assert.Empty(result.Response.Maps[1].Bosses.Zones);
        Assert.Empty(result.Response.Maps[1].Bosses.Unplaced);
        Assert.Empty(result.Response.Maps[2].Bosses.Zones);
    }
}
