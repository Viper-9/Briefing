using System;
using System.Collections.Generic;

namespace Briefing.Core.Screen;

// 격자 칸 하나의 자리. 왼쪽 위가 0,0 이고 단위는 칸. Index 는 넣은 순서.
public sealed class GridCell
{
    public GridCell(int index, int x, int y, int w, int h)
    {
        Index = index;
        X = x;
        Y = y;
        W = w;
        H = h;
    }

    public int Index { get; }
    public int X { get; }
    public int Y { get; }
    public int W { get; }
    public int H { get; }
}

// Free 는 쓰인 줄 안의 빈 자리. 화면이 빈 칸 무늬로 채워 게임 격자처럼 보이게 한다.
public sealed class GridLayout
{
    public GridLayout(IReadOnlyList<GridCell> cells, int columns, int rows, IReadOnlyList<(int X, int Y)> free)
    {
        Cells = cells;
        Columns = columns;
        Rows = rows;
        Free = free;
    }

    public IReadOnlyList<GridCell> Cells { get; }
    public int Columns { get; }
    public int Rows { get; }
    public IReadOnlyList<(int X, int Y)> Free { get; }
}

// 준비물·수집 격자. 칸 크기는 게임 아이템 크기를 가로·세로 최대 2 로 자른 것이고 한 칸은 63(게임 격자와 같은 간격).
// 넣는 순서대로 위쪽 줄부터 첫 빈자리에 둔다(CSS grid-auto-flow: row dense 와 같다).
public static class ItemGrid
{
    public const float CellSize = 63f;
    public const int MaxSpan = 2;
    public const int MinColumns = 2;

    // 격자선 1 을 남기고 63 으로 나눈다. 2×2 칸이 늘 들어가도록 최소 2.
    public static int Columns(float width)
    {
        if (float.IsNaN(width) || float.IsInfinity(width)) return MinColumns;
        return Math.Max(MinColumns, (int)Math.Floor((width - 1f) / CellSize));
    }

    public static (int W, int H) Clamp(int w, int h) => (Math.Max(1, Math.Min(MaxSpan, w)), Math.Max(1, Math.Min(MaxSpan, h)));

    public static GridLayout Pack(IReadOnlyList<(int W, int H)> sizes, int columns)
    {
        columns = Math.Max(MinColumns, columns);
        var used = new List<bool[]>();
        var cells = new List<GridCell>();
        for (var i = 0; i < sizes.Count; i++)
        {
            var (w, h) = Clamp(sizes[i].W, sizes[i].H);
            for (var y = 0; ; y++)
            {
                var placed = false;
                for (var x = 0; x + w <= columns; x++)
                {
                    if (!Fits(used, x, y, w, h)) continue;
                    Mark(used, columns, x, y, w, h);
                    cells.Add(new GridCell(i, x, y, w, h));
                    placed = true;
                    break;
                }
                if (placed) break;
            }
        }
        var free = new List<(int, int)>();
        for (var y = 0; y < used.Count; y++)
            for (var x = 0; x < columns; x++)
                if (!used[y][x]) free.Add((x, y));
        return new GridLayout(cells, columns, used.Count, free);
    }

    private static bool Fits(List<bool[]> used, int x, int y, int w, int h)
    {
        for (var dy = 0; dy < h; dy++)
        {
            if (y + dy >= used.Count) continue; // 아직 없는 줄은 비어 있다
            for (var dx = 0; dx < w; dx++)
                if (used[y + dy][x + dx]) return false;
        }
        return true;
    }

    private static void Mark(List<bool[]> used, int columns, int x, int y, int w, int h)
    {
        while (used.Count < y + h) used.Add(new bool[columns]);
        for (var dy = 0; dy < h; dy++)
            for (var dx = 0; dx < w; dx++)
                used[y + dy][x + dx] = true;
    }
}
