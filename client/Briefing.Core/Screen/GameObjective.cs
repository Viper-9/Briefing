using System;
using System.Collections.Generic;

namespace Briefing.Core.Screen;

// 펼친 퀘스트에서 어떤 줄로 그릴지 고르는 데만 쓴다.
// Raid 는 서버 objectives 에도 오는 레이드 목표(처치, 물건 두기, 비컨 설치, 장소 방문),
// Handover 는 제출(아이템 건네주기, 무기 조립), Other 는 그 밖(레이드에서 찾기, 스킬, 레벨 등).
public enum GameObjectiveKind
{
    Raid,
    Handover,
    Other,
}

// 게임에서 읽은 퀘스트 목표 하나. QuestBridge.Objectives 가 quest.NecessaryConditions 를 게임 순서대로 옮겨 담는다.
// Target 은 게임이 수치를 숨기는 목표면 null. ItemNames 는 ConditionItem 의 대상 아이템 이름이고 없으면 빈 목록.
public sealed class GameObjective
{
    public GameObjective(string questId, string conditionId, string text, double current, double? target, bool done,
        GameObjectiveKind kind, IReadOnlyList<string> itemNames)
    {
        QuestId = questId;
        ConditionId = conditionId;
        Text = text;
        Current = current;
        Target = target;
        Done = done;
        Kind = kind;
        ItemNames = itemNames ?? Array.Empty<string>();
    }

    public string QuestId { get; }
    public string ConditionId { get; }
    public string Text { get; }
    public double Current { get; }
    public double? Target { get; }
    public bool Done { get; }
    public GameObjectiveKind Kind { get; }
    public IReadOnlyList<string> ItemNames { get; }
}
