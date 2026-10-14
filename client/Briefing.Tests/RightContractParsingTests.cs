using System.Linq;
using Briefing.Core.Contract;

namespace Briefing.Tests;

// 오른쪽 열이 쓰는 응답 필드: maps[].bring·find·gear·special, anywhere.bring·gear·special.
public class RightContractParsingTests
{
    private static readonly BriefingResponse Active = FixtureFiles.Load(FixtureFiles.Active, "kr");

    [Fact]
    public void Reads_bring_rows_in_server_order()
    {
        var bring = Active.Maps[0].Bring; // 세관

        Assert.Equal(2, bring.Count);
        var marker = bring[0];
        Assert.Equal("5991b51486f77447b112d44f", marker.Items.Single().Tpl);
        Assert.Equal("MS2000 마커 ", marker.Items.Single().Name); // 서버 이름 그대로(뒤 공백 포함 — 화면이 다듬는다)
        Assert.Equal(1d, marker.Need);
        Assert.Equal(1d, marker.Have);
        Assert.False(marker.Fir);
        Assert.Equal(0d, marker.Missing);
        Assert.Equal("5a27ba1c86f77461ea5a3c56", marker.Sources.Single().QuestId);
        Assert.Equal(1d, marker.Sources.Single().Count);
        Assert.Null(marker.Place);
        Assert.Equal("67d0576f29f580ebc10efd08", bring[1].Items.Single().Tpl); // AK-50 (Hobby Club)
        Assert.Equal(1d, bring[1].Missing);
    }

    [Fact]
    public void Reads_find_rows_with_place_and_alternatives()
    {
        var factory = Active.Maps[1].Find;

        Assert.Equal(new[] { "here", "unknown" }, factory.Select(r => r.Place));
        Assert.Equal("Blood sample", factory[0].Items.Single().Name);
        var wartech = factory[1];
        Assert.Equal(5, wartech.Items.Count);
        Assert.Equal("64a536392d2c4e6e970f4121", wartech.Items[0].Tpl);
        Assert.Equal(50d, wartech.Need);
        Assert.Equal(3d, wartech.Have);
        Assert.True(wartech.Fir);
        Assert.Equal(47d, wartech.Missing);
        Assert.Equal("66151401efb0539ae10875ae", wartech.Sources.Single().QuestId);
        Assert.Equal(50d, wartech.Sources.Single().Count);
    }

    [Fact]
    public void Reads_gear_rows_with_and_groups()
    {
        var gear = Active.Maps[0].Gear.Single();

        Assert.Equal("5c0bde0986f77479cf22c2f8", gear.QuestId); // 천국에서 태어난 저격수
        var row = gear.Rows.Single();
        Assert.Equal("weapon", row.Label);
        Assert.Equal(21, row.Names.Count);
        Assert.Equal(21, row.Tpls.Count);
        Assert.Equal("Accuracy International AXMC .338 LM bolt-action sniper rifle", row.Names[0]);
        Assert.Equal(new[] { "627e14b21713922ded6f2c15" }, row.Tpls[0]);
    }

    [Fact]
    public void Reads_anywhere_gear_and_empty_lists()
    {
        var gear = Active.Anywhere.Gear;

        Assert.Equal(new[] { "5c0bc91486f7746ab41857a2", "666314bafd5ca9577902e03a" }, gear.Select(g => g.QuestId));
        Assert.Equal(new[] { "weapon", "weaponMod", "weapon" }, gear[0].Rows.Select(r => r.Label));
        Assert.Equal(new[] { "5b363dd25acfc4001a598fd2" }, gear[0].Rows[1].Tpls[0]);
        Assert.Empty(Active.Anywhere.Bring);
        Assert.Empty(Active.Anywhere.Special);
        Assert.Empty(Active.Maps[2].Bring); // 미궁
        Assert.Empty(Active.Maps[1].Gear);
    }

    [Fact]
    public void Reads_special_rows()
    {
        var special = Active.Maps[0].Special;

        Assert.Equal(new[] { "5c0d4e61d09282029f53920e", "675c1d6d59b0575973008fc7" }, special.Select(s => s.QuestId));
        var exit = special[0].Rows.Single();
        Assert.Equal("exit", exit.Kind);
        Assert.Equal(new[] { "Survived", "Transit" }, exit.ExitStatuses);
        Assert.Null(exit.ExitName);
        Assert.Equal(new[] { "oneRaid", "exit" }, special[1].Rows.Select(r => r.Kind));
        Assert.Empty(special[1].Rows[0].ExitStatuses);
    }

    [Fact]
    public void Empty_profile_has_empty_lists_and_missing_fields_stay_empty()
    {
        var empty = FixtureFiles.Load(FixtureFiles.Empty, "kr");
        Assert.All(empty.Maps, m =>
        {
            Assert.Empty(m.Bring);
            Assert.Empty(m.Find);
            Assert.Empty(m.Gear);
            Assert.Empty(m.Special);
        });

        // 필드가 없거나 null 이어도 컬렉션은 비어 있을 뿐 null 이 아니다(파서 규칙). 숫자 소수도 그대로.
        var r = BriefingParser.Parse(@"{""schemaVersion"":1,""maps"":[{""key"":""a"",""bring"":null,
            ""find"":[{""items"":null,""need"":1.5,""sources"":null,""place"":""elsewhere""}],
            ""gear"":[{""questId"":""q"",""rows"":[{""label"":""caliber"",""names"":[""all""],""tpls"":[[]]}]}]}],
            ""anywhere"":{}}").Response;
        var map = r.Maps.Single();
        Assert.Empty(map.Bring);
        Assert.Empty(map.Find[0].Items);
        Assert.Empty(map.Find[0].Sources);
        Assert.Equal(1.5, map.Find[0].Need);
        Assert.Equal("elsewhere", map.Find[0].Place);
        Assert.Empty(map.Gear[0].Rows[0].Tpls[0]);
        Assert.Empty(r.Anywhere.Bring);
        Assert.Empty(r.Anywhere.Gear);
    }

    [Fact]
    public void English_snapshot_parses_the_same_shape()
    {
        var en = FixtureFiles.Load(FixtureFiles.Active, "en");

        Assert.Equal(Active.Maps.Select(m => m.Bring.Count), en.Maps.Select(m => m.Bring.Count));
        Assert.Equal(Active.Maps.Select(m => m.Find.Count), en.Maps.Select(m => m.Find.Count));
        Assert.Equal("MS2000 Marker", en.Maps[0].Bring[0].Items[0].Name.Trim());
    }
}
