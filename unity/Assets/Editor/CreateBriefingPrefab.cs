using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

// 에디터 전용. BriefingScreen 프리팹(맵 칩 줄, 왼쪽 열과 펼침 블록, 지도 칸, 오른쪽 열)을 코드로 만든다.
// 이름 구조는 client/Briefing.Client/Ui/UiPaths.cs 와 짝이라 이름을 바꾸면 두 파일을 같이 바꾼다.
// 만든 뒤 크기, 간격, 색은 에디터에서 손봐도 되는데 그 뒤로는 다시 돌리지 않는다. batchmode 는 -forcePrefab 없이는 덮어쓰지 않는다.
// 메뉴 Briefing > Create Briefing Prefab, 또는 -executeMethod CreateBriefingPrefab.Create [-forcePrefab]
public static class CreateBriefingPrefab
{
    private const string PrefabPath = "Assets/Briefing/BriefingScreen.prefab";

    // 목업 색
    private static readonly Color PanelBg = Hex("#121414", 0.82f);
    private static readonly Color BarBg = Hex("#181d1e", 0.85f);
    private static readonly Color HeaderBg = Hex("#141616");
    private static readonly Color RowBg = Hex("#1d1f1f");
    private static readonly Color SectionBg = Hex("#111313");
    private static readonly Color StatusBg = Hex("#2a2117");
    private static readonly Color ButtonBg = Hex("#3a4243");
    private static readonly Color ChipOnBg = Hex("#d8dbd5");
    private static readonly Color ProgressBg = Hex("#0c0d0d");
    private static readonly Color ProgressFill = Hex("#8a8f8c");
    private static readonly Color TextMain = Hex("#c9cbc6");
    private static readonly Color TextBright = Hex("#dfe1dc");
    private static readonly Color TextDim = Hex("#8b8f8a");
    private static readonly Color TextFaint = Hex("#5d615d");
    private static readonly Color ChipText = Hex("#cdd0cb");
    private static readonly Color ChipTextOn = Hex("#141615");
    private static readonly Color ChipCountOn = Hex("#3c403d");
    private static readonly Color DetailBg = Hex("#161818");
    private static readonly Color MapBg = Hex("#060808", 0.55f);
    private static readonly Color OverlayBg = Hex("#141717", 0.9f);
    private static readonly Color FloorOnBg = Hex("#d8dbd5");
    private static readonly Color FloorText = Hex("#cfd2cd");
    private static readonly Color DoorLocked = Hex("#ff2a2a");
    // 퀘스트 마커는 한 가지 톤이다. 여기엔 평소 색만 넣고, 강조 색은 코드(MarkerTone)가 바꾼다.
    private static readonly Color MarkerInk = TextMain;
    private static readonly Color MarkerDark = Hex("#141616");
    // 오른쪽 열 색
    private static readonly Color FilterBg = Hex("#2b2614");
    private static readonly Color Gold = Hex("#c9a64a");
    private static readonly Color GridLine = Hex("#262a29");
    private static readonly Color CellBg = Hex("#151918");
    private static readonly Color CellShort = Hex("#4a2522", 0.9f);
    private static readonly Color CellOk = Hex("#2c3d2b", 0.9f);
    private static readonly Color CellText = Hex("#eef0eb");
    private static readonly Color ShortRed = Hex("#ff8a80");
    private static readonly Color HaveGreen = Hex("#9ff59a");
    private static readonly Color StashCyan = Hex("#7fd4f0");   // ◐ (코드의 StashCyan 과 같은 값)
    private static readonly Color WarnOrange = Hex("#f2b05e");  // ⚠ (코드의 WarnOrange 와 같은 값)
    private static readonly Color SlotFilled = Hex("#3a3422", 0.8f);
    private static readonly Color SoftLine = Hex("#1e2322");
    private static readonly Color LinkBlue = Hex("#7fb2e6");
    private static readonly Color AltsBg = Hex("#0e1110");
    private static readonly Color TooltipBg = Hex("#0a0c0c", 0.96f);

    private const string IconFolder = "Assets/Briefing/Icons/";
    // 카파·등대지기 해금 선행 아이콘(40px). dev-docs/07-kappa-lightkeeper/icons 의 272px 그림을 빈 여백 없이 잘라 줄인 것(카파는 작은 크기에서 잘 읽히는 정면)
    private const string KappaIconFile = "goal_kappa.png";
    private const string LighthouseIconFile = "goal_lighthouse.png";
    private static readonly Color BadgeBg = Hex("#000000", 0.55f);
    private static readonly Color GoalText = Hex("#c9cbc6", 0.7f); // 범례 글은 목표 글보다 한 단계 흐리게

    [MenuItem("Briefing/Create Briefing Prefab")]
    public static void Create()
    {
        if (!ConfirmOverwrite()) return;
        Directory.CreateDirectory("Assets/Briefing");
        _placeholderFont = null; // 실행마다 자리 표시 글꼴을 새로 만든다
        GenerateIcons(); // 판정 표시, FIR 고리, 마커 테두리, 핀 PNG. IconSprite 가 읽기 전에 만든다

        var root = new GameObject("BriefingScreen", typeof(RectTransform));
        Stretch(root);
        VLayout(root, 0, 0, 0, 0, 10, TextAnchor.UpperLeft, true, false);

        BuildMapBar(root.transform);

        var columns = Child(root.transform, "Columns");
        HLayout(columns, 0, 0, 0, 0, 14, TextAnchor.UpperLeft, false, true);
        Layout(columns, flexH: 1);
        BuildLeftColumn(columns.transform);
        BuildMapColumn(columns.transform);
        BuildRightColumn(columns.transform);
        BuildRightTooltip(root.transform); // 오른쪽 열 스크롤 마스크에 잘리지 않게 화면 루트의 마지막 자식으로

        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        var importer = AssetImporter.GetAtPath(PrefabPath);
        if (importer == null) throw new System.Exception("No AssetImporter for " + PrefabPath);
        importer.assetBundleName = "briefing.bundle";
        importer.SaveAndReimport();
        Debug.Log("Created: " + PrefabPath);
    }

    private static bool ConfirmOverwrite()
    {
        if (!File.Exists(PrefabPath)) return true;
        if (Application.isBatchMode)
        {
            if (System.Environment.GetCommandLineArgs().Contains("-forcePrefab")) return true;
            Debug.Log("CreateBriefingPrefab: " + PrefabPath + " already exists; skipped (pass -forcePrefab to overwrite)");
            return false;
        }
        return EditorUtility.DisplayDialog("Create Briefing Prefab",
            PrefabPath + " already exists. Overwrite it? (hand edits in the prefab are lost)", "Overwrite", "Cancel");
    }

    // ── 맵 칩 줄 ──

    private static void BuildMapBar(Transform root)
    {
        var bar = Child(root, "MapBar");
        Img(bar, BarBg);
        Layout(bar, prefH: 52, minH: 52);
        HLayout(bar, 16, 16, 8, 8, 6, TextAnchor.MiddleLeft, false, false);

        Chip(bar.transform, "ChipTemplate", "Customs", "10", false).SetActive(false);
        Chip(bar.transform, "SampleChip1", "Customs", "10", true);
        Chip(bar.transform, "SampleChip2", "The Labyrinth", "0", false).GetComponent<CanvasGroup>().alpha = 0.45f;
    }

    private static GameObject Chip(Transform parent, string name, string label, string count, bool selected)
    {
        var chip = Child(parent, name, typeof(CanvasGroup));
        var hit = Img(chip, new Color(0, 0, 0, 0)); // 투명하지만 클릭은 받는다
        chip.AddComponent<Button>().targetGraphic = hit;
        HLayout(chip, 14, 14, 6, 6, 8, TextAnchor.MiddleCenter, false, false);

        var on = Child(chip.transform, "Selected");
        Stretch(on);
        Img(on, ChipOnBg).raycastTarget = false;
        Layout(on).ignoreLayout = true;
        on.SetActive(selected);

        // 칩은 글자 크기에 맞춰 늘어나므로 말줄임이 필요 없다. 말줄임(Ellipsis)이면 레이아웃이 준 높이가 딱 맞을 때
        // 한글 대체 글꼴 줄이 조금만 높아도 첫 줄이 넘친 것으로 보고 글자를 전부 지운다.
        Label(chip.transform, "Name", label, 17, selected ? ChipTextOn : ChipText, TextAlignmentOptions.Left).overflowMode = TextOverflowModes.Overflow;
        Label(chip.transform, "Count", count, 17, selected ? ChipCountOn : TextDim, TextAlignmentOptions.Left).overflowMode = TextOverflowModes.Overflow;
        // 레이드 중인 맵 표시. 처음엔 꺼 두고 코드가 켜면서 글자를 채운다.
        var raid = Label(chip.transform, "Raid", "In raid", 13, Gold, TextAlignmentOptions.Left);
        raid.overflowMode = TextOverflowModes.Overflow;
        raid.gameObject.SetActive(false);
        return chip;
    }

    // ── 왼쪽 열 ──

    private static void BuildLeftColumn(Transform columns)
    {
        var col = Child(columns, "ColLeft");
        Img(col, PanelBg);
        Layout(col, prefW: 540);
        VLayout(col, 0, 0, 0, 0, 0, TextAnchor.UpperLeft, true, false);

        var header = Child(col.transform, "Header");
        Img(header, HeaderBg);
        // childForceExpandHeight 인 그룹은 flexibleHeight 가 1 이상으로 잡혀 List 와 남는 높이를 나눠 가지므로 0 으로 못박는다.
        Layout(header, prefH: 42, minH: 42, flexH: 0);
        HLayout(header, 0, 0, 0, 0, 0, TextAnchor.MiddleLeft, false, true);
        Layout(Label(header.transform, "Trader", "Trader", 15, TextMain, TextAlignmentOptions.Center).gameObject, prefW: 76, minW: 76);
        var quest = Label(header.transform, "Quest", "Quest", 15, TextMain, TextAlignmentOptions.Left);
        quest.margin = new Vector4(14, 0, 0, 0);
        Layout(quest.gameObject, flexW: 1, minW: 0);
        Layout(Label(header.transform, "Progress", "Progress", 15, TextMain, TextAlignmentOptions.Center).gameObject, prefW: 112, minW: 112);

        BuildStatus(col.transform);
        BuildList(col.transform);
    }

    private static void BuildStatus(Transform col)
    {
        var status = Child(col, "Status");
        Img(status, StatusBg);
        Layout(status, minH: 40);
        HLayout(status, 12, 12, 6, 6, 10, TextAnchor.MiddleLeft, false, false);

        var text = Label(status.transform, "Text", "Data from 5 minutes ago", 14, TextMain, TextAlignmentOptions.Left);
        text.enableWordWrapping = true;
        text.overflowMode = TextOverflowModes.Overflow;
        Layout(text.gameObject, flexW: 1);

        var retry = Child(status.transform, "Retry");
        var bg = Img(retry, ButtonBg);
        retry.AddComponent<Button>().targetGraphic = bg;
        Layout(retry, prefW: 110, prefH: 30, minH: 30);
        Stretch(Label(retry.transform, "Label", "Retry", 14, TextMain, TextAlignmentOptions.Center).gameObject);

        status.SetActive(false);
    }

    private static void BuildList(Transform col)
    {
        var list = Child(col, "List");
        Img(list, new Color(0, 0, 0, 0)); // 휠·끌기를 받는 영역
        Layout(list, flexH: 1);
        var scroll = list.AddComponent<ScrollRect>();

        var viewport = Child(list.transform, "Viewport", typeof(RectMask2D));
        Stretch(viewport);

        var content = Child(viewport.transform, "Content");
        var rt = (RectTransform)content.transform;
        rt.anchorMin = new Vector2(0, 1);
        rt.anchorMax = new Vector2(1, 1);
        rt.pivot = new Vector2(0.5f, 1);
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        VLayout(content, 0, 0, 0, 0, 2, TextAnchor.UpperLeft, true, false);
        content.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scroll.viewport = (RectTransform)viewport.transform;
        scroll.content = rt;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 30;
        BuildScrollbar(list.transform, scroll);

        var c = content.transform;
        Section(c, "SectionTemplate", "Customs", "7").SetActive(false);
        Quest(c, "QuestTemplate", "R", "1", "Quest name", "Objective summary", "0 / 1", 0f).SetActive(false);
        Empty(c, "EmptyTemplate").SetActive(false);
        Detail(c, "DetailTemplate", false).SetActive(false);

        Section(c, "SampleSection", "On Customs", "7");
        Quest(c, "SampleQuest1", "R", "1", "Provide Viewership",
            "Eliminate any target inside the industrial plant on Customs x15", "6 / 15", 0.4f);
        Detail(c, "SampleDetail", true);
        Quest(c, "SampleQuest2", "M", null, "A Shooter Born in Heaven",
            "Eliminate PMC operatives with a headshot while using a bolt-action rifle on Customs x5", "4 / 5", 0.8f);
    }

    // 목록 오른쪽 끝 세로 스크롤바(표준 uGUI Scrollbar). 트랙·핸들 Image 는 raycastTarget 을 켜 둬서 끌기·트랙 클릭을 받는다.
    private static void BuildScrollbar(Transform list, ScrollRect scroll)
    {
        var bar = Child(list, "Scrollbar");
        var track = Img(bar, ProgressBg);
        var brt = (RectTransform)bar.transform;
        brt.anchorMin = new Vector2(1, 0);
        brt.anchorMax = new Vector2(1, 1);
        brt.pivot = new Vector2(1, 0.5f);
        brt.sizeDelta = new Vector2(8, 0);
        brt.anchoredPosition = Vector2.zero;

        var area = Child(bar.transform, "Sliding Area");
        Stretch(area);
        var handle = Child(area.transform, "Handle");
        var handleImg = Img(handle, ProgressFill);
        Stretch(handle); // 핸들 크기는 Scrollbar 가 런타임에 앵커로 정한다

        var sb = bar.AddComponent<Scrollbar>();
        sb.handleRect = (RectTransform)handle.transform;
        sb.targetGraphic = handleImg;
        sb.direction = Scrollbar.Direction.BottomToTop;

        scroll.verticalScrollbar = sb;
        scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHideAndExpandViewport;
        scroll.verticalScrollbarSpacing = 2;
    }

    private static GameObject Section(Transform parent, string name, string title, string count)
    {
        var s = Child(parent, name);
        var bg = Img(s, SectionBg);
        s.AddComponent<Button>().targetGraphic = bg;
        Layout(s, prefH: 34, minH: 34);
        HLayout(s, 12, 12, 0, 0, 8, TextAnchor.MiddleLeft, false, false);
        Label(s.transform, "Caret", "v", 14, TextDim, TextAlignmentOptions.Center);
        Label(s.transform, "Title", title, 14, TextDim, TextAlignmentOptions.Left);
        Label(s.transform, "Count", count, 14, TextMain, TextAlignmentOptions.Left);
        return s;
    }

    private static GameObject Quest(Transform parent, string name, string initial, string marker,
        string title, string summary, string progress, float fraction)
    {
        var q = Child(parent, name);
        var rowBg = Img(q, RowBg);
        q.AddComponent<Button>().targetGraphic = rowBg; // 줄을 누르면 펼친다. 안쪽 글자와 이미지는 raycastTarget 을 끈다
        Layout(q, minH: 74);
        HLayout(q, 0, 0, 0, 0, 0, TextAnchor.MiddleLeft, false, true);

        var trader = Child(q.transform, "Trader");
        Layout(trader, prefW: 76, minW: 76); // HorizontalLayoutGroup 은 min~pref 사이를 보간하므로 둘을 같게 해 긴 글 줄에서도 줄지 않게 한다
        Stretch(Label(trader.transform, "Initial", initial, 22, TextMain, TextAlignmentOptions.Center).gameObject);
        var face = Child(trader.transform, "Face");
        var frt = (RectTransform)face.transform;
        frt.anchorMin = frt.anchorMax = new Vector2(0.5f, 0.5f);
        frt.sizeDelta = new Vector2(58, 58);
        var faceImg = Img(face, Color.white);
        faceImg.preserveAspect = true;
        faceImg.raycastTarget = false;
        face.SetActive(false);
        // 카파·등대지기 해금 선행 배지. 얼굴(58) 오른쪽 아래 모서리에 겹쳐 두고 코드가 퀘스트마다 켠다.
        var badges = Child(trader.transform, "Badges");
        var brt = (RectTransform)badges.transform;
        brt.anchorMin = brt.anchorMax = new Vector2(0.5f, 0.5f);
        brt.pivot = new Vector2(1, 0);
        brt.anchoredPosition = new Vector2(29, -29);
        Img(badges, BadgeBg).raycastTarget = false;
        HLayout(badges, 1, 1, 1, 1, 1, TextAnchor.MiddleCenter, false, false);
        Fit(badges);
        GoalIcon(badges.transform, "Kappa", KappaIconFile, 20);
        GoalIcon(badges.transform, "Lightkeeper", LighthouseIconFile, 20);
        badges.SetActive(false);

        var body = Child(q.transform, "Body");
        Layout(body, flexW: 1, minW: 0);
        VLayout(body, 12, 12, 8, 8, 4, TextAnchor.MiddleLeft, true, false);

        var titleRow = Child(body.transform, "Title");
        HLayout(titleRow, 0, 0, 0, 0, 8, TextAnchor.MiddleLeft, false, false);
        // 이름은 글 폭만큼만 차지해 마커와 손 아이콘이 이름 끝에 붙고, 남는 폭은 Fill 이 받는다.
        // 폭이 모자라면 이름만 줄어든다.
        const float titleSize = 16;
        Layout(Label(titleRow.transform, "Name", title, titleSize, TextBright, TextAlignmentOptions.Left).gameObject, flexW: 0);
        // 지도 번호. 원 지름을 이름 글자 크기와 같게 한다. 평소엔 테두리와 번호만 보이고,
        // 강조되면 코드가 Fill 을 채우고 테두리와 번호를 어둡게 바꾼다.
        var mk = Child(titleRow.transform, "Marker");
        Layout(mk, prefW: titleSize, prefH: titleSize, minH: titleSize, minW: titleSize);
        var mkFill = Child(mk.transform, "Fill");
        Stretch(mkFill);
        ((RectTransform)mkFill.transform).offsetMin = new Vector2(1, 1); // 가장자리를 테두리 밑으로 숨긴다
        ((RectTransform)mkFill.transform).offsetMax = new Vector2(-1, -1);
        var mkFillImg = Img(mkFill, Color.clear);
        mkFillImg.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
        mkFillImg.raycastTarget = false;
        var mkRing = Child(mk.transform, "Ring");
        Stretch(mkRing);
        var mkRingImg = Img(mkRing, MarkerInk);
        mkRingImg.sprite = IconSprite(MarkerRingFile);
        mkRingImg.raycastTarget = false;
        MarkerNumber(mk.transform, marker ?? "1", 11);
        mk.SetActive(marker != null);
        // 손 아이콘. 지금 건네줄 수 있는 퀘스트만 코드가 켜고 게임의 퀘스트 종류 아이콘을 하늘색으로 넣는다.
        var hand = Child(titleRow.transform, "HandIcon");
        var handImg = Img(hand, Color.cyan);
        handImg.preserveAspect = true;
        handImg.raycastTarget = false;
        Layout(hand, prefW: 19, prefH: 19, minH: 19, minW: 19);
        hand.SetActive(false);
        Layout(Child(titleRow.transform, "Fill"), flexW: 1, minW: 0);

        Label(body.transform, "Summary", summary, 12.5f, TextDim, TextAlignmentOptions.Left);

        var prog = Child(q.transform, "Progress");
        Layout(prog, prefW: 112, minW: 112);
        VLayout(prog, 10, 10, 0, 0, 6, TextAnchor.MiddleCenter, false, false);
        Label(prog.transform, "Value", progress, 20, Hex("#d5d7d2"), TextAlignmentOptions.Center);
        var bar = Child(prog.transform, "Bar");
        Img(bar, ProgressBg).raycastTarget = false;
        Layout(bar, prefW: 88, prefH: 6, minH: 6);
        var fill = Child(bar.transform, "Fill");
        var fillRt = (RectTransform)fill.transform;
        fillRt.anchorMin = Vector2.zero;
        fillRt.anchorMax = new Vector2(fraction, 1);
        fillRt.offsetMin = fillRt.offsetMax = Vector2.zero;
        Img(fill, ProgressFill).raycastTarget = false;
        return q;
    }

    // ── 퀘스트 펼침 ──
    // 펼친 퀘스트 줄 바로 아래 형제로 복제되는 블록. 안의 ObjectiveTemplate·HandoverTemplate 은 블록을 복제한 뒤 그 안에서 다시 복제한다.
    // withSamples 면 에디터 미리보기용 줄을 채운다. SampleDetail 은 이름이 Sample 로 시작해 불러올 때 지워진다.
    private static GameObject Detail(Transform parent, string name, bool withSamples)
    {
        var d = Child(parent, name);
        Img(d, DetailBg).raycastTarget = false;
        // 퀘스트 줄과 맞춘다. 얼굴 칸 76 에 여백 12 를 더해 글이 x=88 에서 시작하고, 오른쪽 112 는 진행 수치 칸이다.
        // 핀과 체크 자리(17)를 얼굴 칸 끝(x=84)에 맞추려고 왼쪽 여백을 67 로 둔다.
        VLayout(d, 67, 0, 8, 10, 6, TextAnchor.UpperLeft, true, false);

        // 줄 배지의 뜻. 해당하는 것만 코드가 켜고 둘 다 아니면 줄째 끈다.
        var goals = Child(d.transform, "Goals");
        HLayout(goals, 0, 0, 0, 2, 18, TextAnchor.MiddleLeft, false, false);
        GoalLegend(goals.transform, "Kappa", KappaIconFile, "Needed to unlock Kappa");
        GoalLegend(goals.transform, "Lightkeeper", LighthouseIconFile, "Needed to unlock Lightkeeper");
        goals.SetActive(withSamples);

        // 두 줄 템플릿을 한 컨테이너에 둬서 줄이 게임 목표 순서대로 섞여 쌓인다.
        var objectives = Child(d.transform, "Objectives");
        VLayout(objectives, 0, 0, 0, 0, 3, TextAnchor.UpperLeft, true, false);
        Objective(objectives.transform, "ObjectiveTemplate", true, "Objective text", null, "0 / 1", false).SetActive(false);
        Handover(objectives.transform, "HandoverTemplate", "Hand over the item", null, "0 / 1").SetActive(false);

        var actions = Child(d.transform, "Actions");
        HLayout(actions, 0, 0, 2, 0, 8, TextAnchor.MiddleRight, false, false);
        // "완료하러 가기"는 처음엔 꺼 두고 "완료 가능" 묶음에서만 코드가 켠다. "임무 탭에서 보기" 왼쪽에 온다.
        ActionButton(actions.transform, "TurnIn", "Turn in", 150).SetActive(false);
        ActionButton(actions.transform, "OpenTasks", "Open in Tasks", 150);

        if (withSamples)
        {
            Objective(objectives.transform, "SampleObjective1", false, "Find in raid", "Fleece fabric", "5 / 5", true);
            Objective(objectives.transform, "SampleObjective2", true, "Eliminate any target inside the industrial plant on Customs x15", null, "6 / 15", false);
            Objective(objectives.transform, "SampleObjective3", false, "Find in raid", "Fleece fabric, Ripstop fabric", "2 / 5", false);
            Handover(objectives.transform, "SampleHandover1", "Hand over the found in raid item", "Flash drive", "1 / 2");
        }
        return d;
    }

    // 카파·등대지기 아이콘 한 칸
    private static GameObject GoalIcon(Transform parent, string name, string file, float size)
    {
        var go = Child(parent, name);
        var img = Img(go, Color.white);
        img.sprite = IconSprite(file);
        img.preserveAspect = true;
        img.raycastTarget = false;
        Layout(go, prefW: size, prefH: size, minH: size, minW: size);
        return go;
    }

    // 펼침 맨 위의 아이콘 + 뜻 한 쌍. 글은 코드가 언어에 맞춰 바꾼다.
    private static GameObject GoalLegend(Transform parent, string name, string file, string text)
    {
        var go = Child(parent, name);
        HLayout(go, 0, 0, 0, 0, 6, TextAnchor.MiddleLeft, false, false);
        GoalIcon(go.transform, "Icon", file, 21);
        Label(go.transform, "Label", text, 13, GoalText, TextAlignmentOptions.Left);
        return go;
    }

    // 건네주기 버튼이 없는 줄. 핀, 체크, 색은 코드가 줄 종류에 맞춰 바꾼다. onMap, done, items 는 미리보기용 값이다.
    private static GameObject Objective(Transform parent, string name, bool onMap, string text, string items,
        string progress, bool done)
    {
        var o = Child(parent, name);
        Layout(o, minH: 24);
        HLayout(o, 0, 0, 2, 2, 4, TextAnchor.UpperLeft, false, false); // 마커(67~84)와 간격 4 다음 글이 x=88

        // 지도에 있는 목표는 이 자리에 핀을 켠다. 핀이 없는 줄도 자리는 켜 둬서 글 시작을 맞추고, 끝난 줄은 이 자리에 체크를 켠다.
        var mk = Child(o.transform, "Marker");
        var mkImg = Img(mk, onMap && !done ? MarkerInk : Color.clear);
        mkImg.sprite = IconSprite(PinFile);
        mkImg.preserveAspect = true;
        mkImg.raycastTarget = false;
        Layout(mk, prefW: 17, prefH: 17, minH: 17, minW: 17);
        Check(mk.transform, done);

        Layout(Body(o.transform, text, items, done, 0), flexW: 1);
        var v = ValueLabel(o.transform, progress, TextAlignmentOptions.Top);
        if (done) v.color = TextDim;
        return o;
    }

    private static GameObject Handover(Transform parent, string name, string text, string items, string progress)
    {
        var h = Child(parent, name);
        Layout(h, minH: 30);
        // 목표 줄처럼 빈 마커 자리를 둬서 글이 x=88 에서 시작한다. 글과 버튼 사이는 Main 의 오른쪽 여백까지 8.
        HLayout(h, 0, 0, 2, 2, 4, TextAnchor.MiddleLeft, false, false);

        var mk = Child(h.transform, "Marker");
        Layout(mk, prefW: 17, prefH: 17, minH: 17, minW: 17);
        Check(mk.transform, false);

        Layout(Body(h.transform, text, items, false, 4), flexW: 1);
        ActionButton(h.transform, "Give", "Hand over", 104);
        ValueLabel(h.transform, progress, TextAlignmentOptions.Center); // 글, 건네주기, 수치 순서
        return h;
    }

    // 끝난 줄의 체크. 처음엔 꺼 두고, 내장 Checkmark 스프라이트를 흐린 색으로 쓴다.
    private static void Check(Transform marker, bool on)
    {
        var c = Child(marker, "Check");
        var img = Img(c, TextDim);
        img.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Checkmark.psd");
        img.preserveAspect = true;
        img.raycastTarget = false;
        Stretch(c);
        c.SetActive(on);
    }

    // 글(Text)과 그 아래 작은 아이템 이름 줄(Items). 끝난 줄의 글 색도 Items 색을 쓴다.
    // 선호 높이로 크기가 정해지는 글에 말줄임을 쓰면 게임에서 첫 줄이 통째로 지워져서 둘 다 줄바꿈과 Overflow 로 둔다.
    private static GameObject Body(Transform parent, string text, string items, bool done, int rightPadding)
    {
        var main = Child(parent, "Main");
        VLayout(main, 0, rightPadding, 0, 0, 1, TextAnchor.UpperLeft, true, false);
        var t = Label(main.transform, "Text", text, 13.5f, done ? TextDim : TextMain, TextAlignmentOptions.TopLeft);
        t.enableWordWrapping = true;
        t.overflowMode = TextOverflowModes.Overflow;
        var i = Label(main.transform, "Items", items ?? "Item name", 12f, TextDim, TextAlignmentOptions.TopLeft);
        i.enableWordWrapping = true;
        i.overflowMode = TextOverflowModes.Overflow;
        i.gameObject.SetActive(items != null);
        return main;
    }

    // 퀘스트 줄 진행 칸(112)과 같은 폭에 가운데 정렬이라 위 줄 수치 바로 아래에 온다.
    // 고정 폭에 말줄임이면 "2971 / 5..." 처럼 잘려서, 길면 글자 폭만큼 자라고 Text 쪽이 양보한다.
    private static TextMeshProUGUI ValueLabel(Transform parent, string progress, TextAlignmentOptions align)
    {
        var v = Label(parent, "Value", progress, 13.5f, TextDim, align);
        v.overflowMode = TextOverflowModes.Overflow;
        v.enableWordWrapping = false;
        Layout(v.gameObject, prefW: 112, minW: 112);
        return v;
    }

    private static GameObject ActionButton(Transform parent, string name, string label, float width)
    {
        var b = Child(parent, name);
        var bg = Img(b, ButtonBg);
        b.AddComponent<Button>().targetGraphic = bg;
        Layout(b, prefW: width, prefH: 28, minH: 28, minW: Mathf.Min(width, 120));
        Stretch(Label(b.transform, "Label", label, 13.5f, TextMain, TextAlignmentOptions.Center).gameObject);
        return b;
    }

    private static GameObject Empty(Transform parent, string name)
    {
        var t = Label(parent, name, "No quests on this map", 14, TextFaint, TextAlignmentOptions.Center);
        Layout(t.gameObject, prefH: 48, minH: 48);
        return t.gameObject;
    }

    // ── 지도 칸 ──
    // Viewport 는 휠·끌기를 받는 투명 바탕, Content 는 확대·이동 대상이고 크기와 위치는 코드가 정한다.
    // Content 아래는 Layers, Areas, Doors, Exits, Markers 순으로 쌓이고 층 SVG 와 구역은 코드가 붙인다.
    // 그 위에 층 버튼, 체크박스, 확대 버튼, 출처, 메시지, 말풍선이 있다. Icons 는 PNG 를 번들에 싣기 위한 꺼 둔 그릇이다.
    private static void BuildMapColumn(Transform columns)
    {
        var col = Child(columns, "ColMap");
        Img(col, MapBg);
        Layout(col, flexW: 1);
        var c = col.transform;

        var viewport = Child(c, "Viewport", typeof(RectMask2D));
        Stretch(viewport);
        Img(viewport, new Color(0, 0, 0, 0)); // 투명하지만 휠·끌기·더블클릭을 받는다

        var content = Child(viewport.transform, "Content");
        var crt = (RectTransform)content.transform;
        crt.anchorMin = crt.anchorMax = new Vector2(0.5f, 0.5f);
        crt.pivot = new Vector2(0, 1);
        crt.sizeDelta = new Vector2(600, 300);
        crt.anchoredPosition = new Vector2(-300, 150);
        foreach (var name in new[] { "Layers", "Areas", "Doors", "Exits", "Markers" }) Stretch(Child(content.transform, name));
        var ct = content.transform;

        var door = Icon(ct.Find("Doors"), "DoorTemplate", "door_with_lock.png", 26);
        door.GetComponent<Image>().color = DoorLocked; // 곱하기라 흰 도형만 빨갛게 되고 검은 외곽선은 그대로다(DynamicMaps 와 같다)
        door.SetActive(false);
        Icon(ct.Find("Exits"), "ExitTemplate", "extract_pmc.png", 26).SetActive(false);
        Marker(ct.Find("Markers"), "MarkerTemplate").SetActive(false);

        var floors = Child(c, "Floors");
        Corner(floors, new Vector2(0, 1), new Vector2(14, -14));
        VLayout(floors, 0, 0, 0, 0, 6, TextAnchor.UpperLeft, false, false);
        Fit(floors);
        Floor(floors.transform, "FloorTemplate", "Ground", false).SetActive(false);
        Floor(floors.transform, "SampleFloor1", "2F", false);
        Floor(floors.transform, "SampleFloor2", "Ground", true);
        Floor(floors.transform, "SampleFloor3", "Underground", false);

        var toggles = Child(c, "LayerToggles");
        Corner(toggles, new Vector2(1, 1), new Vector2(-14, -14));
        HLayout(toggles, 0, 0, 0, 0, 6, TextAnchor.UpperRight, false, false);
        Fit(toggles);
        ExitsDropdown(toggles.transform);
        LayerToggle(toggles.transform, "Doors", "Locked doors");
        LayerToggle(toggles.transform, "Areas", "Quest zones");

        // 목록 밖을 누르면 닫는 투명한 막(Unity Dropdown 의 Blocker 와 같다). 처음엔 꺼 두고 열 때 코드가 켠다.
        // 하위 Canvas 로 지도와 체크박스 위에 그리고, 목록은 그보다 위에 그린다.
        var blocker = Child(c, "ExitsBlocker");
        Stretch(blocker);
        var blockerImg = Img(blocker, new Color(0, 0, 0, 0));
        blocker.AddComponent<Button>().targetGraphic = blockerImg;
        AddSubCanvas(blocker);
        blocker.SetActive(false);

        var zoom = Child(c, "Zoom");
        Corner(zoom, new Vector2(0, 0), new Vector2(14, 16));
        VLayout(zoom, 0, 0, 0, 0, 4, TextAnchor.LowerLeft, false, false);
        Fit(zoom);
        ZoomButton(zoom.transform, "In", "+");
        ZoomButton(zoom.transform, "Out", "-");

        var credit = Label(c, "Credit", "Map: Shebuka (CC BY-NC-SA 4.0)", 11, TextFaint, TextAlignmentOptions.BottomRight);
        credit.overflowMode = TextOverflowModes.Overflow;
        Corner(credit.gameObject, new Vector2(1, 0), new Vector2(-10, 6));
        ((RectTransform)credit.transform).sizeDelta = new Vector2(700, 18);

        var message = Label(c, "Message", "Loading map...", 18, TextFaint, TextAlignmentOptions.Center);
        message.enableWordWrapping = true;
        message.overflowMode = TextOverflowModes.Overflow;
        Stretch(message.gameObject);

        BuildTooltip(c);

        var icons = Child(c, "Icons");
        foreach (var (name, file) in new[] { ("Pmc", "extract_pmc.png"), ("Shared", "extract_shared.png"), ("Scav", "extract_scav.png"),
                     ("Transit", "extract_transit.png"), ("Door", "door_with_lock.png") })
            Icon(icons.transform, name, file, 26);
        icons.SetActive(false);
    }

    // 지도 위 아이콘(탈출구·잠긴 문). 마우스 올림 말풍선을 받으려고 raycastTarget 을 켠다. 위치는 코드가 앵커로 정한다.
    private static GameObject Icon(Transform parent, string name, string file, float size)
    {
        var go = Child(parent, name);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(size, size);
        var img = Img(go, Color.white);
        img.sprite = IconSprite(file);
        img.preserveAspect = true;
        return go;
    }

    // 밝은 고리 안에 어두운 원(Fill)과 번호. 강조되면 코드가 색을 뒤집는다. 확대해도 화면에서 같은 크기로 보이게 코드가 배율로 나눈다.
    private static GameObject Marker(Transform parent, string name)
    {
        var go = Child(parent, name);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(22, 22);
        Img(go, MarkerInk).sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
        var fill = Child(go.transform, "Fill");
        Stretch(fill);
        ((RectTransform)fill.transform).offsetMin = new Vector2(2, 2);
        ((RectTransform)fill.transform).offsetMax = new Vector2(-2, -2);
        var fillImg = Img(fill, MarkerDark);
        fillImg.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
        fillImg.raycastTarget = false;
        MarkerNumber(go.transform, "1", 12);
        return go;
    }

    // 마커 번호. 줄 높이가 아니라 글자 모양을 기준으로 가운데에 놓아 숫자가 원 한가운데 오게 한다.
    private static TextMeshProUGUI MarkerNumber(Transform parent, string text, float size)
    {
        var t = Label(parent, "Number", text, size, MarkerInk, TextAlignmentOptions.MidlineGeoAligned);
        t.overflowMode = TextOverflowModes.Overflow;
        Stretch(t.gameObject);
        return t;
    }

    private static GameObject Floor(Transform parent, string name, string label, bool selected)
    {
        var f = Child(parent, name);
        var bg = Img(f, OverlayBg);
        f.AddComponent<Button>().targetGraphic = bg;
        Layout(f, prefW: 92, prefH: 30, minH: 30);
        var on = Child(f.transform, "Selected");
        Stretch(on);
        Img(on, FloorOnBg).raycastTarget = false;
        on.SetActive(selected);
        var t = Label(f.transform, "Label", label, 14, selected ? Hex("#161817") : FloorText, TextAlignmentOptions.Center);
        t.overflowMode = TextOverflowModes.Overflow;
        Stretch(t.gameObject);
        // 다른 층에 퀘스트 마커가 있으면 켜는 점. 웹 .qc-map__dot 처럼 버튼 오른쪽 위 모서리에 걸치고, 색은 마커와 같은 톤이다.
        var dot = Child(f.transform, "Dot");
        var drt = (RectTransform)dot.transform;
        drt.anchorMin = drt.anchorMax = new Vector2(1, 1);
        drt.sizeDelta = new Vector2(8, 8);
        drt.anchoredPosition = new Vector2(-1, -1);
        var dotImg = Img(dot, MarkerInk);
        dotImg.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
        dotImg.raycastTarget = false;
        dot.SetActive(false);
        return f;
    }

    // 탈출구 드롭다운. 다른 체크박스와 같은 모양에 오른쪽 작은 화살표가 붙고, 누르면 아래로 ExitsMenu 가 열린다.
    // 버튼의 체크 모양은 코드가 바꾼다. 목록은 버튼 오른쪽 아래에 맞추고 레이아웃에서 뺀다.
    private static void ExitsDropdown(Transform parent)
    {
        var t = Child(parent, "Exits");
        var bg = Img(t, OverlayBg);
        t.AddComponent<Button>().targetGraphic = bg;
        HLayout(t, 10, 10, 5, 5, 6, TextAnchor.MiddleLeft, false, false);
        CheckBox(t, "Extracts");
        var arrow = Child(t.transform, "Arrow");
        Layout(arrow, prefW: 10, prefH: 10, minW: 10, minH: 10);
        var arrowImg = Img(arrow, FloorText);
        arrowImg.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/DropdownArrow.psd");
        arrowImg.raycastTarget = false;

        var menu = Child(t.transform, "ExitsMenu");
        var mrt = (RectTransform)menu.transform;
        mrt.anchorMin = mrt.anchorMax = new Vector2(1, 0);
        mrt.pivot = new Vector2(1, 1);
        mrt.anchoredPosition = new Vector2(0, -4);
        menu.AddComponent<LayoutElement>().ignoreLayout = true;
        VLayout(menu, 0, 0, 0, 0, 2, TextAnchor.UpperRight, true, false);
        Fit(menu);
        LayerToggle(menu.transform, "Pmc", "PMC");
        LayerToggle(menu.transform, "Scav", "Scav");
        LayerToggle(menu.transform, "Transit", "Transit");
        AddSubCanvas(menu);
        menu.SetActive(false);
    }

    // 하위 Canvas 와 GraphicRaycaster 자리만 만든다. 만들 때는 루트 캔버스라 overrideSorting 이 저장되지 않아서
    // 정렬 값은 MapView.RaiseExitsMenu 가 열 때 정한다.
    private static void AddSubCanvas(GameObject go)
    {
        go.AddComponent<Canvas>();
        go.AddComponent<GraphicRaycaster>();
    }

    // 체크 칸과 이름. 체크박스와 드롭다운 버튼이 같이 쓰고 클릭은 부모가 받는다.
    private static Image CheckBox(GameObject t, string label)
    {
        var box = Child(t.transform, "Box");
        Img(box, ProgressBg).raycastTarget = false;
        Layout(box, prefW: 14, prefH: 14, minW: 14, minH: 14);
        var check = Child(box.transform, "Check");
        Stretch(check);
        var checkImg = Img(check, FloorText);
        checkImg.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Checkmark.psd");
        checkImg.raycastTarget = false;
        Label(t.transform, "Label", label, 13, FloorText, TextAlignmentOptions.Left).overflowMode = TextOverflowModes.Overflow;
        return checkImg;
    }

    private static void LayerToggle(Transform parent, string name, string label)
    {
        var t = Child(parent, name);
        var bg = Img(t, OverlayBg);
        HLayout(t, 10, 10, 5, 5, 6, TextAnchor.MiddleLeft, false, false);
        var checkImg = CheckBox(t, label);
        var toggle = t.AddComponent<Toggle>();
        toggle.targetGraphic = bg;
        toggle.graphic = checkImg;
        toggle.isOn = true;
    }

    private static void ZoomButton(Transform parent, string name, string label)
    {
        var b = Child(parent, name);
        var bg = Img(b, OverlayBg);
        b.AddComponent<Button>().targetGraphic = bg;
        Layout(b, prefW: 30, prefH: 30, minW: 30, minH: 30);
        var t = Label(b.transform, "Label", label, 22, TextBright, TextAlignmentOptions.Center);
        t.overflowMode = TextOverflowModes.Overflow;
        Stretch(t.gameObject);
    }

    // 탈출구와 잠긴 문 말풍선. 위치는 코드가 아이콘 위로 옮긴다.
    private static void BuildTooltip(Transform col)
    {
        var tip = Child(col, "Tooltip");
        var rt = (RectTransform)tip.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0);
        Img(tip, OverlayBg).raycastTarget = false;
        VLayout(tip, 8, 8, 5, 5, 1, TextAnchor.UpperLeft, false, false);
        Fit(tip);
        Label(tip.transform, "Title", "Crossroads · PMC", 13, TextBright, TextAlignmentOptions.Left).overflowMode = TextOverflowModes.Overflow;
        Label(tip.transform, "Detail", "50% chance", 12, TextDim, TextAlignmentOptions.Left).overflowMode = TextOverflowModes.Overflow;
        tip.SetActive(false);
    }

    // 칸 모서리에 붙는 상자. 크기는 ContentSizeFitter 가 정하고 corner 는 앵커와 pivot 이 같은 모서리다.
    private static void Corner(GameObject go, Vector2 corner, Vector2 offset)
    {
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = rt.pivot = corner;
        rt.anchoredPosition = offset;
    }

    private static void Fit(GameObject go)
    {
        var f = go.AddComponent<ContentSizeFitter>();
        f.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        f.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
    }

    // Assets/Briefing/Icons/<file> 을 Sprite 로 가져온다. 처음이면 가져오기 설정을 Sprite 로 바꿔 다시 가져온다.
    private static Sprite IconSprite(string file)
    {
        var path = IconFolder + file;
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) throw new System.Exception("icon missing: " + path);
        if (importer.textureType != TextureImporterType.Sprite || importer.spriteImportMode != SpriteImportMode.Single || importer.mipmapEnabled)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.SaveAndReimport();
        }
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (sprite == null) throw new System.Exception("icon is not a sprite: " + path);
        return sprite;
    }

    // ── 오른쪽 열 ──
    // 탭 줄, 필터 띠(처음엔 꺼짐), 세로 스크롤 본문. Content 아래는 모두 코드가 복제할 꺼 둔 템플릿이다.
    // 게임 한글 폴백 글꼴에 ✓·⚠ 가 없어서 판정 표시와 FIR 고리는 이 스크립트가 PNG 로 그려 Icons 에 싣는다.
    private static void BuildRightColumn(Transform columns)
    {
        var col = Child(columns, "ColRight");
        Img(col, PanelBg);
        Layout(col, prefW: 470); // 실제 폭은 ColumnResize 가 정한다
        VLayout(col, 0, 0, 0, 0, 0, TextAnchor.UpperLeft, true, false);
        var c = col.transform;

        var tabs = Child(c, "Tabs");
        Img(tabs, HeaderBg);
        Layout(tabs, prefH: 46, minH: 46, flexH: 0);
        HLayout(tabs, 10, 10, 8, 8, 4, TextAnchor.MiddleLeft, false, true);
        RightTab(tabs.transform, "Bring", "Bring", "2", true);
        RightTab(tabs.transform, "Find", "Find", "15", false);
        RightTab(tabs.transform, "Gear", "Gear", "7", false);

        var filter = Child(c, "Filter");
        Img(filter, FilterBg);
        Layout(filter, minH: 32, flexH: 0);
        HLayout(filter, 12, 8, 4, 4, 8, TextAnchor.MiddleLeft, false, false);
        var ft = Label(filter.transform, "Text", "Selected quest only: Hobby Club", 13, Gold, TextAlignmentOptions.Left);
        Layout(ft.gameObject, flexW: 1, minW: 0);
        var clear = Child(filter.transform, "Clear");
        var clearBg = Img(clear, new Color(0, 0, 0, 0));
        clear.AddComponent<Button>().targetGraphic = clearBg;
        HLayout(clear, 6, 6, 2, 2, 0, TextAnchor.MiddleCenter, false, false);
        Label(clear.transform, "Label", "Show all ×", 13, Gold, TextAlignmentOptions.Center).overflowMode = TextOverflowModes.Overflow;
        filter.SetActive(false);

        var scroll = Child(c, "Scroll");
        Img(scroll, new Color(0, 0, 0, 0)); // 휠·끌기를 받는 영역
        Layout(scroll, flexH: 1);
        var sr = scroll.AddComponent<ScrollRect>();
        var viewport = Child(scroll.transform, "Viewport", typeof(RectMask2D));
        Stretch(viewport);
        var content = Child(viewport.transform, "Content");
        var crt = (RectTransform)content.transform;
        crt.anchorMin = new Vector2(0, 1);
        crt.anchorMax = new Vector2(1, 1);
        crt.pivot = new Vector2(0.5f, 1);
        crt.offsetMin = crt.offsetMax = Vector2.zero;
        VLayout(content, 0, 0, 0, 0, 0, TextAnchor.UpperLeft, true, false);
        content.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        sr.viewport = (RectTransform)viewport.transform;
        sr.content = crt;
        sr.horizontal = false;
        sr.vertical = true;
        sr.movementType = ScrollRect.MovementType.Clamped;
        sr.scrollSensitivity = 30;
        BuildScrollbar(scroll.transform, sr);

        var ct = content.transform;
        RightSection(ct, "SectionHeader", "Quest items to bring", "2 short").SetActive(false);
        RightEmpty(ct, "Empty", "Nothing to bring").SetActive(false);
        ItemBoard(ct, "Grid", false).SetActive(false);
        Loadout(ct, "Loadout", false).SetActive(false);
        GearBlock(ct, "GearBlock", false).SetActive(false);

        RightSection(ct, "SampleSection", "Quest items to bring", "1 short");
        ItemBoard(ct, "SampleGrid", true);
        RightSection(ct, "SampleLoadoutHeader", "Current gear", null);
        Loadout(ct, "SampleLoadout", true);
        GearBlock(ct, "SampleGearBlock", true);

        var icons = Child(c, "Icons");
        foreach (var (name, file) in new[] { ("MarkOk", MarkOkFile), ("MarkStash", MarkStashFile), ("MarkWarn", MarkWarnFile), ("Ring", RingFile) })
            Icon(icons.transform, name, file, 16);
        icons.SetActive(false);
    }

    private static void RightTab(Transform parent, string name, string label, string count, bool selected)
    {
        var t = Child(parent, name);
        var bg = Img(t, new Color(0, 0, 0, 0));
        t.AddComponent<Button>().targetGraphic = bg;
        HLayout(t, 12, 12, 0, 0, 4, TextAnchor.MiddleCenter, false, false);
        var on = Child(t.transform, "Selected");
        Stretch(on);
        Img(on, ChipOnBg).raycastTarget = false;
        Layout(on).ignoreLayout = true;
        on.SetActive(selected);
        // 폭 300 에서도 숫자까지 한 줄에 들어가게 글자 크기만큼만 차지하고 말줄임은 쓰지 않는다.
        Label(t.transform, "Label", label, 15, selected ? ChipTextOn : ChipText, TextAlignmentOptions.Left).overflowMode = TextOverflowModes.Overflow;
        Label(t.transform, "Count", count, 11, selected ? ChipCountOn : TextDim, TextAlignmentOptions.Left).overflowMode = TextOverflowModes.Overflow;
    }

    // 왼쪽에 제목, 오른쪽에 빨간 메모("부족 N"). 메모가 없으면 코드가 끈다.
    private static GameObject RightSection(Transform parent, string name, string title, string note)
    {
        var s = Child(parent, name);
        Img(s, HeaderBg).raycastTarget = false;
        Layout(s, prefH: 36, minH: 36);
        HLayout(s, 12, 12, 0, 0, 8, TextAnchor.MiddleLeft, false, true);
        Layout(Label(s.transform, "Title", title, 14, TextBright, TextAlignmentOptions.Left).gameObject, flexW: 1, minW: 0);
        var n = Label(s.transform, "Note", note ?? "", 13, ShortRed, TextAlignmentOptions.Right);
        n.overflowMode = TextOverflowModes.Overflow;
        n.gameObject.SetActive(note != null);
        // 범례는 장비 탭 "무기·장비 조건" 머리에서만 코드가 켠다. 제목이 남는 폭을 가져가서 오른쪽 끝에 붙는다.
        var legend = Child(s.transform, "Legend");
        HLayout(legend, 0, 0, 0, 0, 8, TextAnchor.MiddleRight, false, false);
        LegendItem(legend.transform, "Ok", MarkOkFile, HaveGreen, "Equipped");
        LegendItem(legend.transform, "Stash", MarkStashFile, StashCyan, "Stash");
        LegendItem(legend.transform, "Warn", MarkWarnFile, WarnOrange, "Missing");
        legend.SetActive(false);
        return s;
    }

    // 표시(14×14)와 낱말. 고정 칸에서 Ellipsis 는 첫 줄을 지울 수 있어 Overflow 로 둔다.
    private static void LegendItem(Transform parent, string name, string file, Color color, string text)
    {
        var item = Child(parent, name);
        HLayout(item, 0, 0, 0, 0, 4, TextAnchor.MiddleLeft, false, false);
        MarkImage(item.transform, "Mark", file, color);
        Label(item.transform, "Text", text, 11, TextDim, TextAlignmentOptions.Left).overflowMode = TextOverflowModes.Overflow;
    }

    private static GameObject RightEmpty(Transform parent, string name, string text)
    {
        var t = Label(parent, name, text, 13, TextDim, TextAlignmentOptions.TopLeft);
        t.enableWordWrapping = true;
        t.overflowMode = TextOverflowModes.Overflow;
        t.margin = new Vector4(12, 10, 12, 14);
        return t.gameObject;
    }

    // Board 는 격자선 색 바탕이고 크기와 칸 위치는 코드가 정한다. 칸 한 변은 게임 격자와 같은 63 이고,
    // 칸 사이 1px 틈으로 바탕이 비쳐 격자선이 된다.
    private static GameObject ItemBoard(Transform parent, string name, bool sample)
    {
        var g = Child(parent, name);
        HLayout(g, 12, 12, 10, 14, 0, TextAnchor.UpperLeft, false, false);
        var board = Child(g.transform, "Board");
        Img(board, GridLine).raycastTarget = false;
        var cols = sample ? 4 : 2;
        var rows = sample ? 2 : 1;
        Layout(board, prefW: cols * 63 + 1, prefH: rows * 63 + 1, minW: cols * 63 + 1, minH: rows * 63 + 1);
        var bt = board.transform;
        BoardPiece(BlankCell(bt, "Blank"), 0, 0, 1, 1).SetActive(false);
        BoardPiece(ItemCell(bt, "ItemCell", "AK-50", "0/1", true, false, 0), 0, 0, 1, 1).SetActive(false);
        if (sample)
        {
            BoardPiece(ItemCell(bt, "SampleCell1", "AK-50", "0/1", true, false, 0), 0, 0, 2, 2);
            BoardPiece(ItemCell(bt, "SampleCell2", "MS2000", "1/1", false, false, 0), 2, 0, 1, 1);
            BoardPiece(ItemCell(bt, "SampleCell3", "TV-115", "3/50", true, false, 4), 3, 0, 1, 1);
            BoardPiece(ItemCell(bt, "SampleCell4", "Duct", "0/3", true, true, 0), 2, 1, 1, 1);
            BoardPiece(BlankCell(bt, "SampleBlank"), 3, 1, 1, 1);
        }
        return g;
    }

    private static GameObject BoardPiece(GameObject go, int x, int y, int w, int h)
    {
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0, 1);
        rt.anchoredPosition = new Vector2(x * 63 + 1, -(y * 63 + 1));
        rt.sizeDelta = new Vector2(w * 63 - 1, h * 63 - 1);
        return go;
    }

    private static GameObject BlankCell(Transform parent, string name)
    {
        var b = Child(parent, name);
        Img(b, CellBg).raycastTarget = false;
        return b;
    }

    // 바탕색은 코드가 부족이면 빨강, 충분하면 초록으로 바꾸고 말풍선을 위해 raycast 를 켠다.
    // 오른쪽 위에 짧은 이름, 왼쪽 아래에 "보유/필요", 오른쪽 아래에 FIR 고리나 금색 "+N".
    private static GameObject ItemCell(Transform parent, string name, string shortName, string count, bool missing, bool fir, int alt)
    {
        var cell = Child(parent, name);
        Img(cell, missing ? CellShort : CellOk);
        var icon = Child(cell.transform, "Icon");
        Stretch(icon);
        ((RectTransform)icon.transform).offsetMin = new Vector2(3, 3);
        ((RectTransform)icon.transform).offsetMax = new Vector2(-3, -3);
        var iconImg = Img(icon, Color.white);
        iconImg.preserveAspect = true;
        iconImg.raycastTarget = false;
        iconImg.enabled = false; // 아이콘이 오면 코드가 켠다
        var sn = Label(cell.transform, "Short", shortName, 11, CellText, TextAlignmentOptions.TopRight);
        CellCorner(sn.gameObject, new Vector2(1, 1), new Vector2(-3, -1), new Vector2(57, 16));
        var cnt = Label(cell.transform, "Count", count, 12.5f, missing ? ShortRed : HaveGreen, TextAlignmentOptions.BottomLeft);
        cnt.overflowMode = TextOverflowModes.Overflow;
        CellCorner(cnt.gameObject, new Vector2(0, 0), new Vector2(3, 1), new Vector2(57, 16));
        var ring = Child(cell.transform, "Fir");
        CellCorner(ring, new Vector2(1, 0), new Vector2(-3, 3), new Vector2(14, 14));
        var ringImg = Img(ring, Gold);
        ringImg.sprite = IconSprite(RingFile);
        ringImg.raycastTarget = false;
        ring.SetActive(fir && alt == 0);
        var a = Label(cell.transform, "Alt", "+" + alt, 10, Gold, TextAlignmentOptions.BottomRight);
        a.overflowMode = TextOverflowModes.Overflow;
        CellCorner(a.gameObject, new Vector2(1, 0), new Vector2(-3, 1), new Vector2(30, 14));
        a.gameObject.SetActive(alt > 0);
        return cell;
    }

    private static void CellCorner(GameObject go, Vector2 corner, Vector2 offset, Vector2 size)
    {
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = rt.pivot = corner;
        rt.anchoredPosition = offset;
        rt.sizeDelta = size;
    }

    // 현재 장비 줄. 96×46 슬롯을 폭에 맞춰 줄바꿈하고, 빈 슬롯은 코드가 어둡게 칠한다.
    private static GameObject Loadout(Transform parent, string name, bool sample)
    {
        var lo = Child(parent, name);
        var grid = lo.AddComponent<GridLayoutGroup>();
        grid.padding = new RectOffset(12, 12, 10, 12);
        grid.cellSize = new Vector2(96, 46);
        grid.spacing = new Vector2(6, 6);
        grid.startCorner = GridLayoutGroup.Corner.UpperLeft;
        grid.childAlignment = TextAnchor.UpperLeft;
        Slot(lo.transform, "Slot", "Primary", true).SetActive(false);
        if (sample)
        {
            Slot(lo.transform, "SampleSlot1", "Primary 1", true);
            Slot(lo.transform, "SampleSlot2", "Primary 2", false);
            Slot(lo.transform, "SampleSlot3", "Holster", true);
            Slot(lo.transform, "SampleSlot4", "Helmet", true);
            Slot(lo.transform, "SampleSlot5", "Body armor", false);
        }
        return lo;
    }

    private static GameObject Slot(Transform parent, string name, string label, bool filled)
    {
        var s = Child(parent, name);
        Img(s, filled ? SlotFilled : CellBg).raycastTarget = false;
        var icon = Child(s.transform, "Icon");
        Stretch(icon);
        ((RectTransform)icon.transform).offsetMin = new Vector2(6, 2);
        ((RectTransform)icon.transform).offsetMax = new Vector2(-6, -12);
        var img = Img(icon, Color.white);
        img.preserveAspect = true;
        img.raycastTarget = false;
        img.enabled = false;
        var l = Label(s.transform, "Label", label, 10.5f, TextDim, TextAlignmentOptions.TopLeft);
        l.overflowMode = TextOverflowModes.Overflow; // 한글 줄 높이가 14 를 넘으면 Ellipsis 가 첫 줄을 통째로 지운다
        CellCorner(l.gameObject, new Vector2(0, 1), new Vector2(4, -1), new Vector2(88, 14));
        return s;
    }

    // 퀘스트 하나의 장비 덩어리. 머리에는 이름만 두고 판정은 줄마다 붙는다. 특수 조건 덩어리도 이 블록을 쓴다.
    // RuleRow 는 라벨 칸(68)과 값(판정 표시, 대표 이름, 태그, "외 N종" 버튼, 그 아래 처음엔 꺼진 대안 상자). SpecialRow 는 라벨과 문장.
    private static GameObject GearBlock(Transform parent, string name, bool sample)
    {
        var b = Child(parent, name);
        VLayout(b, 12, 12, 9, 9, 4, TextAnchor.UpperLeft, true, false);
        var head = Child(b.transform, "Head");
        HLayout(head, 0, 0, 0, 0, 6, TextAnchor.MiddleLeft, false, false);
        var title = Label(head.transform, "Title", "Quest name", 13.5f, TextBright, TextAlignmentOptions.Left);
        title.fontStyle = FontStyles.Bold;
        title.enableWordWrapping = true;
        title.overflowMode = TextOverflowModes.Overflow;
        Layout(title.gameObject, flexW: 1, minW: 0);
        RuleRow(b.transform, "RuleRow", "Weapon", "B&T MP9 9x19 submachine gun", "equipped", "+21 more", false).SetActive(false);
        SpecialRow(b.transform, "SpecialRow", "Exit", "Survived · Transit").SetActive(false);
        if (sample)
        {
            RuleRow(b.transform, "SampleRule1", "Weapon", "B&T MP9 9x19 submachine gun", "equipped", "+21 more", true);
            RuleRow(b.transform, "SampleRule2", "Don't wear", "Not wearing any of these", null, "List 169", false);
        }
        // 덩어리 아래 구분선
        var line = Child(b.transform, "Line");
        Img(line, SoftLine).raycastTarget = false;
        Layout(line, prefH: 1, minH: 1);
        return b;
    }

    // 판정 표시 14×14. 칸을 옆 글줄 높이(boxH)만큼 키우면 스프라이트가 칸 가운데에 서서 글과 높이가 맞는다.
    private static Image MarkImage(Transform parent, string name, string file, Color color, float boxH = 14)
    {
        var m = Child(parent, name);
        var img = Img(m, color);
        img.sprite = IconSprite(file);
        img.preserveAspect = true;
        img.raycastTarget = false;
        Layout(m, prefW: 14, prefH: boxH, minW: 14, minH: boxH);
        return img;
    }

    // 글줄 높이 기본값(12.5pt 는 17, 12pt 는 16). 실제 높이는 글꼴마다 달라 RightColumnView.FitMarkToLine 이 잰 값으로 덮어쓰고,
    // 이 값은 재지 못했을 때만 쓰인다.
    private const float RuleLineH = 17f;
    private const float AltLineH = 16f;

    private static GameObject RuleRow(Transform parent, string name, string label, string lead, string tag, string more, bool open)
    {
        var r = Child(parent, name);
        HLayout(r, 0, 0, 0, 0, 6, TextAnchor.UpperLeft, false, false);
        var l = Label(r.transform, "Label", label, 12.5f, TextDim, TextAlignmentOptions.TopLeft);
        l.enableWordWrapping = true;
        l.overflowMode = TextOverflowModes.Overflow;
        Layout(l.gameObject, prefW: 68, minW: 68, flexW: 0);
        var value = Child(r.transform, "Value");
        Layout(value, flexW: 1, minW: 0);
        VLayout(value, 0, 0, 0, 0, 2, TextAnchor.UpperLeft, true, false);
        var lineRow = Child(value.transform, "Line");
        HLayout(lineRow, 0, 0, 0, 0, 5, TextAnchor.UpperLeft, false, false);
        MarkImage(lineRow.transform, "Mark", MarkOkFile, HaveGreen, RuleLineH);
        var leadText = Label(lineRow.transform, "Lead", lead, 12.5f, HaveGreen, TextAlignmentOptions.TopLeft);
        leadText.enableWordWrapping = true;
        leadText.overflowMode = TextOverflowModes.Overflow;
        Layout(leadText.gameObject, flexW: 1, minW: 0);
        var tagText = Label(lineRow.transform, "Tag", tag ?? "equipped", 11, TextDim, TextAlignmentOptions.TopLeft);
        tagText.overflowMode = TextOverflowModes.Overflow;
        tagText.gameObject.SetActive(tag != null);
        var moreBtn = Child(lineRow.transform, "More");
        var moreBg = Img(moreBtn, new Color(0, 0, 0, 0));
        moreBtn.AddComponent<Button>().targetGraphic = moreBg;
        HLayout(moreBtn, 0, 0, 0, 0, 0, TextAnchor.UpperLeft, false, false);
        Label(moreBtn.transform, "Label", more + " ▼", 12, LinkBlue, TextAlignmentOptions.TopLeft).overflowMode = TextOverflowModes.Overflow;

        // 대안이 수십 종이어도 열이 길어지지 않게 높이를 약 8줄(150)로 제한한 ScrollRect.
        var alts = Child(value.transform, "Alts");
        Img(alts, AltsBg);
        Layout(alts, prefH: 150, minH: 150, flexH: 0);
        var asr = alts.AddComponent<ScrollRect>();
        var aview = Child(alts.transform, "Viewport", typeof(RectMask2D));
        Stretch(aview);
        var acontent = Child(aview.transform, "Content");
        var acrt = (RectTransform)acontent.transform;
        acrt.anchorMin = new Vector2(0, 1);
        acrt.anchorMax = new Vector2(1, 1);
        acrt.pivot = new Vector2(0.5f, 1);
        acrt.offsetMin = acrt.offsetMax = Vector2.zero;
        VLayout(acontent, 8, 8, 4, 4, 1, TextAnchor.UpperLeft, true, false);
        acontent.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        asr.viewport = (RectTransform)aview.transform;
        asr.content = acrt;
        asr.horizontal = false;
        asr.vertical = true;
        asr.movementType = ScrollRect.MovementType.Clamped;
        asr.scrollSensitivity = 20;
        BuildScrollbar(alts.transform, asr);
        AltLine(acontent.transform, "AltTemplate", "Alternative name", true).SetActive(false);
        if (open)
            for (var i = 1; i <= 9; i++) AltLine(acontent.transform, "SampleAlt" + i, "Alternative " + i, i == 1);
        alts.SetActive(open);
        return r;
    }

    private static GameObject AltLine(Transform parent, string name, string text, bool hit)
    {
        var a = Child(parent, name);
        HLayout(a, 0, 0, 0, 0, 5, TextAnchor.MiddleLeft, false, false);
        var mark = MarkImage(a.transform, "Mark", MarkOkFile, HaveGreen, AltLineH);
        mark.gameObject.SetActive(hit);
        var t = Label(a.transform, "Text", text, 12, hit ? HaveGreen : FloorText, TextAlignmentOptions.Left);
        Layout(t.gameObject, flexW: 1, minW: 0);
        return a;
    }

    private static GameObject SpecialRow(Transform parent, string name, string label, string text)
    {
        var r = Child(parent, name);
        HLayout(r, 0, 0, 0, 0, 6, TextAnchor.UpperLeft, false, false);
        var l = Label(r.transform, "Label", label, 12.5f, TextDim, TextAlignmentOptions.TopLeft);
        Layout(l.gameObject, prefW: 68, minW: 68, flexW: 0);
        var t = Label(r.transform, "Text", text, 12.5f, TextMain, TextAlignmentOptions.TopLeft);
        t.enableWordWrapping = true;
        t.overflowMode = TextOverflowModes.Overflow;
        Layout(t.gameObject, flexW: 1, minW: 0);
        return r;
    }

    // 격자 칸 말풍선. 이름, 종류, 대안, 보유와 필요, 요구 퀘스트 줄이 있고 폭과 위치는 코드가 정한다.
    private static void BuildRightTooltip(Transform root)
    {
        var tip = Child(root, "RightTooltip");
        Layout(tip).ignoreLayout = true;
        var rt = (RectTransform)tip.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(1, 1);
        rt.sizeDelta = new Vector2(300, 80);
        Img(tip, TooltipBg).raycastTarget = false;
        VLayout(tip, 10, 10, 7, 7, 2, TextAnchor.UpperLeft, true, false);
        tip.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        foreach (var (name, text, size, color) in new[]
                 {
                     ("Title", "WARTECH TV-115 plate carrier (Olive Drab)", 13f, TextBright),
                     ("Kind", "Quest item · spawns only while the quest is active", 12f, Gold),
                     ("Alt", "Or: WARTECH Berkut BB-102 backpack", 12f, TextDim),
                     ("Count", "Have 3 / Need 50 · FIR · Short 47", 12.5f, ShortRed),
                     ("Quests", "Drip-Out - Part 1", 12f, TextDim),
                 })
        {
            var t = Label(tip.transform, name, text, size, color, TextAlignmentOptions.TopLeft);
            t.enableWordWrapping = true;
            t.overflowMode = TextOverflowModes.Overflow;
        }
        tip.SetActive(false);
    }

    // ── 생성 아이콘 ──
    // 흰 도형 64×64 PNG. 색은 Image.color 로 곱한다. 픽셀마다 4×4 표본으로 덮인 비율을 알파로 써서 가장자리를 부드럽게 한다.
    // 코드가 같으면 바이트도 같아서 다시 돌려도 파일이 바뀌지 않는다.
    private const string MarkOkFile = "mark_ok.png";
    private const string MarkStashFile = "mark_stash.png";
    private const string MarkWarnFile = "mark_warn.png";
    private const string RingFile = "ring.png";
    private const string MarkerRingFile = "marker_ring.png";
    private const string PinFile = "pin.png";

    private static void GenerateIcons()
    {
        Directory.CreateDirectory(IconFolder);
        // ✓ 는 두 선분
        WriteIcon(MarkOkFile, (x, y) => Segment(x, y, 0.16f, 0.52f, 0.40f, 0.26f) < 0.075f || Segment(x, y, 0.40f, 0.26f, 0.86f, 0.76f) < 0.075f);
        // ◐ 는 고리에 왼쪽 반을 채운다
        WriteIcon(MarkStashFile, (x, y) =>
        {
            var d = Mathf.Sqrt((x - 0.5f) * (x - 0.5f) + (y - 0.5f) * (y - 0.5f));
            return (d < 0.44f && d > 0.33f) || (d <= 0.44f && x < 0.5f);
        });
        // ⚠ 는 삼각형에서 느낌표 자리를 뺀다
        WriteIcon(MarkWarnFile, (x, y) =>
        {
            var inside = y > 0.1f && y < 0.92f && Mathf.Abs(x - 0.5f) < (0.92f - y) * 0.56f;
            var bar = Mathf.Abs(x - 0.5f) < 0.055f && y > 0.36f && y < 0.7f;
            var dot = (x - 0.5f) * (x - 0.5f) + (y - 0.24f) * (y - 0.24f) < 0.06f * 0.06f;
            return inside && !bar && !dot;
        });
        // FIR 고리
        WriteIcon(RingFile, (x, y) =>
        {
            var d = Mathf.Sqrt((x - 0.5f) * (x - 0.5f) + (y - 0.5f) * (y - 0.5f));
            return d < 0.46f && d > 0.31f;
        });
        // 목록 지도 번호의 테두리. 16px 로 그리면 1.5px 두께다
        WriteIcon(MarkerRingFile, (x, y) =>
        {
            var d = Mathf.Sqrt((x - 0.5f) * (x - 0.5f) + (y - 0.5f) * (y - 0.5f));
            return d < 0.5f && d > 0.405f;
        });
        // 지도에 있는 목표의 핀. 구멍 뚫린 둥근 머리(가운데 0.5, 0.6 반지름 0.22)에서 아래 끝(0.5, 0.12)으로 좁아진다.
        // 아래 끝에서 머리에 닿는 접선이 높이 0.5 에서 만나고 기울기는 0.516 이다.
        WriteIcon(PinFile, (x, y) =>
        {
            float dx = x - 0.5f, dy = y - 0.6f;
            var head = dx * dx + dy * dy;
            if (head < 0.085f * 0.085f) return false;
            if (head < 0.22f * 0.22f) return true;
            return y >= 0.12f && y <= 0.5f && Mathf.Abs(dx) <= (y - 0.12f) * 0.516f;
        });
        AssetDatabase.Refresh();
    }

    private static float Segment(float px, float py, float ax, float ay, float bx, float by)
    {
        float dx = bx - ax, dy = by - ay;
        var t = Mathf.Clamp01(((px - ax) * dx + (py - ay) * dy) / (dx * dx + dy * dy));
        float cx = ax + t * dx - px, cy = ay + t * dy - py;
        return Mathf.Sqrt(cx * cx + cy * cy);
    }

    private static void WriteIcon(string file, System.Func<float, float, bool> inside)
    {
        const int size = 64, samples = 4;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        for (var py = 0; py < size; py++)
            for (var px = 0; px < size; px++)
            {
                var hits = 0;
                for (var sy = 0; sy < samples; sy++)
                    for (var sx = 0; sx < samples; sx++)
                        if (inside((px + (sx + 0.5f) / samples) / size, (py + (sy + 0.5f) / samples) / size)) hits++;
                tex.SetPixel(px, py, new Color(1, 1, 1, hits / (float)(samples * samples)));
            }
        File.WriteAllBytes(IconFolder + file, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
    }

    // ── 도우미 ──

    private static GameObject Child(Transform parent, string name, params System.Type[] components)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        foreach (var c in components) go.AddComponent(c);
        return go;
    }

    private static void Stretch(GameObject go)
    {
        var rt = (RectTransform)go.transform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    private static Image Img(GameObject go, Color color)
    {
        var img = go.AddComponent<Image>();
        img.color = color;
        return img;
    }

    // ── 자리 표시 글꼴 ──
    // 런타임에 BundleLoader.SwapFonts 가 모든 TMP 글꼴을 게임 글꼴로 바꾸므로 번들의 글꼴은 에디터 미리보기용일 뿐이다.
    // 기본 TMP 글꼴은 큰 아틀라스와 원본 TTF 까지 끌고 와 번들이 커지므로, 영문 몇 글자만 담은 작은 정적 글꼴을 만들어 쓴다.
    private const string PlaceholderFontPath = "Assets/Briefing/Fonts/Placeholder SDF.asset";
    private const string PlaceholderChars = "0123456789 ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz/·.,:%+-()";
    private static TMP_FontAsset _placeholderFont;

    private static TMP_FontAsset PlaceholderFont()
    {
        if (_placeholderFont != null) return _placeholderFont;
        Directory.CreateDirectory("Assets/Briefing/Fonts");
        AssetDatabase.DeleteAsset(PlaceholderFontPath);
        var source = AssetDatabase.LoadAssetAtPath<Font>("Assets/TextMesh Pro/Fonts/LiberationSans.ttf");
        if (source == null) throw new System.Exception("LiberationSans.ttf not found (TextMesh Pro essentials)");
        var font = TMP_FontAsset.CreateFontAsset(source, 16, 2, UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA, 128, 128,
            AtlasPopulationMode.Dynamic, false);
        font.name = "Placeholder SDF";
        font.TryAddCharacters(PlaceholderChars, out _);
        font.atlasPopulationMode = AtlasPopulationMode.Static; // 정적으로 바꾸면 원본 글꼴 참조를 놓는다
        AssetDatabase.CreateAsset(font, PlaceholderFontPath);
        font.atlasTexture.name = "Placeholder SDF Atlas";
        AssetDatabase.AddObjectToAsset(font.atlasTexture, font);
        font.material.name = "Placeholder SDF Material";
        AssetDatabase.AddObjectToAsset(font.material, font);
        AssetDatabase.SaveAssets();
        _placeholderFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(PlaceholderFontPath);
        if (_placeholderFont == null) throw new System.Exception("placeholder font not created: " + PlaceholderFontPath);
        return _placeholderFont;
    }

    private static TextMeshProUGUI Label(Transform parent, string name, string text, float size, Color color, TextAlignmentOptions align)
    {
        var t = Child(parent, name).AddComponent<TextMeshProUGUI>();
        t.font = PlaceholderFont(); // 기본 글꼴과 그 TTF 가 번들에 딸려 오지 않게 한다. 런타임에 게임 글꼴로 바뀐다
        t.text = text;
        t.fontSize = size;
        t.color = color;
        t.alignment = align;
        t.enableWordWrapping = false;
        t.overflowMode = TextOverflowModes.Ellipsis;
        t.raycastTarget = false; // 버튼 바탕이 클릭을 받게
        return t;
    }

    private static LayoutElement Layout(GameObject go, float prefW = -1, float prefH = -1, float flexW = -1, float flexH = -1, float minH = -1, float minW = -1)
    {
        var le = go.GetComponent<LayoutElement>();
        if (le == null) le = go.AddComponent<LayoutElement>();
        if (prefW >= 0) le.preferredWidth = prefW;
        if (prefH >= 0) le.preferredHeight = prefH;
        if (flexW >= 0) le.flexibleWidth = flexW;
        if (flexH >= 0) le.flexibleHeight = flexH;
        if (minH >= 0) le.minHeight = minH;
        if (minW >= 0) le.minWidth = minW;
        return le;
    }

    private static void HLayout(GameObject go, int left, int right, int top, int bottom, float spacing, TextAnchor align, bool expandW, bool expandH)
    {
        var g = go.AddComponent<HorizontalLayoutGroup>();
        g.padding = new RectOffset(left, right, top, bottom);
        g.spacing = spacing;
        g.childAlignment = align;
        g.childControlWidth = true;
        g.childControlHeight = true;
        g.childForceExpandWidth = expandW;
        g.childForceExpandHeight = expandH;
    }

    private static void VLayout(GameObject go, int left, int right, int top, int bottom, float spacing, TextAnchor align, bool expandW, bool expandH)
    {
        var g = go.AddComponent<VerticalLayoutGroup>();
        g.padding = new RectOffset(left, right, top, bottom);
        g.spacing = spacing;
        g.childAlignment = align;
        g.childControlWidth = true;
        g.childControlHeight = true;
        g.childForceExpandWidth = expandW;
        g.childForceExpandHeight = expandH;
    }

    private static Color Hex(string hex, float alpha = 1f)
    {
        ColorUtility.TryParseHtmlString(hex, out var c);
        c.a = alpha;
        return c;
    }
}
