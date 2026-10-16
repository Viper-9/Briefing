using System.Globalization;

namespace Briefing.Core.Screen;

// 화면 문구. 문구마다 L(kr, en, ru) 한 줄로 세 언어를 같이 적어서 어느 언어도 빠뜨릴 수 없게 했다.
// kr 은 한국어, ru 는 러시아어, 그 밖의 게임 언어는 영어.
// 게임 글꼴(Bender + KS X 1001 한글 폴백)에 없는 기호(…, ✓, ⚠, ▾, ▴, ✕, —)는 쓰지 않는다. TextsTableTests 가 검사한다.
// 러시아어 낱말은 QuestCodex 웹의 ru.json 과 맞췄다.
public static class Texts
{
    private enum Lang { Kr, En, Ru }

    private static Lang _lang = Lang.Kr; // 언어를 정하지 않은 테스트는 한국어 문구를 본다

    // BundleBriefingView 가 Build·Render 때 메인 스레드에서 UseLanguage 로 맞춘다.
    public static string Language => _lang == Lang.Kr ? "kr" : _lang == Lang.Ru ? "ru" : "en";

    public static void UseLanguage(string lang) => _lang = lang == "kr" ? Lang.Kr : lang == "ru" ? Lang.Ru : Lang.En;

    private static string L(string kr, string en, string ru) => _lang == Lang.Kr ? kr : _lang == Lang.Ru ? ru : en;

    private static string N(double value) => value.ToString("0.##", CultureInfo.InvariantCulture);

    // 브리핑 라우트가 처음 들어간 QuestCodex 버전. 1.7.0 에는 라우트가 없다.
    public const string RequiredQuestCodex = "1.9.0";
    public const string UnknownTraderInitial = "?";

    public static string TabTitle => L("브리핑", "Briefing", "Брифинг");
    public static string Loading => L("브리핑을 불러오는 중...", "Loading briefing...", "Загрузка брифинга...");
    public static string LoadFailed => L("브리핑을 불러오지 못했습니다", "Couldn't load the briefing", "Не удалось загрузить брифинг");
    public static string QuestCodexMissing => L(
        "QuestCodex가 필요합니다. SPT 서버에 QuestCodex를 설치하세요 (SPT_Runtime/user/mods/QuestCodex)",
        "QuestCodex is required. Install QuestCodex on the SPT server (SPT_Runtime/user/mods/QuestCodex)",
        "Нужен QuestCodex. Установите QuestCodex на сервер SPT (SPT_Runtime/user/mods/QuestCodex)");
    public static string QuestCodexOutdated => L(
        $"QuestCodex {RequiredQuestCodex} 이상으로 업데이트하세요",
        $"Update QuestCodex to {RequiredQuestCodex} or later",
        $"Обновите QuestCodex до версии {RequiredQuestCodex} или новее");
    public static string BriefingOutdated => L(
        "Briefing을 업데이트하세요. QuestCodex가 더 새로운 응답 형식을 보냅니다",
        "Update Briefing. QuestCodex sends a newer response format",
        "Обновите Briefing. QuestCodex отправляет более новый формат ответа");
    public static string Retry => L("다시 시도", "Retry", "Повторить");
    public static string JustNow => L("1분 이내에 받은 데이터", "Data from less than a minute ago", "Данные менее минуты назад");
    public static string MinutesAgo(int minutes) => L($"{minutes}분 전 데이터", $"Data from {minutes} min ago", $"Данные {minutes} мин назад");

    // 레이드 중 상태 줄과 맵 칩 표시
    public static string RaidData => L("레이드 전 데이터 · 진행은 실시간", "Pre-raid data · progress is live", "Данные до рейда · прогресс в реальном времени");
    public static string RaidNoData => L("메뉴에서 브리핑을 받지 못했습니다", "No briefing was received in the menu", "Брифинг не был получен в меню");
    public static string RaidTag => L("레이드 중", "In raid", "В рейде");

    public static string HereTitle(string mapName) => L($"{mapName}에서 진행", $"On {mapName}", $"Задания: {mapName}");
    public static string AnywhereTitle => L("아무 맵에서나", "Any map", "Любая карта");
    public static string NoQuestsHere => L("이 맵에서 진행할 퀘스트가 없습니다", "No quests to do on this map", "На этой карте нет заданий");

    // 퀘스트 펼침
    public static string ReadyTitle => L("제출", "Hand in", "Передать");
    public static string HandoverCount(int count) => L($"건네줄 목표 {count}개",
        count == 1 ? "1 objective to hand over" : $"{count} objectives to hand over",
        $"Целей к передаче: {count}");
    public static string Handover => L("건네주기", "Hand over", "Передать");
    public static string OpenInTasks => L("임무 탭에서 보기", "Show in Tasks", "Показать в заданиях");
    // "완료 가능" 묶음과 상인 임무 화면으로 가는 버튼
    public static string TurnInTitle => L("완료 가능", "Ready to turn in", "Можно сдать");
    public static string TurnInSummary => L("상인에게 보고하면 완료", "Report to the trader to complete", "Доложите торговцу, чтобы завершить");
    public static string TurnIn => L("완료하러 가기", "Turn in", "Сдать");
    // 아이템 이름 3개 뒤에 붙는 나머지 개수. 앞 공백까지 포함한다.
    public static string MoreItems(int count) => L($" 외 {count}개", $" +{count} more", $" +{count} ещё");

    public static string HeaderTrader => L("상인", "Trader", "Торговец");
    public static string HeaderQuest => L("임무", "Quest", "Задание");
    public static string HeaderProgress => L("진행률", "Progress", "Прогресс");

    // 지도 칸. 층 이름은 QuestCodex 웹 floorName 과 같다.
    public static string MapLoading => L("지도를 불러오는 중...", "Loading map...", "Загрузка карты...");
    public static string MapNone => L("이 맵은 지도가 없습니다", "No map for this location", "Для этой локации нет карты");
    public static string MapLoadFailed => L("지도를 불러오지 못했습니다", "Couldn't load the map", "Не удалось загрузить карту");
    public static string LayerExits => L("탈출구", "Extracts", "Выходы");
    public static string LayerDoors => L("잠긴 문", "Locked doors", "Запертые двери");
    public static string LayerAreas => L("퀘스트 구역", "Quest zones", "Зоны заданий");

    // "deck"(아이스브레이커)은 층 번호가 곧 데크 번호. 그 밖에는 0 이 지상, 음수가 지하, n 이 n+1층.
    public static string FloorName(int level, string floorNames)
    {
        var n = level.ToString(CultureInfo.InvariantCulture);
        if (floorNames == "deck") return L($"데크 {n}", $"Deck {n}", $"Палуба {n}");
        if (level < 0) return L("지하", "Underground", "Подземный");
        if (level == 0) return L("지상", "Ground", "Наземный");
        var upper = (level + 1).ToString(CultureInfo.InvariantCulture);
        return L($"{upper}층", $"Floor {upper}", $"Этаж {upper}");
    }

    // 탈출구·잠긴 문 말풍선. QuestCodex 웹 문구와 같다.
    public static string ExitKind(string kind)
    {
        switch (kind)
        {
            case "pmc": return L("PMC", "PMC", "ЧВК");
            case "shared": return L("PMC + 스캐브", "PMC + Scav", "ЧВК + Дикий");
            case "scav": return L("스캐브", "Scav", "Дикий");
            case "transit": return L("환승", "Transit", "Переход");
            default: return kind ?? "";
        }
    }

    public static string TransitTo(string mapName) => L($"환승 → {mapName}", $"Transit → {mapName}", $"Переход → {mapName}");

    // 모르는 종류면 null 이라 말풍선에서 빠진다.
    public static string ExitRequirement(string requirementKind)
    {
        switch (requirementKind)
        {
            case "coop": return L("스캐브와 협동 필요", "Co-op with a Scav", "Нужен кооператив с Диким");
            case "train": return L("열차 도착 대기", "Wait for the train", "Дождитесь поезда");
            case "secret": return L("비밀 탈출구 (먼저 발견해야 사용 가능)", "Secret extract (must be discovered first)", "Секретный выход (сначала нужно обнаружить)");
            case "alpinist": return L("등반 장비 필요(파라코드 + 얼음 도끼)", "Needs climbing gear (paracord + ice pick)", "Нужно альпинистское снаряжение (паракорд + ледоруб)");
            case "switch": return L("스위치·전원 작동 필요", "Needs a switch or power to open", "Нужно включить рубильник или питание");
            default: return null;
        }
    }

    public static string ExitChance(double percent)
    {
        var n = N(percent);
        return L($"등장 확률 {n}%", $"{n}% chance", $"Шанс {n}%");
    }

    public static string Keycard => L("키카드", "Keycard", "Ключ-карта");
    public static string Keypad => L("키패드 잠금", "Keypad lock", "Кодовый замок");
    public static string KeypadCode(string code) => L($"코드 {code}", $"code {code}", $"код {code}");
    public static string ExplosiveDoor(string item) => L($"폭파 필요: {item}", $"Blow open with: {item}", $"Подорвать: {item}");
    public static string FrozenHatch(string item) => L($"얼음을 녹여야 함: {item}", $"Melt the ice with: {item}", $"Растопить лёд: {item}");

    // 지도 칸 구석의 출처 표기. 언어와 상관없이 같다.
    public static string MapCredit(string author, string license) =>
        string.IsNullOrEmpty(license) ? $"Map: {author}" : $"Map: {author} ({license})";

    // 오른쪽 열. 뜻은 QuestCodex 웹과 같다.
    public static string TabBring => L("준비물", "Supplies", "Припасы");
    public static string TabFind => L("수집", "Collect", "Сбор");
    public static string TabGear => L("장비", "Gear", "Снаряжение");
    public static string BringHere => L("레이드에 가져갈 퀘스트 아이템", "Quest items to bring", "Взять с собой в рейд");
    public static string BringNone => L("준비물 없음", "No supplies", "Нет припасов");
    public static string ShortRows(int count) => L($"부족 {count}", $"{count} short", $"Не хватает: {count}");
    public static string FindHere(string mapName) => L($"{mapName} 획득처", $"{mapName} sources", $"Где найти: {mapName}");
    public static string FindAnyMap => L("모든 맵", "Any map", "Любая карта");
    public static string FindNone => L("없음", "None", "Нет");
    public static string FilterEmptyBring => L("이 퀘스트는 챙길 것이 없습니다", "Nothing to bring for this quest", "Для этого задания ничего не нужно брать");
    public static string FilterEmptyFind => L("이 퀘스트는 찾을 것이 없습니다", "Nothing to find for this quest", "Для этого задания ничего не нужно искать");
    public static string FilterOnly(string questName) => L($"선택한 퀘스트만: {questName}", $"Selected quest only: {questName}", $"Только выбранное задание: {questName}");
    // ✕(U+2715)는 한글 폴백에 없어서 ×(U+00D7)를 쓴다.
    public static string ShowAll => L("전체 보기 ×", "Show all ×", "Показать все ×");
    // 격자 칸 말풍선
    public static string OrItems(string names) => L($"또는: {names}", $"Or: {names}", $"Или: {names}");
    public static string HaveNeed(string have, string need) => L($"보유 {have} / 필요 {need}", $"Have {have} / Need {need}", $"Есть {have} / Нужно {need}");
    public static string QuestItemKind => L(
        "퀘스트 전용 아이템 · 퀘스트를 받으면 레이드에 생김",
        "Quest item · spawns only while the quest is active",
        "Квестовый предмет · появляется, пока задание активно");
    public static string LootItemKind => L("일반 아이템 · 레이드에서 언제든 파밍", "Regular item · lootable in any raid", "Обычный предмет · можно найти в любом рейде");
    public static string ShortBy(string count) => L($"부족 {count}", $"Short {count}", $"Не хватает {count}");

    // 장비 탭. 조건 라벨은 웹과 같다.
    public static string GearLoadoutTitle => L("현재 장비", "Current gear", "Текущее снаряжение");
    public static string GearRulesTitle => L("무기·장비 조건", "Weapons and gear", "Оружие и снаряжение");
    public static string GearSpecialTitle => L("특수 조건", "Special conditions", "Особые условия");
    public static string GearNone => L("장비 조건 없음", "No gear conditions", "Нет условий по снаряжению");
    public static string FilterEmptyGear => L("이 퀘스트는 장비 조건이 없습니다", "No gear conditions for this quest", "У этого задания нет условий по снаряжению");
    public static string LoadoutUnreadable => L("장착 정보를 읽지 못했습니다", "Couldn't read your equipment", "Не удалось прочитать снаряжение");
    public static string SlotPrimary1 => L("주무기 1", "Primary 1", "Основное 1");
    public static string SlotPrimary2 => L("주무기 2", "Primary 2", "Основное 2");
    public static string SlotHolster => L("권총", "Holster", "Пистолет");
    public static string SlotHelmet => L("헬멧", "Helmet", "Шлем");
    public static string SlotArmor => L("방탄복", "Body armor", "Бронежилет");
    public static string SlotRig => L("조끼", "Rig", "Разгрузка");

    public static string GearLabel(string label)
    {
        switch (label)
        {
            case "weapon": return L("무기", "Weapon", "Оружие");
            case "caliber": return L("구경", "Caliber", "Калибр");
            case "weaponMod": return L("필수 부착물", "Weapon mods", "Моды оружия");
            case "equipment": return L("착용", "Wear", "Надеть");
            case "forbidden": return L("착용 금지", "Don't wear", "Не надевать");
            default: return label ?? "";
        }
    }

    public static string AnyCaliber => L("모든 구경", "Any caliber", "Любой калибр");
    public static string TagEquipped => L("장착 중", "equipped", "надето");
    public static string TagStash => L("보관함", "in stash", "в схроне");
    public static string ForbiddenClear => L("해당 장비 착용 안 함", "Not wearing any of these", "Ничего из этого не надето");
    public static string WearingNow(string name) => L($"착용 중: {name}", $"Wearing: {name}", $"Надето: {name}");
    // build 줄의 ⚠ 이유
    public static string BuildReasonLabel => L("확인", "Check", "Проверка");
    public static string BuildOnOtherWeapon => L("인정 부착물이 다른 총에 달려 있음", "Accepted mod is on another weapon", "Нужный мод стоит на другом оружии");
    public static string BuildNotAccepted(string names) => L($"인정 안 되는 부착물: {names}", $"Not accepted: {names}", $"Не засчитывается: {names}");
    public static string BuildNoMods => L("조건 무기에 인정 부착물 없음", "No accepted mod on the required weapon", "На нужном оружии нет подходящего мода");
    // 화살표는 화면이 붙인다. ▼▲ 는 한글 폴백에 있고 ▾▴ 는 없다.
    public static string MoreAlternatives(int count) => L($"외 {count}종", $"+{count} more", $"+{count} ещё");
    public static string ListCount(int count) => L($"목록 {count}종", $"List of {count}", $"Список: {count}");
    public static string ShowLess => L("접기", "Show less", "Свернуть");

    // "무기·장비 조건" 머리 오른쪽 범례
    public static string LegendMark(GearMark mark)
    {
        switch (mark)
        {
            case GearMark.Equipped: return L("장착", "Equipped", "Надето");
            case GearMark.Stash: return L("보관함", "Stash", "Схрон");
            case GearMark.Missing: return L("없음", "Missing", "Нет");
            default: return "";
        }
    }

    // 특수 조건. 모르는 탈출 상태는 그대로 보인다.
    public static string SpecialRaid => L("레이드", "Raid", "Рейд");
    public static string OneRaid => L("한 레이드 안에 완료", "Complete in a single raid", "Выполнить за один рейд");
    public static string SpecialExit => L("탈출", "Exit", "Выход");
    public static string ExitVia(string name) => L($"{name} 탈출구", $"via {name}", $"через {name}");

    public static string ExitStatus(string status)
    {
        switch (status)
        {
            case "Survived": return L("생존", "Survived", "Выжил");
            case "Runner": return L("런스루", "Run through", "Пробежал");
            case "Transit": return L("환승", "Transit", "Переход");
            case "Killed": return L("사망", "Killed", "Убит");
            case "Left": return L("이탈", "Left", "Вышел");
            case "MissingInAction": return L("실종(MIA)", "MIA", "Пропал без вести");
            default: return status ?? "";
        }
    }
}
