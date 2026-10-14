using System.Linq;
using Briefing.Core.Contract;

namespace Briefing.Tests;

// 지도가 쓰는 응답 필드: 목표 areas, maps[].exits, maps[].lockedDoors.
public class MapContractParsingTests
{
    private static readonly BriefingResponse Active = FixtureFiles.Load(FixtureFiles.Active, "kr");

    [Fact]
    public void Reads_area_shape()
    {
        var area = Active.Maps[0].Quests[0].Objectives.Single().Locations.Single().Areas.Single(); // Provide Viewership

        Assert.Equal(275.33, area.Center.X);
        Assert.Equal(2.17, area.Center.Y);
        Assert.Equal(-43.3, area.Center.Z);
        Assert.Equal(31.94, area.SizeX);
        Assert.Equal(73.07, area.SizeZ);
        Assert.Equal(11.44, area.Yaw);
        Assert.Equal(-0.2, area.MinY);
        Assert.Equal(17.41, area.MaxY);
    }

    [Fact]
    public void Reads_exits_in_server_order()
    {
        var exits = Active.Maps[0].Exits; // 세관(잘린 사본: 앞 2개)

        Assert.Equal(new[] { "EXFIL_ZB013", "Dorms V-Ex" }, exits.Select(e => e.Key));
        var zb = exits[0];
        Assert.Equal("ZB-013", zb.Name);
        Assert.Equal("pmc", zb.Kind);
        Assert.Equal(200.98, zb.Position.X);
        Assert.Equal(-1.15, zb.Position.Y);
        Assert.Equal(-153.09, zb.Position.Z);
        Assert.Null(zb.Requirement);
        Assert.Equal("switch", zb.RequirementKind);
        Assert.Null(zb.Chance);
        Assert.Null(zb.Target);
        Assert.Equal(50d, exits[1].Chance);
        Assert.NotNull(exits[1].Requirement);
    }

    [Fact]
    public void Reads_locked_doors()
    {
        var door = Active.Maps[0].LockedDoors[0];

        Assert.Equal("5448ba0b4bdc2d02308b456c", door.KeyTpl);
        Assert.Equal("Factory emergency exit key", door.KeyName);
        Assert.Equal("door", door.Kind);
        Assert.Equal(198.93, door.Position.X);
        Assert.Null(door.Code);
        Assert.Equal(2, Active.Maps[0].LockedDoors.Count);
    }

    [Fact]
    public void Reads_transit_and_keypad_fields()
    {
        const string json = @"{""schemaVersion"":1,""maps"":[{""key"":""icebreaker"",
            ""exits"":[{""key"":""9"",""name"":"""",""kind"":""transit"",""position"":{""x"":1,""y"":2,""z"":3},""target"":""rezervbase"",""chance"":12.5}],
            ""lockedDoors"":[{""keyTpl"":""k"",""keyName"":""Keypad"",""kind"":""keypad"",""position"":{""x"":4,""y"":5,""z"":6},""code"":""2024""}]}]}";

        var map = BriefingParser.Parse(json).Response.Maps[0];

        Assert.Equal("rezervbase", map.Exits[0].Target);
        Assert.Equal(12.5, map.Exits[0].Chance);
        Assert.Equal("2024", map.LockedDoors[0].Code);
        Assert.Equal(6d, map.LockedDoors[0].Position.Z);
    }

    [Fact]
    public void Nulls_and_missing_map_fields_become_safe_defaults()
    {
        const string json = @"{""schemaVersion"":1,""maps"":[
            {""key"":""a"",""exits"":null,""lockedDoors"":null},
            {""key"":""b""},
            {""key"":""c"",""exits"":[{""key"":""x"",""position"":null,""chance"":null,""future"":1}],""lockedDoors"":[{""keyTpl"":""k""}],
             ""quests"":[{""questId"":""q"",""objectives"":[{""conditionId"":""o"",""locations"":[{""map"":""c"",""areas"":[{""sizeX"":1},{""center"":{""x"":1,""y"":2,""z"":3}}]}]}]}]}]}";

        var maps = BriefingParser.Parse(json).Response.Maps;

        Assert.Empty(maps[0].Exits);
        Assert.Empty(maps[0].LockedDoors);
        Assert.Empty(maps[1].Exits);
        Assert.Empty(maps[1].LockedDoors);
        Assert.Null(maps[2].Exits[0].Position);
        Assert.Null(maps[2].Exits[0].Chance);
        Assert.Null(maps[2].LockedDoors[0].Position);
        var areas = maps[2].Quests[0].Objectives[0].Locations[0].Areas;
        Assert.Null(areas[0].Center);
        Assert.Equal(1d, areas[0].SizeX);
        Assert.Null(areas[1].MinY);
        Assert.Null(areas[1].MaxY);
        Assert.Equal(0d, areas[1].Yaw);
    }
}
