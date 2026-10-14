using Briefing.Core.Map;
using Briefing.Core.Screen;

namespace Briefing.Tests;

// 지도 칸 문구(층 이름 = QuestCodex 웹 floorName)와 지도 파일 경로.
[Collection("Texts")]
public class MapTextsTests
{
    [Theory]
    [InlineData(-1, null, "지하", "Underground")]
    [InlineData(-2, null, "지하", "Underground")]
    [InlineData(0, null, "지상", "Ground")]
    [InlineData(1, null, "2층", "Floor 2")]
    [InlineData(3, null, "4층", "Floor 4")]
    [InlineData(-3, "deck", "데크 -3", "Deck -3")]
    [InlineData(0, "deck", "데크 0", "Deck 0")]
    [InlineData(10, "deck", "데크 10", "Deck 10")]
    public void Floor_names_follow_the_web_rule(int level, string floorNames, string kr, string en)
    {
        try
        {
            Texts.UseLanguage("kr");
            Assert.Equal(kr, Texts.FloorName(level, floorNames));
            Texts.UseLanguage("en");
            Assert.Equal(en, Texts.FloorName(level, floorNames));
        }
        finally { Texts.UseLanguage("kr"); }
    }

    [Fact]
    public void Map_messages_in_both_languages()
    {
        try
        {
            Texts.UseLanguage("kr");
            Assert.Equal("지도를 불러오는 중...", Texts.MapLoading);
            Assert.Equal("이 맵은 지도가 없습니다", Texts.MapNone);
            Assert.Equal("지도를 불러오지 못했습니다", Texts.MapLoadFailed);
            Assert.Equal("탈출구", Texts.LayerExits);
            Assert.Equal("잠긴 문", Texts.LayerDoors);
            Assert.Equal("퀘스트 구역", Texts.LayerAreas);
            Texts.UseLanguage("en");
            Assert.Equal("Loading map...", Texts.MapLoading);
            Assert.Equal("No map for this location", Texts.MapNone);
            Assert.Equal("Couldn't load the map", Texts.MapLoadFailed);
            Assert.Equal("Extracts", Texts.LayerExits);
            Assert.Equal("Locked doors", Texts.LayerDoors);
            Assert.Equal("Quest zones", Texts.LayerAreas);
        }
        finally { Texts.UseLanguage("kr"); }
    }

    [Fact]
    public void Tab_title_in_both_languages()
    {
        try
        {
            Texts.UseLanguage("kr");
            Assert.Equal("브리핑", Texts.TabTitle);
            Texts.UseLanguage("en");
            Assert.Equal("Briefing", Texts.TabTitle);
        }
        finally { Texts.UseLanguage("kr"); }
    }

    [Fact]
    public void Map_file_paths()
    {
        Assert.Equal("/questcodex/maps/interchange-manimal/map.json", MapRequest.DefPath("interchange-manimal"));
        Assert.Equal("/questcodex/maps/bigmap/Customs-Ground_Level.svg", MapRequest.SvgPath("bigmap", "Customs-Ground_Level.svg"));
        Assert.Equal("/questcodex/maps/a%20b/c%23.svg", MapRequest.SvgPath("a b", "c#.svg"));
    }
}
