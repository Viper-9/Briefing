using System.Collections.Generic;
using System.Globalization;
using Briefing.Core.Contract;

namespace Briefing.Core.Screen;

// 보스 말풍선 한 줄: "이름 (고레벨, 밤) 39%". QuestCodex 웹 MapMarkers.tsx bossLine 과 같다.
// 확률은 맵 어디서든 나올 값 하나만 쓴다.
// 조건부는 "이름 · 조건부" 로 숫자를 쓰지 않는다. 합친 값은 모든 신호를 눌렀을 때의 확률이라 연구소 레이더가 100% 로 나온다.
// 맵을 순환하는 보스는 "(순환)" 을 붙인다. 숫자는 이 맵에 배정됐을 때의 값이고 지금 이 맵인지는 알 수 없다.
// 광신도는 서버 표에 시간 조건이 없지만 게임이 낮에 막는다. 야간 공장 줄은 이미 "야간" 이라 덧붙이지 않는다.
public static class BossLine
{
    public static string Of(BriefingBoss boss)
    {
        var labels = new List<string>();
        var on = boss.OnlyOn == null ? null : Texts.BossOn(boss.OnlyOn);
        if (on != null) labels.Add(on);
        if (boss.Key == "sectantPriest" && boss.OnlyOn != "factory4_night") labels.Add(Texts.BossNight);
        if (boss.Rotating) labels.Add(Texts.BossRotating);

        var name = Texts.BossName(boss.Key) + (labels.Count > 0 ? " (" + string.Join(", ", labels) + ")" : "");
        return boss.Conditional
            ? name + " · " + Texts.BossConditional
            : name + " " + boss.MapChance.ToString(CultureInfo.InvariantCulture) + "%";
    }
}
