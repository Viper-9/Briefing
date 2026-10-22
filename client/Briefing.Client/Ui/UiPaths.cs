namespace Briefing.Ui;

// BriefingScreen 프리팹 안의 경로(Transform.Find 형식). unity/Assets/Editor/CreateBriefingPrefab.cs 가 이 이름으로 만들므로
// 이름을 바꾸면 두 파일을 같이 바꾼다. 경로가 없으면 탭을 만들지 않는다. 에디터에서 크기와 색은 바꿔도 되지만 이름은 그대로 둔다.
internal static class UiPaths
{
    // 이 이름으로 시작하는 자식은 에디터 미리보기용이라 불러올 때 지운다.
    internal const string SamplePrefix = "Sample";

    internal const string MapBar = "MapBar";
    internal const string ChipTemplate = "MapBar/ChipTemplate";
    // 칩 안(칩 기준)
    internal const string ChipSelected = "Selected";
    internal const string ChipName = "Name";
    internal const string ChipCount = "Count";
    internal const string ChipRaid = "Raid"; // 레이드 중인 맵 표시, 처음엔 꺼져 있다

    // 열 폭 조절 손잡이는 런타임에 만든다.
    internal const string Columns = "Columns";
    internal const string ColLeft = "Columns/ColLeft";
    internal const string ColRight = "Columns/ColRight";

    internal const string HeaderTrader = "Columns/ColLeft/Header/Trader";
    internal const string HeaderQuest = "Columns/ColLeft/Header/Quest";
    internal const string HeaderProgress = "Columns/ColLeft/Header/Progress";

    internal const string Status = "Columns/ColLeft/Status";
    // 상태 줄 안(Status 기준)
    internal const string StatusText = "Text";
    internal const string StatusRetry = "Retry";
    internal const string StatusRetryLabel = "Retry/Label";

    internal const string List = "Columns/ColLeft/List";
    internal const string ListContent = "Columns/ColLeft/List/Viewport/Content";
    internal const string SectionTemplate = ListContent + "/SectionTemplate";
    internal const string QuestTemplate = ListContent + "/QuestTemplate";
    internal const string EmptyTemplate = ListContent + "/EmptyTemplate";
    internal const string DetailTemplate = ListContent + "/DetailTemplate"; // 펼침 블록
    // 묶음 머리줄 안(머리줄 기준)
    internal const string SectionCaret = "Caret";
    internal const string SectionTitle = "Title";
    internal const string SectionCount = "Count";
    // 퀘스트 줄 안(줄 기준)
    internal const string QuestInitial = "Trader/Initial";
    internal const string QuestFace = "Trader/Face";
    internal const string QuestBadges = "Trader/Badges"; // 카파·등대지기 해금 선행 배지, 처음엔 꺼져 있다
    internal const string QuestBadgeKappa = "Trader/Badges/Kappa";
    internal const string QuestBadgeLightkeeper = "Trader/Badges/Lightkeeper";
    internal const string QuestMarker = "Body/Title/Marker";
    internal const string QuestMarkerNumber = "Body/Title/Marker/Number";
    internal const string QuestName = "Body/Title/Name";
    internal const string QuestHandIcon = "Body/Title/HandIcon"; // 처음엔 꺼져 있다
    internal const string QuestSummary = "Body/Summary";
    internal const string QuestValue = "Progress/Value";
    internal const string QuestBar = "Progress/Bar";
    internal const string QuestFill = "Progress/Bar/Fill";
    // 펼침 블록 안(블록 기준). 줄 템플릿은 블록을 복제한 뒤 그 안에서 다시 복제하고, 모든 줄이 Objectives 에 게임 순서대로 쌓인다.
    internal const string DetailGoals = "Goals"; // 배지의 뜻, 처음엔 꺼져 있다
    internal const string GoalKappa = "Goals/Kappa";
    internal const string GoalKappaLabel = "Goals/Kappa/Label";
    internal const string GoalLightkeeper = "Goals/Lightkeeper";
    internal const string GoalLightkeeperLabel = "Goals/Lightkeeper/Label";
    internal const string DetailObjectives = "Objectives";
    internal const string ObjectiveTemplate = "Objectives/ObjectiveTemplate";
    internal const string HandoverTemplate = "Objectives/HandoverTemplate";
    internal const string TurnIn = "Actions/TurnIn";            // "완료하러 가기", 처음엔 꺼져 있다
    internal const string TurnInLabel = "Actions/TurnIn/Label";
    internal const string OpenTasks = "Actions/OpenTasks";
    internal const string OpenTasksLabel = "Actions/OpenTasks/Label";
    // 목표 줄 안(줄 기준). 건네주기 버튼이 없는 줄은 모두 이것을 쓴다.
    internal const string ObjectiveMarker = "Marker";
    internal const string ObjectiveMarkerNumber = "Marker/Number";
    internal const string ObjectiveCheck = "Marker/Check";
    internal const string ObjectiveText = "Main/Text";
    internal const string ObjectiveItems = "Main/Items";
    internal const string ObjectiveValue = "Value";
    // 건네주기 줄 안(줄 기준). Marker 는 글 시작을 목표 줄과 맞추려고 비워 둔 자리다.
    internal const string HandoverCheck = "Marker/Check";
    internal const string HandoverText = "Main/Text";
    internal const string HandoverItems = "Main/Items";
    internal const string HandoverValue = "Value";
    internal const string HandoverGive = "Give";
    internal const string HandoverGiveLabel = "Give/Label";

    // 지도 칸. Content 를 확대·이동하고, 그 아래는 지도, 구역, 잠긴 문, 탈출구, 마커 순으로 쌓인다.
    internal const string MapViewport = "Columns/ColMap/Viewport";
    internal const string MapContent = MapViewport + "/Content";
    internal const string MapLayers = MapContent + "/Layers";
    internal const string MapAreas = MapContent + "/Areas";
    internal const string MapDoors = MapContent + "/Doors";
    internal const string DoorTemplate = MapDoors + "/DoorTemplate";
    internal const string MapExits = MapContent + "/Exits";
    internal const string ExitTemplate = MapExits + "/ExitTemplate";
    internal const string MapMarkers = MapContent + "/Markers";
    internal const string MarkerTemplate = MapMarkers + "/MarkerTemplate";
    // 마커 안(마커 기준). 마커 자체 Image 는 테두리 고리, Fill 은 퀘스트 색 원.
    internal const string MarkerFill = "Fill";
    internal const string MarkerNumber = "Number";
    internal const string MapFloors = "Columns/ColMap/Floors";
    internal const string FloorTemplate = MapFloors + "/FloorTemplate";
    // 층 버튼 안(버튼 기준)
    internal const string FloorSelected = "Selected";
    internal const string FloorLabel = "Label";
    internal const string FloorDot = "Dot";
    internal const string MapToggles = "Columns/ColMap/LayerToggles";
    internal const string ToggleExits = MapToggles + "/Exits";
    // 탈출구 드롭다운. Exits 버튼 아래 ExitsMenu 에 종류별 체크박스가 있고, ExitsBlocker 는 목록 밖 클릭을 받는 투명한 막이다.
    internal const string ExitsMenu = ToggleExits + "/ExitsMenu";
    internal const string ExitsPmc = ExitsMenu + "/Pmc";
    internal const string ExitsScav = ExitsMenu + "/Scav";
    internal const string ExitsTransit = ExitsMenu + "/Transit";
    internal const string ExitsBlocker = "Columns/ColMap/ExitsBlocker";
    internal const string ExitsCheck = "Box/Check"; // 버튼 안(버튼 기준)
    internal const string ToggleDoors = MapToggles + "/Doors";
    internal const string ToggleAreas = MapToggles + "/Areas";
    internal const string ToggleLabel = "Label"; // 체크박스 안(체크박스 기준)
    internal const string MapZoom = "Columns/ColMap/Zoom";
    internal const string ZoomIn = MapZoom + "/In";
    internal const string ZoomOut = MapZoom + "/Out";
    internal const string MapMessage = "Columns/ColMap/Message";
    internal const string MapCredit = "Columns/ColMap/Credit";
    internal const string MapTooltip = "Columns/ColMap/Tooltip";
    internal const string TooltipTitle = "Title";   // 말풍선 안(말풍선 기준)
    internal const string TooltipDetail = "Detail";
    // 꺼 둔 아이콘 견본. 코드는 sprite 만 가져다 쓴다.
    internal const string IconPmc = "Columns/ColMap/Icons/Pmc";
    internal const string IconShared = "Columns/ColMap/Icons/Shared";
    internal const string IconScav = "Columns/ColMap/Icons/Scav";
    internal const string IconTransit = "Columns/ColMap/Icons/Transit";
    internal const string IconDoor = "Columns/ColMap/Icons/Door";

    // 오른쪽 열. 탭 셋, 필터 띠, 스크롤 본문 아래의 꺼 둔 템플릿.
    internal const string RightTabs = "Columns/ColRight/Tabs";
    internal const string TabBring = RightTabs + "/Bring";
    internal const string TabFind = RightTabs + "/Find";
    internal const string TabGear = RightTabs + "/Gear";
    internal const string TabSelected = "Selected";
    internal const string TabLabel = "Label";
    internal const string TabCount = "Count";
    internal const string RightFilter = "Columns/ColRight/Filter";
    internal const string FilterText = "Text";        // 띠 기준
    internal const string FilterClear = "Clear";
    internal const string FilterClearLabel = "Clear/Label";
    internal const string RightScroll = "Columns/ColRight/Scroll";
    internal const string RightContent = RightScroll + "/Viewport/Content";
    internal const string RightSectionTemplate = RightContent + "/SectionHeader";
    internal const string RightSectionTitle = "Title";  // 머리줄 기준
    internal const string RightSectionNote = "Note";
    internal const string RightSectionLegend = "Legend"; // 머리줄 기준, 처음엔 꺼져 있다
    internal const string LegendOk = "Legend/Ok";
    internal const string LegendStash = "Legend/Stash";
    internal const string LegendWarn = "Legend/Warn";
    internal const string LegendMark = "Mark";           // 범례 항목 기준
    internal const string LegendText = "Text";
    internal const string RightEmptyTemplate = RightContent + "/Empty";
    internal const string RightGridTemplate = RightContent + "/Grid";
    internal const string GridBoard = "Board";          // 격자 기준
    internal const string GridCellTemplate = "Board/ItemCell";
    internal const string GridBlankTemplate = "Board/Blank";
    internal const string CellIcon = "Icon";            // 칸 기준
    internal const string CellShort = "Short";
    internal const string CellCount = "Count";
    internal const string CellFir = "Fir";
    internal const string CellAlt = "Alt";
    internal const string RightLoadoutTemplate = RightContent + "/Loadout";
    internal const string LoadoutSlotTemplate = "Slot"; // 현재 장비 줄 기준
    internal const string SlotIcon = "Icon";            // 슬롯 기준
    internal const string SlotLabel = "Label";
    internal const string RightGearTemplate = RightContent + "/GearBlock";
    internal const string GearTitle = "Head/Title";     // 덩어리 기준
    internal const string GearLine = "Line";             // 덩어리 아래 구분선
    internal const string RuleRowTemplate = "RuleRow";
    internal const string SpecialRowTemplate = "SpecialRow";
    internal const string RuleLabel = "Label";          // 조건 줄 기준
    internal const string RuleMark = "Value/Line/Mark";
    internal const string RuleLead = "Value/Line/Lead";
    internal const string RuleTag = "Value/Line/Tag";
    internal const string RuleMore = "Value/Line/More";
    internal const string RuleMoreLabel = "Value/Line/More/Label";
    internal const string RuleAlts = "Value/Alts";
    internal const string RuleAltsContent = "Value/Alts/Viewport/Content";
    internal const string AltTemplate = "Value/Alts/Viewport/Content/AltTemplate";
    internal const string AltMark = "Mark";             // 대안 줄 기준
    internal const string AltText = "Text";
    internal const string SpecialLabel = "Label";       // 특수 조건 줄 기준
    internal const string SpecialText = "Text";
    internal const string RightTooltip = "RightTooltip"; // 스크롤 마스크에 잘리지 않게 화면 루트의 마지막 자식
    internal const string RightTipTitle = "Title";       // 말풍선 기준
    internal const string RightTipKind = "Kind";
    internal const string RightTipAlt = "Alt";
    internal const string RightTipCount = "Count";
    internal const string RightTipQuests = "Quests";
    // 꺼 둔 판정 표시와 FIR 고리 견본.
    internal const string IconMarkOk = "Columns/ColRight/Icons/MarkOk";
    internal const string IconMarkStash = "Columns/ColRight/Icons/MarkStash";
    internal const string IconMarkWarn = "Columns/ColRight/Icons/MarkWarn";
    internal const string IconRing = "Columns/ColRight/Icons/Ring";
}
