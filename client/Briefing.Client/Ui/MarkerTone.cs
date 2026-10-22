using UnityEngine;

namespace Briefing.Ui;

// 퀘스트 마커(목록 번호, 펼침 핀, 지도 마커와 구역)는 퀘스트마다 색을 나누지 않고 한 가지 톤을 쓴다.
// 평소엔 밝은 테두리와 번호, 강조(마우스 올림, 펼친 줄)되면 게임의 선택 줄 색으로 채우고 테두리와 번호를 어둡게 한다.
internal static class MarkerTone
{
    internal static readonly Color Ink = UiKit.Hex("#c9cbc6"); // 목록 본문 글자색
    internal static readonly Color Dark = UiKit.Hex("#141616");

    // 게임 색은 QuestListView.Render 가 읽어 둔다. 지도 위에서 비치지 않게 불투명으로 쓴다.
    internal static Color LitFill
    {
        get
        {
            var c = Game.QuestListColors.SelectedBg;
            c.a = 1f;
            return c;
        }
    }

    internal static Color LitText => Game.QuestListColors.SelectedText;
}
