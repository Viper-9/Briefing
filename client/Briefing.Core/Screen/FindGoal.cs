namespace Briefing.Core.Screen;

// 게임에서 읽은 찾기(FindItem) 목표 하나. 짝인 건네주기 목표가 찾기를 끝내야 보이는 퀘스트가 있어서,
// 그런 퀘스트도 "제출" 묶음에 보이도록 QuestBridge 가 따로 읽어 온다.
public sealed class FindGoal
{
    public FindGoal(string questId, string questName, string traderId, string conditionId, string text)
    {
        QuestId = questId;
        QuestName = questName;
        TraderId = traderId;
        ConditionId = conditionId;
        Text = text;
    }

    public string QuestId { get; }
    public string QuestName { get; }
    public string TraderId { get; }
    public string ConditionId { get; }
    public string Text { get; }
}
