using System;
using System.Collections.Generic;

namespace Briefing.Core.Contract;

// 브리핑 응답(schemaVersion 1) 중 화면이 쓰는 필드만. 모르는 필드는 파서가 무시하니 필요한 필드는 여기에 더하면 된다.
// 컬렉션은 기본값이 있고 파서가 JSON null 을 건너뛰므로 null 이 되지 않는다.
// Unity Mono 에서 쓰려고 record·init 대신 평범한 클래스로 둔다.

public sealed class BriefingResponse
{
    public int SchemaVersion { get; set; }
    public string QuestCodexVersion { get; set; }
    public DateTimeOffset GeneratedAt { get; set; }
    public string Lang { get; set; }
    public Dictionary<string, BriefingTrader> Traders { get; set; } = new Dictionary<string, BriefingTrader>();
    public List<BriefingMap> Maps { get; set; } = new List<BriefingMap>();
    public BriefingAnywhere Anywhere { get; set; } = new BriefingAnywhere();
}

public sealed class BriefingTrader
{
    public string Name { get; set; }
}

// 맵 탭 하나. Count 는 퀘스트 수가 아니라 이 맵 전용 미완료 목표 수. 지도가 없으면 MapFolder 는 null.
public sealed class BriefingMap
{
    public string Key { get; set; }
    public List<string> LocationIds { get; set; } = new List<string>();
    public string Name { get; set; }
    public int Count { get; set; }
    public bool HasMap { get; set; }
    public string MapFolder { get; set; }
    public List<BriefingQuest> Quests { get; set; } = new List<BriefingQuest>();
    public List<BriefingExit> Exits { get; set; } = new List<BriefingExit>();
    public List<BriefingDoor> LockedDoors { get; set; } = new List<BriefingDoor>();
    // 오른쪽 열. 이 맵 전용 목표의 것만 온다. 수집은 place 마다 같은 줄이 반복될 수 있다.
    public List<BriefingItemRow> Bring { get; set; } = new List<BriefingItemRow>();
    public List<BriefingItemRow> Find { get; set; } = new List<BriefingItemRow>();
    public List<BriefingGear> Gear { get; set; } = new List<BriefingGear>();
    public List<BriefingSpecial> Special { get; set; } = new List<BriefingSpecial>();
}

// 순서·Marker·Summary·Current·Target 은 서버가 준 그대로 쓴다. Marker 는 지도 좌표가 있는 퀘스트만 1부터 붙는다.
// Current 와 Target 은 둘 다 null 이거나 둘 다 값이 있다.
public sealed class BriefingQuest
{
    public string QuestId { get; set; }
    public string TraderId { get; set; }
    public string Name { get; set; }
    public int? Marker { get; set; }
    public string Summary { get; set; }
    public double? Current { get; set; }
    public double? Target { get; set; }
    public List<BriefingObjective> Objectives { get; set; } = new List<BriefingObjective>();
}

// 펼친 퀘스트의 목표 한 줄. 서버는 끝나지 않은 레이드 목표만 보낸다(제출·무기 조립은 없고, 찾기는 정해진 자리에 생기는 퀘스트 아이템만).
// Current 는 Target 을 넘을 수 있다. InferredMap(목표 문장에서 맵을 추정했는지)은 읽기만 하고 화면에는 쓰지 않는다.
public sealed class BriefingObjective
{
    public string ConditionId { get; set; }
    public string Text { get; set; }
    public double Current { get; set; }
    public double? Target { get; set; }
    public bool InferredMap { get; set; }
    public List<BriefingLocation> Locations { get; set; } = new List<BriefingLocation>();
    // 클라이언트 전용. 레이드 중 LiveProgress 가 끝났다고 표시한 목표. 서버 응답에서는 늘 false.
    public bool Done { get; set; }
}

// 목표 위치(월드 좌표). 맵 탭 안에서는 그 탭 것만 하나로 합쳐 오고 Map 은 탭 키다.
public sealed class BriefingLocation
{
    public string Map { get; set; }
    public List<BriefingPoint> Points { get; set; } = new List<BriefingPoint>();
    public List<BriefingArea> Areas { get; set; } = new List<BriefingArea>();
}

// 퀘스트 구역(QuestCodex 웹의 MapArea). Yaw 는 Unity 규약의 도 단위. MinY·MaxY 가 없으면 높이는 Center.Y 한 점.
// Center 가 없으면 지도가 그 구역을 건너뛴다.
public sealed class BriefingArea
{
    public BriefingPoint Center { get; set; }
    public double SizeX { get; set; }
    public double SizeZ { get; set; }
    public double Yaw { get; set; }
    public double? MinY { get; set; }
    public double? MaxY { get; set; }
}

// 탈출구·환승. 서버가 탭 단위로 합치고 중복을 뺐다.
// Kind: pmc|shared|scav|transit. RequirementKind: coop|train|secret|alpinist|switch 또는 null.
// Requirement 는 서버가 lang 으로 쓴 문장이고, 있으면 RequirementKind 보다 우선한다.
// Chance 는 등장 확률(%), Target 은 환승 목적지 맵 키. Name 은 tarkov.dev 영문 이름이라 언어와 상관없다.
// Position 이 없으면 지도가 건너뛴다.
public sealed class BriefingExit
{
    public string Key { get; set; }
    public string Name { get; set; }
    public string Kind { get; set; }
    public BriefingPoint Position { get; set; }
    public string Requirement { get; set; }
    public string RequirementKind { get; set; }
    public double? Chance { get; set; }
    public string Target { get; set; }
}

// 잠긴 문. Kind: door|keycard|keypad|explosive|hatch. KeyName 은 열쇠나 폭약·도구 이름, Code 는 키패드 코드.
// Position 이 없으면 지도가 건너뛴다.
public sealed class BriefingDoor
{
    public string KeyTpl { get; set; }
    public string KeyName { get; set; }
    public string Kind { get; set; }
    public BriefingPoint Position { get; set; }
    public string Code { get; set; }
}

public sealed class BriefingPoint
{
    public double X { get; set; }
    public double Y { get; set; }
    public double Z { get; set; }
}

// 아무 맵이나 되거나 맵을 모르는 것. 맵마다 반복하지 않고 한 번만 온다. 수집은 맵마다 place 로 나뉘므로 여기 없다.
public sealed class BriefingAnywhere
{
    public List<BriefingQuest> Quests { get; set; } = new List<BriefingQuest>();
    public List<BriefingItemRow> Bring { get; set; } = new List<BriefingItemRow>();
    public List<BriefingGear> Gear { get; set; } = new List<BriefingGear>();
    public List<BriefingSpecial> Special { get; set; } = new List<BriefingSpecial>();
}

// 준비물·수집 한 줄. Items 는 대안 아이템이고 첫 것이 대표다. Fir 이면 Have 는 FIR 보유 수.
// Missing 은 웹의 missing() 값이고, 수집은 서버가 Missing > 0 인 것만 보낸다. 수는 double 로 온다.
// Place 는 수집에만 있다: here(이 맵에서 나옴), unknown(모든 맵), elsewhere(다른 맵에서만, 그리지 않음).
public sealed class BriefingItemRow
{
    public List<BriefingItemRef> Items { get; set; } = new List<BriefingItemRef>();
    public double Need { get; set; }
    public double Have { get; set; }
    public bool Fir { get; set; }
    public double Missing { get; set; }
    public List<BriefingSource> Sources { get; set; } = new List<BriefingSource>();
    public string Place { get; set; }
}

public sealed class BriefingItemRef
{
    public string Tpl { get; set; }
    public string Name { get; set; }
}

// 이 줄을 요구하는 퀘스트와 그 수. 수집을 요구하는 제출 퀘스트는 퀘스트 목록에 없을 수 있어 이름은 받지 않는다.
public sealed class BriefingSource
{
    public string QuestId { get; set; }
    public double Count { get; set; }
}

// 퀘스트 하나의 무기·장비 조건. 맵 것과 anywhere 것은 클라이언트가 questId 로 합친다.
public sealed class BriefingGear
{
    public string QuestId { get; set; }
    public List<BriefingGearRow> Rows { get; set; } = new List<BriefingGearRow>();
}

// Label: weapon|caliber|weaponMod|equipment|forbidden. 번역은 클라이언트가 한다.
// Names[i] 는 대안 i 의 이름, Tpls[i] 는 그 대안에 다 있어야 하는 tpl 들. 화면은 "외 N종"으로 줄여도 장착 판정에는 전부 쓴다.
// caliber 는 Names 가 게임 구경 코드("all" 가능)이고 Tpls[i] 는 비어 있다.
// build 는 한 총에 같이 있어야 하는 무기 조건과 부착물이다. Names·Tpls 는 부착물 대안, Weapons·Calibers 는 그 총의 조건이고
// 둘 다 비면 아무 무기. ModParents 는 부착물 대안 tpl 들의 부모 id 로, 인정 안 되는 같은 종류 부품을 찾을 때 쓴다.
public sealed class BriefingGearRow
{
    public string Label { get; set; }
    public List<string> Names { get; set; } = new List<string>();
    public List<List<string>> Tpls { get; set; } = new List<List<string>>();
    public List<BriefingItemRef> Weapons { get; set; } = new List<BriefingItemRef>();
    public List<string> Calibers { get; set; } = new List<string>();
    public List<string> ModParents { get; set; } = new List<string>();
}

public sealed class BriefingSpecial
{
    public string QuestId { get; set; }
    public List<BriefingSpecialRow> Rows { get; set; } = new List<BriefingSpecialRow>();
}

// Kind: oneRaid(한 레이드 안에 끝내기) | exit(탈출 상태·탈출구 조건).
// ExitStatuses: Survived|Runner|Transit|Killed|Left|MissingInAction, 모르는 값은 그대로 온다.
public sealed class BriefingSpecialRow
{
    public string Kind { get; set; }
    public List<string> ExitStatuses { get; set; } = new List<string>();
    public string ExitName { get; set; }
}
