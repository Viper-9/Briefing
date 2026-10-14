using Briefing.Core.Screen;

namespace Briefing.Tests;

// 저장된 열 폭을 고르는 규칙과 손잡이를 끌 때의 한계.
public class ColumnWidthsTests
{
    private const float Wide = 1920f; // Columns 폭 예: 지도 칸 최소 400 + 간격 28 을 빼고도 넉넉

    [Fact]
    public void Defaults_are_540_and_470()
    {
        var (left, right) = ColumnWidths.Resolve(540f, 470f, Wide);
        Assert.Equal(540f, left);
        Assert.Equal(470f, right);
    }

    [Theory]
    [InlineData(0f)]
    [InlineData(-5f)]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    public void Zero_negative_nan_or_infinite_means_default_width(float saved)
    {
        var (left, right) = ColumnWidths.Resolve(saved, saved, Wide);
        Assert.Equal(ColumnWidths.DefaultLeft, left);
        Assert.Equal(ColumnWidths.DefaultRight, right);
    }

    [Fact]
    public void Below_minimum_is_raised_to_minimum()
    {
        var (left, right) = ColumnWidths.Resolve(100f, 10f, Wide);
        Assert.Equal(ColumnWidths.MinLeft, left);
        Assert.Equal(ColumnWidths.MinRight, right);
    }

    [Fact]
    public void Too_large_keeps_the_map_minimum_by_shrinking_the_right_column_first()
    {
        // 남는 폭 = 1920 − 28 − 400 = 1492. 왼쪽 2000 + 오른쪽 470 → 오른쪽을 최소 300 까지, 그래도 넘으면 왼쪽.
        var (left, right) = ColumnWidths.Resolve(2000f, 470f, Wide);
        Assert.Equal(300f, right);
        Assert.Equal(1192f, left);
        Assert.Equal(ColumnWidths.MinMap, Wide - 2 * ColumnWidths.Gap - left - right);
    }

    [Fact]
    public void Slightly_too_large_shrinks_only_the_right_column()
    {
        // 남는 폭 1492: 왼쪽 1100 + 오른쪽 470 = 1570 → 78 초과 → 오른쪽만 392 로.
        var (left, right) = ColumnWidths.Resolve(1100f, 470f, Wide);
        Assert.Equal(1100f, left);
        Assert.Equal(392f, right);
    }

    [Fact]
    public void Small_total_width_falls_back_to_both_minimums()
    {
        var (left, right) = ColumnWidths.Resolve(540f, 470f, 1000f);
        Assert.Equal(ColumnWidths.MinLeft, left);
        Assert.Equal(ColumnWidths.MinRight, right);
    }

    [Theory]
    [InlineData(0f)]
    [InlineData(-1f)]
    [InlineData(float.NaN)]
    public void Unknown_total_width_skips_the_map_limit(float total)
    {
        var (left, right) = ColumnWidths.Resolve(2000f, 900f, total);
        Assert.Equal(2000f, left);
        Assert.Equal(900f, right);
    }

    [Fact]
    public void Dragging_the_left_handle_adds_dx_to_the_left_width()
    {
        Assert.Equal(640f, ColumnWidths.DragLeft(540f, 470f, Wide, 100f));
        Assert.Equal(440f, ColumnWidths.DragLeft(540f, 470f, Wide, -100f));
    }

    [Fact]
    public void Dragging_the_left_handle_stops_at_both_limits()
    {
        Assert.Equal(ColumnWidths.MinLeft, ColumnWidths.DragLeft(540f, 470f, Wide, -1000f));
        // 최대 = 1920 − 28 − 470 − 400 = 1022
        Assert.Equal(1022f, ColumnWidths.DragLeft(540f, 470f, Wide, 10000f));
    }

    [Fact]
    public void Dragging_the_right_handle_subtracts_dx_from_the_right_width()
    {
        Assert.Equal(370f, ColumnWidths.DragRight(540f, 470f, Wide, 100f));
        Assert.Equal(570f, ColumnWidths.DragRight(540f, 470f, Wide, -100f));
    }

    [Fact]
    public void Dragging_the_right_handle_stops_at_both_limits()
    {
        Assert.Equal(ColumnWidths.MinRight, ColumnWidths.DragRight(540f, 470f, Wide, 1000f));
        // 최대 = 1920 − 28 − 540 − 400 = 952
        Assert.Equal(952f, ColumnWidths.DragRight(540f, 470f, Wide, -10000f));
    }

    [Fact]
    public void Dragging_with_no_room_returns_the_minimum()
    {
        Assert.Equal(ColumnWidths.MinLeft, ColumnWidths.DragLeft(540f, 470f, 1000f, 50f));
        Assert.Equal(ColumnWidths.MinRight, ColumnWidths.DragRight(540f, 470f, 1000f, -50f));
    }

    [Fact]
    public void Dragging_with_unknown_total_only_has_the_minimum()
    {
        Assert.Equal(5540f, ColumnWidths.DragLeft(540f, 470f, 0f, 5000f));
        Assert.Equal(ColumnWidths.MinLeft, ColumnWidths.DragLeft(540f, 470f, 0f, -5000f));
    }
}
