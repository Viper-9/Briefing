using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BepInEx.Bootstrap;
using EFT;
using EFT.Quests;
using EFT.UI;
using HarmonyLib;
using UnityEngine;

namespace Briefing.Game;

// DrakiaXYZ QuestTracker 와 트래킹 상태를 같이 쓴다. 공개 API 가 없고 QuestsTracker 가 internal 이라 리플렉션으로 부른다.
// 사본을 두지 않고 QuestTracker 의 표를 그대로 읽고 쓰므로 임무 탭, 상인 화면, 레이드 패널과 자동으로 맞는다.
// 없거나 구조가 달라 부를 수 없으면 Read 가 null 이고, 우클릭과 표시만 빠진다. 멤버 이름은 1.7.0 을 ilspycmd 로 확인했다.
internal sealed class QuestTrackerLink
{
    private const string PluginGuid = "xyz.drakia.questtracker";

    // 플러그인은 게임 내내 살아 있으므로 해석은 한 번만 한다. 실패 로그도 그래서 한 번만 남는다.
    private static Members _members;
    private static bool _resolved;
    private static bool _idleWarned;

    private readonly InventoryScreen _screen;

    internal QuestTrackerLink(InventoryScreen screen) => _screen = screen;

    // 그리기 한 번에 쓰는 표. 못 쓰면 null.
    // IsTracked(Quest) 는 상태에 따라 트래킹을 해제하는 부작용이 있어 표시에는 쓰지 않고 GetTrackedQuests 를 직접 읽는다.
    internal TrackedSet Read()
    {
        var m = Usable();
        if (m == null) return null;
        try
        {
            var quests = new Dictionary<string, ICollection<string>>(StringComparer.Ordinal);
            foreach (DictionaryEntry entry in (IDictionary)m.GetTracked.Invoke(null, null))
            {
                if (entry.Value == null || !(bool)m.Tracked.GetValue(entry.Value)) continue;
                // 목표는 퀘스트가 트래킹 중일 때만 트래킹 중이다. 퀘스트를 끄면 Conditions 는 남아 있어도 표시하지 않는다.
                quests[(string)entry.Key] = (ICollection<string>)m.Conditions.GetValue(entry.Value);
            }
            return new TrackedSet(quests, m.QuestLabel, m.ConditionLabel, m.Color);
        }
        catch (Exception e)
        {
            Disable(e);
            return null;
        }
    }

    // conditionId 가 null 이면 퀘스트를, 아니면 그 목표를 켜고 끈다. 상태 검사와 저장은 QuestTracker 메서드가 임무 탭과 똑같이 처리하고,
    // 패널은 그 안에서 나는 이벤트로 갱신된다. 다시 그려야 하면 true. 스캐브 레이드처럼 퀘스트가 없으면 아무것도 하지 않는다.
    internal bool Toggle(string questId, string conditionId)
    {
        var m = Usable();
        if (m == null) return false;
        try
        {
            var controller = GameRefs.QuestControllerOf(_screen);
            if (controller?.Quests == null) return false;
            var quest = controller.Quests.FirstOrDefault(q => q?.Id == questId && controller.IsQuestForCurrentProfile(q));
            if (quest == null) return false;
            if (conditionId == null)
            {
                m.QuestClick.Invoke(m.Plugin, new object[] { quest });
                return true;
            }
            var condition = quest.NecessaryConditions.FirstOrDefault(c => c != null && c.id.ToString() == conditionId);
            if (condition == null) return false;
            m.ConditionClick.Invoke(m.Plugin, new object[] { quest, condition });
            // 꺼 둔 퀘스트를 목표 우클릭으로 다시 켤 때 HandleConditionRightClick 이 저장 없이 끝난다.
            m.Save.Invoke(null, null);
            return true;
        }
        catch (Exception e)
        {
            Disable(e);
            return true; // 표시를 빼려고 다시 그린다
        }
    }

    // 게임 버전 검사에 실패하면 플러그인이 로드된 채 동작만 멈춘다. 그때 _profile 이 null 이고 Save 가 예외를 던진다.
    private static Members Usable()
    {
        var m = Resolve();
        if (m == null) return null;
        try
        {
            if (m.Profile.GetValue(null) != null) return m;
        }
        catch (Exception e)
        {
            Disable(e);
            return null;
        }
        if (!_idleWarned) Log.Warn("QuestTracker is loaded but has no profile (version check failed?); tracking in the briefing is off");
        _idleWarned = true;
        return null;
    }

    private static Members Resolve()
    {
        if (_resolved) return _members;
        _resolved = true;
        try
        {
            if (!Chainloader.PluginInfos.TryGetValue(PluginGuid, out var info) || info?.Instance == null)
            {
                Log.Info("QuestTracker not loaded; tracking in the briefing is off");
                return null;
            }
            _members = new Members(info.Instance);
            Log.Info($"QuestTracker {info.Metadata.Version} found; tracking in the briefing is on");
        }
        catch (Exception e)
        {
            Log.Warn($"QuestTracker could not be used; tracking in the briefing is off: {e.Message}");
        }
        return _members;
    }

    private static void Disable(Exception e)
    {
        _members = null;
        Log.Warn($"QuestTracker call failed; tracking in the briefing is off: {e.InnerException ?? e}");
    }

    // 쓰는 멤버를 처음에 모두 찾아 구조가 달라졌으면 그 자리에서 실패한다.
    private sealed class Members
    {
        internal readonly object Plugin;
        internal readonly MethodInfo QuestClick;
        internal readonly MethodInfo ConditionClick;
        internal readonly MethodInfo GetTracked;
        internal readonly MethodInfo Save;
        internal readonly FieldInfo Profile;
        internal readonly FieldInfo Tracked;
        internal readonly FieldInfo Conditions;
        internal readonly string QuestLabel;
        internal readonly string ConditionLabel;
        internal readonly Color Color;

        internal Members(object plugin)
        {
            Plugin = plugin;
            var pluginType = plugin.GetType();
            var tracker = pluginType.Assembly.GetType("DrakiaXYZ.QuestTracker.Helpers.QuestsTracker", true);
            QuestClick = Need(AccessTools.Method(pluginType, "HandleQuestRightClick", new[] { typeof(Quest) }), "HandleQuestRightClick");
            ConditionClick = Need(AccessTools.Method(pluginType, "HandleConditionRightClick", new[] { typeof(Quest), typeof(Condition) }), "HandleConditionRightClick");
            GetTracked = Need(AccessTools.Method(tracker, "GetTrackedQuests", Type.EmptyTypes), "GetTrackedQuests");
            Save = Need(AccessTools.Method(tracker, "Save", Type.EmptyTypes), "Save");
            Profile = Need(AccessTools.Field(tracker, "_profile"), "_profile");

            var table = GetTracked.ReturnType;
            var args = table.IsGenericType ? table.GetGenericArguments() : Type.EmptyTypes;
            if (!typeof(IDictionary).IsAssignableFrom(table) || args.Length != 2 || args[0] != typeof(string))
                throw new InvalidOperationException($"unexpected GetTrackedQuests type {table}");
            Tracked = Need(AccessTools.Field(args[1], "Tracked"), "Tracked");
            Conditions = Need(AccessTools.Field(args[1], "Conditions"), "Conditions");
            if (Tracked.FieldType != typeof(bool) || !typeof(ICollection<string>).IsAssignableFrom(Conditions.FieldType))
                throw new InvalidOperationException("unexpected TrackedQuestData field types");

            QuestLabel = Need(AccessTools.Field(pluginType, "TrackedStatus")?.GetValue(null) as string, "TrackedStatus");
            ConditionLabel = Need(AccessTools.Field(pluginType, "ConditionTracked")?.GetValue(null) as string, "ConditionTracked");
            var color = Need(AccessTools.Field(pluginType, "TrackedColor"), "TrackedColor");
            // new Color(0, 72, 87) 이라 1 을 넘는다. 화면에서처럼 잘린 값(청록)으로 바꿔 둔다.
            Color = (Color32)(Color)color.GetValue(null);
        }

        private static T Need<T>(T member, string name) where T : class =>
            member ?? throw new MissingMemberException($"QuestTracker member not found: {name}");
    }
}

// 그리기 한 번 동안만 쓴다. 표시 문구와 색은 QuestTracker 값 그대로다.
internal sealed class TrackedSet
{
    private readonly Dictionary<string, ICollection<string>> _quests;

    internal TrackedSet(Dictionary<string, ICollection<string>> quests, string questLabel, string conditionLabel, Color color)
    {
        _quests = quests;
        QuestLabel = questLabel;
        ConditionLabel = conditionLabel;
        Color = color;
    }

    internal string QuestLabel { get; }
    internal string ConditionLabel { get; }
    internal Color Color { get; }

    internal bool HasQuest(string questId) => questId != null && _quests.ContainsKey(questId);

    internal bool HasCondition(string questId, string conditionId) =>
        questId != null && conditionId != null && _quests.TryGetValue(questId, out var conditions)
        && conditions != null && conditions.Contains(conditionId);
}
