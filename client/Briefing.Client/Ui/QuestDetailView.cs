using System;
using System.Globalization;
using Briefing.Core.Screen;
using Briefing.Game;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Briefing.Ui;

// 펼친 줄 바로 아래에 DetailTemplate 을 복제해 목표 줄과 "임무 탭에서 보기" 버튼을 채운다.
// 건네주기 버튼이 있는 줄은 HandoverTemplate, 나머지는 ObjectiveTemplate 을 복제한다. 블록은 QuestListView 가 다른 줄과 함께 지운다.
// "완료 가능" 묶음에서는 "완료하러 가기" 버튼도 켠다.
internal sealed class QuestDetailView
{
    // 게임 버튼 복제본의 크기. 프리팹 버튼에 맞추고 높이만 30 으로 낮춘다.
    private const float GiveWidth = 104, OpenTasksWidth = 150, TurnInWidth = 150, ButtonHeight = 30;
    private const int FontSize = 16;

    private readonly GameObject _template;
    private readonly Action<string> _onOpenTasks;
    private readonly Action<string> _onTurnIn;
    private readonly Action<DetailLine> _onHandover;
    private readonly Func<bool> _handoverBusy;

    internal QuestDetailView(Transform screenRoot, Action<string> onOpenTasks, Action<string> onTurnIn, Action<DetailLine> onHandover,
        Func<bool> handoverBusy)
    {
        _onOpenTasks = onOpenTasks;
        _onTurnIn = onTurnIn;
        _onHandover = onHandover;
        _handoverBusy = handoverBusy;
        _template = UiKit.Child(screenRoot, UiPaths.DetailTemplate).gameObject;
        var d = _template.transform;
        var objective = UiKit.Child(d, UiPaths.ObjectiveTemplate);
        UiKit.Get<Image>(objective, UiPaths.ObjectiveMarker);
        UiKit.Get<Image>(objective, UiPaths.ObjectiveCheck);
        foreach (var p in new[] { UiPaths.ObjectiveMarkerNumber, UiPaths.ObjectiveText, UiPaths.ObjectiveItems, UiPaths.ObjectiveValue })
            UiKit.Get<TMP_Text>(objective, p);
        var handover = UiKit.Child(d, UiPaths.HandoverTemplate);
        UiKit.Get<Image>(handover, UiPaths.HandoverCheck);
        foreach (var p in new[] { UiPaths.HandoverText, UiPaths.HandoverItems, UiPaths.HandoverValue, UiPaths.HandoverGiveLabel })
            UiKit.Get<TMP_Text>(handover, p);
        UiKit.Get<Button>(handover, UiPaths.HandoverGive);
        UiKit.Get<Button>(d, UiPaths.OpenTasks);
        UiKit.Get<TMP_Text>(d, UiPaths.OpenTasksLabel);
        UiKit.Get<Button>(d, UiPaths.TurnIn).gameObject.SetActive(false);
        UiKit.Get<TMP_Text>(d, UiPaths.TurnInLabel);
        UiKit.Get<TMP_Text>(d, UiPaths.GoalKappaLabel);
        UiKit.Get<TMP_Text>(d, UiPaths.GoalLightkeeperLabel);
        UiKit.Child(d, UiPaths.DetailGoals).gameObject.SetActive(false);
        objective.gameObject.SetActive(false);
        handover.gameObject.SetActive(false);
        _template.SetActive(false);
    }

    internal GameObject Create(Transform parent, QuestDetailModel model)
    {
        var go = UnityEngine.Object.Instantiate(_template, parent, false);
        go.name = "Detail_" + model.QuestId;
        try
        {
            var d = go.transform;
            // 게임 버튼은 켜진 계층에서 복제해야 Awake 가 처리기를 연결하므로 블록을 먼저 켠다. 같은 프레임이라 깜빡이지 않는다.
            go.SetActive(true);

            // 줄 배지의 뜻. 해당하는 것만 켠다.
            UiKit.Guard("goal legend", () =>
            {
                UiKit.Get<TMP_Text>(d, UiPaths.GoalKappaLabel).text = Texts.KappaGoal;
                UiKit.Get<TMP_Text>(d, UiPaths.GoalLightkeeperLabel).text = Texts.LightkeeperGoal;
                UiKit.Child(d, UiPaths.GoalKappa).gameObject.SetActive(model.Kappa);
                UiKit.Child(d, UiPaths.GoalLightkeeper).gameObject.SetActive(model.Lightkeeper);
                UiKit.Child(d, UiPaths.DetailGoals).gameObject.SetActive(model.Kappa || model.Lightkeeper);
            });

            // 한 줄이 실패해도 나머지 줄과 버튼은 그린다.
            var objectiveTemplate = UiKit.Child(d, UiPaths.ObjectiveTemplate).gameObject;
            var handoverTemplate = UiKit.Child(d, UiPaths.HandoverTemplate).gameObject;
            var busy = _handoverBusy();
            foreach (var line in model.Lines)
            {
                if (line.ShowButton) UiKit.Guard("handover line", () => AddHandover(handoverTemplate, line, busy));
                else UiKit.Guard("objective line", () => AddObjective(objectiveTemplate, line));
            }
            UiKit.Child(d, UiPaths.DetailObjectives).gameObject.SetActive(model.Lines.Count > 0);

            var questId = model.QuestId;
            // "완료하러 가기"는 템플릿에서 꺼져 있어 "완료 가능" 묶음일 때만 켠다.
            if (model.CanTurnIn)
                UiKit.Guard("turn-in button", () =>
                {
                    var own = UiKit.Get<Button>(d, UiPaths.TurnIn);
                    own.gameObject.SetActive(true); // 켜 둔 자리를 Clone 이 넘겨받고 원본을 끈다
                    var button = GameButtons.Clone(own.transform.parent, own.gameObject, TurnInWidth, ButtonHeight, FontSize)
                        ?? DetailButton.Own(own, UiKit.Get<TMP_Text>(d, UiPaths.TurnInLabel));
                    button.SetText(Texts.TurnIn);
                    button.OnClick(() => UiKit.Guard("turn in", () => _onTurnIn(questId)));
                });
            UiKit.Guard("open-tasks button", () =>
            {
                var own = UiKit.Get<Button>(d, UiPaths.OpenTasks);
                var button = GameButtons.Clone(own.transform.parent, own.gameObject, OpenTasksWidth, ButtonHeight, FontSize)
                    ?? DetailButton.Own(own, UiKit.Get<TMP_Text>(d, UiPaths.OpenTasksLabel));
                button.SetText(Texts.OpenInTasks);
                button.OnClick(() => UiKit.Guard("open in tasks", () => _onOpenTasks(questId)));
            });
            return go;
        }
        catch
        {
            // 아직 _rows 에 들어가기 전이라 Clear 가 못 지우니 여기서 지운다.
            UnityEngine.Object.Destroy(go);
            throw;
        }
    }

    // 끝난 줄은 글과 수치를 아이템 이름 줄과 같은 흐린 색으로 하고 마커 자리에 체크를 켠다.
    private static void AddObjective(GameObject template, DetailLine line)
    {
        var go = UnityEngine.Object.Instantiate(template, template.transform.parent, false);
        go.name = "Objective_" + line.ConditionId;
        var t = go.transform;
        var done = line.Kind == DetailLineKind.Done;
        var text = UiKit.Get<TMP_Text>(t, UiPaths.ObjectiveText);
        text.text = line.Text ?? "";
        var items = UiKit.Get<TMP_Text>(t, UiPaths.ObjectiveItems);
        SetItems(items, line.Items);

        var value = UiKit.Get<TMP_Text>(t, UiPaths.ObjectiveValue);
        value.gameObject.SetActive(line.Progress != null);
        value.text = line.Progress ?? "";
        if (done)
        {
            text.color = items.color;
            value.color = items.color;
        }

        // 원이 없는 줄도 자리는 남겨 글 시작 위치를 맞춘다.
        var marker = UiKit.Get<Image>(t, UiPaths.ObjectiveMarker);
        var number = UiKit.Get<TMP_Text>(t, UiPaths.ObjectiveMarkerNumber);
        var show = !done && line.Marker.HasValue && line.MarkerColor != null;
        marker.gameObject.SetActive(true);
        marker.color = show ? UiKit.Hex(line.MarkerColor) : Color.clear;
        number.text = show ? line.Marker.Value.ToString(CultureInfo.InvariantCulture) : "";
        UiKit.Get<Image>(t, UiPaths.ObjectiveCheck).gameObject.SetActive(done);

        go.SetActive(true);
    }

    // 값이 없으면 꺼서 자리를 접는다.
    private static void SetItems(TMP_Text items, string text)
    {
        items.gameObject.SetActive(!string.IsNullOrEmpty(text));
        items.text = text ?? "";
    }

    // 지금 낼 수 있고 다른 제출이 진행 중이 아닐 때만 버튼을 누를 수 있다.
    private void AddHandover(GameObject template, DetailLine line, bool busy)
    {
        var go = UnityEngine.Object.Instantiate(template, template.transform.parent, false);
        go.name = "Handover_" + line.ConditionId;
        var t = go.transform;
        UiKit.Get<TMP_Text>(t, UiPaths.HandoverText).text = line.Text ?? "";
        SetItems(UiKit.Get<TMP_Text>(t, UiPaths.HandoverItems), line.Items);
        UiKit.Get<Image>(t, UiPaths.HandoverCheck).gameObject.SetActive(false);
        var value = UiKit.Get<TMP_Text>(t, UiPaths.HandoverValue);
        value.gameObject.SetActive(line.Progress != null);
        value.text = line.Progress ?? "";

        go.SetActive(true);

        // 게임 버튼은 게임이 글자를 채우니 우리 버튼일 때만 글자를 넣는다.
        var own = UiKit.Get<Button>(t, UiPaths.HandoverGive);
        var give = GameButtons.Clone(own.transform.parent, own.gameObject, GiveWidth, ButtonHeight, FontSize);
        if (give == null)
        {
            give = DetailButton.Own(own, UiKit.Get<TMP_Text>(t, UiPaths.HandoverGiveLabel));
            give.SetText(Texts.Handover);
        }
        give.Interactable = line.Ready && !busy;
        give.OnClick(() => UiKit.Guard("handover", () => _onHandover(line)));
    }
}
