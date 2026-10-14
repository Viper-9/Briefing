using System;
using System.Collections.Generic;
using System.Linq;
using Briefing.Core.Contract;

namespace Briefing.Core.Screen;

// 레이드 목표 하나의 지금 값. QuestBridge.LiveObjectives 가 quest.ProgressCheckers[c].CurrentValue 와
// quest.IsConditionDone(c) 에서 읽어 온다.
public sealed class LiveObjective
{
    public LiveObjective(string conditionId, double current, bool done)
    {
        ConditionId = conditionId;
        Current = current;
        Done = done;
    }

    public string ConditionId { get; }
    public double Current { get; }
    public bool Done { get; }
}

// 게임에서 읽을 범위. 게임 퀘스트 목록을 QuestIds 로 먼저 걸러서 모든 목표를 돌지 않게 한다.
public sealed class LiveTargets
{
    public ISet<string> QuestIds { get; } = new HashSet<string>(StringComparer.Ordinal);
    public ISet<string> ConditionIds { get; } = new HashSet<string>(StringComparer.Ordinal);
}

// SPT 는 레이드 카운터를 레이드가 끝나야 서버 프로필에 쓰므로, 레이드 중 받은 응답의 current 는 레이드 전 값이다.
// 그래서 게임에서 읽은 값으로 목표의 current 와 Done 을 덮어쓰고 퀘스트 합계를 다시 낸다.
// 레이드 중 부하를 줄이려고 바뀐 목표와 그 위로 이어지는 퀘스트·목록·응답만 새로 만들고 나머지는 원래 객체를 같이 쓴다.
// 바뀐 게 없으면 원래 응답을 그대로 돌려준다.
public static class LiveProgress
{
    // 지금 보이는 퀘스트(고른 맵과 아무 맵에서나)의 목표만. 다른 맵은 읽지 않는다.
    public static LiveTargets Targets(BriefingMap map, BriefingAnywhere anywhere)
    {
        var targets = new LiveTargets();
        var quests = (map?.Quests ?? Enumerable.Empty<BriefingQuest>()).Concat(anywhere?.Quests ?? Enumerable.Empty<BriefingQuest>());
        foreach (var quest in quests)
        {
            if (quest?.QuestId == null) continue;
            var any = false;
            foreach (var o in quest.Objectives)
            {
                if (o?.ConditionId == null) continue;
                targets.ConditionIds.Add(o.ConditionId);
                any = true;
            }
            if (any) targets.QuestIds.Add(quest.QuestId);
        }
        return targets;
    }

    // live 는 conditionId 별 게임 값이고 읽은 것만 들어 있다.
    public static BriefingResponse Apply(BriefingResponse response, IReadOnlyDictionary<string, LiveObjective> live)
    {
        if (response == null || live == null || live.Count == 0) return response;
        List<BriefingMap> maps = null;
        for (var i = 0; i < response.Maps.Count; i++)
        {
            var map = response.Maps[i];
            var quests = map == null ? null : ApplyQuests(map.Quests, live);
            if (quests == null) continue;
            maps ??= new List<BriefingMap>(response.Maps);
            maps[i] = WithQuests(map, quests);
        }
        var anywhere = ApplyQuests(response.Anywhere.Quests, live);
        if (maps == null && anywhere == null) return response;
        return new BriefingResponse
        {
            SchemaVersion = response.SchemaVersion,
            QuestCodexVersion = response.QuestCodexVersion,
            GeneratedAt = response.GeneratedAt,
            Lang = response.Lang,
            Traders = response.Traders,
            Maps = maps ?? response.Maps,
            Anywhere = anywhere == null
                ? response.Anywhere
                : new BriefingAnywhere
                {
                    Quests = anywhere,
                    Bring = response.Anywhere.Bring,
                    Gear = response.Anywhere.Gear,
                    Special = response.Anywhere.Special,
                },
        };
    }

    // 서버와 같은 셈이다. target 이 있는 목표만 min(current, target) 과 target 을 더하고, 하나도 없으면 둘 다 null.
    // 게임이 끝났다고 한 목표는 target 까지 찬 것으로 센다.
    public static (double? Current, double? Target) Totals(IEnumerable<BriefingObjective> objectives)
    {
        double current = 0, target = 0;
        var any = false;
        foreach (var o in objectives ?? Enumerable.Empty<BriefingObjective>())
        {
            if (o?.Target == null) continue;
            any = true;
            var t = o.Target.Value;
            current += o.Done ? t : Math.Min(o.Current, t);
            target += t;
        }
        return any ? (current, target) : ((double?)null, (double?)null);
    }

    // 바뀐 퀘스트가 없으면 null.
    private static List<BriefingQuest> ApplyQuests(List<BriefingQuest> quests, IReadOnlyDictionary<string, LiveObjective> live)
    {
        List<BriefingQuest> result = null;
        for (var i = 0; i < quests.Count; i++)
        {
            var quest = quests[i];
            var applied = ApplyQuest(quest, live);
            if (ReferenceEquals(applied, quest)) continue;
            result ??= new List<BriefingQuest>(quests);
            result[i] = applied;
        }
        return result;
    }

    private static BriefingQuest ApplyQuest(BriefingQuest quest, IReadOnlyDictionary<string, LiveObjective> live)
    {
        if (quest == null) return null;
        List<BriefingObjective> objectives = null;
        for (var i = 0; i < quest.Objectives.Count; i++)
        {
            var o = quest.Objectives[i];
            if (o?.ConditionId == null || !live.TryGetValue(o.ConditionId, out var value) || value == null) continue;
            if (o.Current == value.Current && o.Done == value.Done) continue;
            objectives ??= new List<BriefingObjective>(quest.Objectives);
            objectives[i] = new BriefingObjective
            {
                ConditionId = o.ConditionId,
                Text = o.Text,
                Current = value.Current,
                Target = o.Target,
                InferredMap = o.InferredMap,
                Locations = o.Locations,
                Done = value.Done,
            };
        }
        if (objectives == null) return quest;
        var (current, target) = Totals(objectives);
        return new BriefingQuest
        {
            QuestId = quest.QuestId,
            TraderId = quest.TraderId,
            Name = quest.Name,
            Marker = quest.Marker,
            Summary = quest.Summary,
            Current = current,
            Target = target,
            Objectives = objectives,
        };
    }

    private static BriefingMap WithQuests(BriefingMap map, List<BriefingQuest> quests) => new BriefingMap
    {
        Key = map.Key,
        LocationIds = map.LocationIds,
        Name = map.Name,
        Count = map.Count,
        HasMap = map.HasMap,
        MapFolder = map.MapFolder,
        Quests = quests,
        Exits = map.Exits,
        LockedDoors = map.LockedDoors,
        Bring = map.Bring,
        Find = map.Find,
        Gear = map.Gear,
        Special = map.Special,
    };
}
