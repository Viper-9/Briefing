using System;
using System.Collections.Generic;
using System.Linq;
using Briefing.Core.Contract;

namespace Briefing.Core.Screen;

// 펼친 처치·명중 목표 아래 작은 줄과 마우스를 올리면 뜨는 말풍선 글.
// TipTitle·TipBody 는 이름을 줄여서 다 못 보여 줄 때만 있다.
public sealed class EnemySummary
{
    public EnemySummary(string line, string tipTitle, string tipBody)
    {
        Line = line;
        TipTitle = tipTitle;
        TipBody = tipBody;
    }

    public string Line { get; }
    public string TipTitle { get; }
    public string TipBody { get; }
}

// 게임 설명문은 "보스 처치"처럼 뭉뚱그려서 어느 봇이 인정되는지, 경호원이 들어가는지 알 수 없다. 판정 값으로 따로 적는다.
// 처치·명중을 따로 묶어 이름을 3개까지 잇고 나머지는 개수로 붙인다. 넘친 묶음만 말풍선에 전체 이름을 둔다.
// 역할이 없는 Savage 는 보스까지 전부 인정되므로 진영 문구를 쓴다. 역할이 없는 PMC·Any 는 설명문이 정확해서 적지 않는다.
// 이름은 서버가 정한 것만 쓴다. 서버가 숨긴 역할(스톰트루퍼 등)만 있는 조건은 Names 가 비어 건너뛴다.
// 아이템 이름 줄처럼 목표 글에 이름이 전부 들어 있으면("킬라 제거" + 킬라) 같은 말이라 그 묶음은 적지 않는다.
public static class EnemyLine
{
    private const int MaxNames = 3;
    private const string Savage = "Savage";

    public static EnemySummary Build(IReadOnlyList<BriefingEnemy> enemies, string text = null)
    {
        if (enemies == null || enemies.Count == 0) return null;
        var parts = new List<(string Label, string Short, string Full)>();
        foreach (var group in enemies.Where(e => e != null).GroupBy(e => e.Kind == "shots"))
        {
            var label = group.Key ? Texts.EnemyShot : Texts.EnemyKill;
            if (group.Any(e => (e.Roles == null || e.Roles.Count == 0) && string.Equals(e.Target, Savage, StringComparison.OrdinalIgnoreCase)))
            {
                parts.Add((label, Texts.AnyScavSide, null));
                continue;
            }
            var names = group.Where(e => e.Roles != null && e.Roles.Count > 0)
                .SelectMany(e => e.Names ?? new List<string>())
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .Distinct()
                .ToList();
            if (names.Count == 0) continue;
            if (text != null && names.All(n => text.Contains(n))) continue;
            var shown = string.Join(", ", names.Take(MaxNames));
            parts.Add(names.Count > MaxNames
                ? (label, shown + Texts.MoreItems(names.Count - MaxNames), string.Join(", ", names))
                : (label, shown, null));
        }
        if (parts.Count == 0) return null;

        var line = string.Join(" · ", parts.Select(p => $"{p.Label}: {p.Short}"));
        var full = parts.Where(p => p.Full != null).ToList();
        if (full.Count == 0) return new EnemySummary(line, null, null);
        if (parts.Count == 1) return new EnemySummary(line, full[0].Label, full[0].Full);
        return new EnemySummary(line, Texts.EnemyBoth, string.Join("\n", full.Select(p => $"{p.Label}: {p.Full}")));
    }
}
