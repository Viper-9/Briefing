using System;
using System.Collections.Generic;

namespace Briefing.Core.Screen;

// 게임에서 읽은 제출 목표 하나. 서버 응답에는 없고 QuestBridge 가 게임 Quest·Condition 에서 옮겨 담는다.
// Target 은 게임이 수치를 숨기는 목표(IsHiddenValue)면 null. Ready 는 게임의 제출 버튼이 켜지는 조건과 같다.
// ItemNames 는 낼 아이템 이름이고 없으면 빈 목록.
public sealed class HandoverGoal
{
    public HandoverGoal(string questId, string questName, string traderId, string conditionId, string text,
        double current, double? target, bool ready, IReadOnlyList<string> itemNames = null)
    {
        QuestId = questId;
        QuestName = questName;
        TraderId = traderId;
        ConditionId = conditionId;
        Text = text;
        Current = current;
        Target = target;
        Ready = ready;
        ItemNames = itemNames ?? Array.Empty<string>();
    }

    public string QuestId { get; }
    public string QuestName { get; }
    public string TraderId { get; }
    public string ConditionId { get; }
    public string Text { get; }
    public double Current { get; }
    public double? Target { get; }
    public bool Ready { get; }
    public IReadOnlyList<string> ItemNames { get; }
}
