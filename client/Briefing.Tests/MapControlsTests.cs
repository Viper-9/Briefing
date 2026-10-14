using Briefing.Core.Screen;

namespace Briefing.Tests;

// 지도 칸 오른쪽 위 체크박스 줄을 세로로 쌓을지(웹에서는 flex-wrap 과 미디어 쿼리가 하던 일).
public class MapControlsTests
{
    private const float M = 14f;

    [Fact]
    public void Wide_column_keeps_one_row()
    {
        Assert.False(MapControls.Stack(1000f, 120f, 450f, M));
    }

    [Fact]
    public void Exactly_fitting_row_is_not_stacked()
    {
        // 왼쪽 여백 + 층 버튼 + 사이 여백 + 한 줄 + 오른쪽 여백
        Assert.False(MapControls.Stack(M + 120f + M + 450f + M, 120f, 450f, M));
    }

    [Fact]
    public void One_unit_too_narrow_stacks()
    {
        Assert.True(MapControls.Stack(M + 120f + M + 450f + M - 1f, 120f, 450f, M));
    }

    [Fact]
    public void Without_floor_buttons_only_the_margins_count()
    {
        Assert.False(MapControls.Stack(M + 450f + M, 0f, 450f, M));
        Assert.True(MapControls.Stack(M + 450f + M - 1f, 0f, 450f, M));
    }

    [Theory]
    [InlineData(0f)]
    [InlineData(-1f)]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    public void Unknown_column_width_keeps_current_layout(float width)
    {
        Assert.False(MapControls.Stack(width, 120f, 450f, M));
    }

    [Theory]
    [InlineData(true, true, true, CheckState.All)]
    [InlineData(true, false, false, CheckState.Some)]
    [InlineData(false, true, true, CheckState.Some)]
    [InlineData(false, false, true, CheckState.Some)]
    [InlineData(false, false, false, CheckState.None)]
    public void Exits_button_shows_all_some_or_none(bool pmc, bool scav, bool transit, CheckState expected)
    {
        Assert.Equal(expected, MapControls.ExitsCheck(pmc, scav, transit));
    }

    [Fact]
    public void Bad_widths_are_treated_as_zero()
    {
        Assert.False(MapControls.Stack(1000f, float.NaN, float.NaN, M));
    }
}
