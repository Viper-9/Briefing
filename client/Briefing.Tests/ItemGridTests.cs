using System.Linq;
using Briefing.Core.Screen;

namespace Briefing.Tests;

// 격자 배치: 칸은 최대 2×2, 위쪽 줄부터 첫 빈자리, 한 줄 칸 수는 폭 ÷ 63(최소 2).
public class ItemGridTests
{
    private static string Places(GridLayout layout) =>
        string.Join(" ", layout.Cells.Select(c => $"{c.Index}@{c.X},{c.Y}:{c.W}x{c.H}"));

    [Fact]
    public void Columns_follow_the_width()
    {
        Assert.Equal(7, ItemGrid.Columns(446)); // 기본 폭 470 - 좌우 여백 24
        Assert.Equal(4, ItemGrid.Columns(276)); // 최소 폭 300
        Assert.Equal(2, ItemGrid.Columns(127)); // (127-1)/63 = 2
        Assert.Equal(2, ItemGrid.Columns(100)); // 최소 2
        Assert.Equal(2, ItemGrid.Columns(0));
        Assert.Equal(2, ItemGrid.Columns(float.NaN));
    }

    [Fact]
    public void Sizes_are_clamped_to_two_by_two()
    {
        Assert.Equal((2, 2), ItemGrid.Clamp(5, 2)); // 무기 프리셋 5×2
        Assert.Equal((1, 2), ItemGrid.Clamp(1, 3));
        Assert.Equal((1, 1), ItemGrid.Clamp(0, -1));
    }

    [Fact]
    public void Packs_in_order_into_the_first_free_place()
    {
        // 시안의 준비물: AK-50 2×2, 총열덮개 2×1, MS2000 1×1, WI-FI 1×1, 총열 2×1 — 4칸 폭
        var layout = ItemGrid.Pack(new[] { (2, 2), (2, 1), (1, 1), (1, 1), (2, 1) }, 4);

        Assert.Equal("0@0,0:2x2 1@2,0:2x1 2@2,1:1x1 3@3,1:1x1 4@0,2:2x1", Places(layout));
        Assert.Equal(4, layout.Columns);
        Assert.Equal(3, layout.Rows);
        Assert.Equal(new[] { (2, 2), (3, 2) }, layout.Free);
    }

    [Fact]
    public void Dense_packing_backfills_earlier_holes()
    {
        // 1×1 다음 2×2 는 첫 줄에 안 들어가 아래로 — 그 뒤 1×1 은 첫 줄 빈자리로 돌아간다(dense).
        var layout = ItemGrid.Pack(new[] { (1, 1), (2, 2), (1, 1) }, 2);

        Assert.Equal("0@0,0:1x1 1@0,1:2x2 2@1,0:1x1", Places(layout));
        Assert.Equal(3, layout.Rows);
        Assert.Empty(layout.Free);
    }

    [Fact]
    public void Column_count_changes_the_layout_only()
    {
        var sizes = new[] { (2, 1), (2, 1), (1, 1) };

        Assert.Equal("0@0,0:2x1 1@2,0:2x1 2@4,0:1x1", Places(ItemGrid.Pack(sizes, 7)));
        Assert.Equal("0@0,0:2x1 1@0,1:2x1 2@0,2:1x1", Places(ItemGrid.Pack(sizes, 2)));
        Assert.Equal(new[] { (1, 2) }, ItemGrid.Pack(sizes, 2).Free);
    }

    [Fact]
    public void Oversized_and_tiny_column_counts_are_safe()
    {
        var layout = ItemGrid.Pack(new[] { (5, 2), (3, 3) }, 0); // 열 수 0 → 최소 2, 크기는 2×2 로

        Assert.Equal("0@0,0:2x2 1@0,2:2x2", Places(layout));
        Assert.Equal(2, layout.Columns);
        Assert.Equal(4, layout.Rows);
        Assert.Empty(ItemGrid.Pack(new (int, int)[0], 4).Cells);
        Assert.Equal(0, ItemGrid.Pack(new (int, int)[0], 4).Rows);
    }
}
