using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using EFT.Quests;
using EFT.UI;
using HarmonyLib;
using UnityEngine.UI;

namespace Briefing.Game;

// "임무 탭에서 보기". 사용자가 임무 탭을 누른 것처럼 열고, 목록이 만들어지면 그 퀘스트 줄(NotesTask)의 Toggle 을 켠다.
// 그러면 게임이 줄을 펼치고 그 줄까지 스크롤한다. 못 찾으면 임무 탭만 열어 두고 로그를 남긴다.
internal static class TasksJump
{
    private const int MaxFrames = 60; // 탭 전환(TryHide 는 비동기)과 TasksScreen.Show 를 기다리는 한도

    // TasksPanel 의 private ViewList<Quest, NotesTask>
    private static readonly FieldInfo ViewListField = AccessTools.Field(typeof(TasksPanel), "_questsViewList");

    private enum Result { NotReady, Opened, SwitchedToDaily, NotFound }

    internal static void Open(InventoryScreen screen, string questId)
    {
        if (screen == null || screen._tabDictionary == null
            || !screen._tabDictionary.TryGetValue(EInventoryTab.Notes, out var notes) || notes == null)
        {
            Log.Warn("open in tasks: tasks tab not found");
            return;
        }
        notes.HandlePointerClick(false); // Tab.OnPointerClick 이 부르는 것과 같다
        screen.StartCoroutine(OpenWhenReady(screen, questId));
    }

    private static IEnumerator OpenWhenReady(InventoryScreen screen, string questId)
    {
        var dailyTried = false;
        for (var frame = 0; frame < MaxFrames; frame++)
        {
            yield return null;
            Result result;
            try
            {
                result = TryOpen(screen, questId, dailyTried);
            }
            catch (Exception e)
            {
                Log.Error($"open in tasks failed: {e}");
                yield break;
            }
            switch (result)
            {
                case Result.Opened:
                    yield break;
                case Result.SwitchedToDaily:
                    dailyTried = true; // 필터를 바꾸면 목록을 다시 만드니 다음 프레임에 다시 찾는다
                    break;
                case Result.NotFound:
                    Log.Warn($"open in tasks: quest {questId} is not in the tasks list (filter or side)");
                    yield break;
            }
        }
        Log.Warn($"open in tasks: tasks list was not ready after {MaxFrames} frames");
    }

    private static Result TryOpen(InventoryScreen screen, string questId, bool dailyTried)
    {
        var tasks = screen._tasksScreen;
        var panel = tasks != null ? tasks._tasksPanel : null;
        // 꺼져 있는 동안의 목록은 지난번 것일 수 있어서 임무 화면이 켜진 뒤에만 본다.
        if (panel == null || !panel.gameObject.activeInHierarchy) return Result.NotReady;
        if (!(ViewListField?.GetValue(panel) is ViewList<Quest, NotesTask> list)) return Result.NotReady;

        foreach (var pair in list.Views)
        {
            if (pair.Key == null || pair.Key.Id != questId || pair.Value == null) continue;
            var toggle = pair.Value.GetComponent<Toggle>();
            if (toggle == null) return Result.NotFound;
            if (toggle.isOn) panel.TaskSelectedHandler(pair.Value.RectTransform); // 이미 펼쳐져 있으면 스크롤만
            else toggle.isOn = true;
            return Result.Opened;
        }
        // TasksScreen.Show 는 늘 일반 퀘스트 필터로 시작하므로 일일 퀘스트면 필터를 바꾼다.
        if (!dailyTried && GameRefs.QuestControllerOf(screen)?.Quests.FirstOrDefault(q => q.Id == questId) is DailyQuest)
        {
            tasks._dailyQuestsToggleSpawner.IsToggled = true;
            return Result.SwitchedToDaily;
        }
        return Result.NotFound;
    }
}
