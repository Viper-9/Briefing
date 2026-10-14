namespace Briefing.Core.Screen;

// QuestCodex 웹 objectiveColor(n)(web/src/styles.css 의 --obj-1..10)와 같은 10색. 10 을 넘으면 처음으로 돌아간다.
// 목록 번호와 지도 마커·구역이 같은 색을 쓴다.
public static class MarkerPalette
{
    private static readonly string[] Colors =
    {
        "#ff8c42", "#2ec4b6", "#a78bfa", "#facc15", "#60a5fa", "#f472b6", "#4ade80", "#d4a373", "#e879f9", "#94a3b8",
    };

    public static string Hex(int marker) => marker < 1 ? null : Colors[(marker - 1) % Colors.Length];
}
