namespace Briefing.Core.Screen;

// 목표를 다 채워 상인에게 보고만 하면 되는 퀘스트(게임 상태 AvailableForFinish).
// 서버 응답에는 없고 QuestBridge.TurnIns 가 게임에서 읽어 온다.
public sealed class TurnInQuest
{
    public TurnInQuest(string questId, string questName, string traderId)
    {
        QuestId = questId;
        QuestName = questName;
        TraderId = traderId;
    }

    public string QuestId { get; }
    public string QuestName { get; }
    public string TraderId { get; }
}
