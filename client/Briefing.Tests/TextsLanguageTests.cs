using Briefing.Core.Screen;

namespace Briefing.Tests;

// Texts 의 언어는 정적 상태라 Texts 를 읽는 다른 테스트와 병렬로 돌지 않게 같은 컬렉션에 둔다.
[CollectionDefinition("Texts", DisableParallelization = true)]
public class TextsCollection { }

[Collection("Texts")]
public class TextsLanguageTests
{
    [Fact]
    public void EnglishModeGivesEnglishStrings()
    {
        try
        {
            Texts.UseLanguage("en");
            Assert.Equal("On Customs", Texts.HereTitle("Customs"));
            Assert.Equal("Hand in", Texts.ReadyTitle);
            Assert.Equal("1 objective to hand over", Texts.HandoverCount(1));
            Assert.Equal("3 objectives to hand over", Texts.HandoverCount(3));
            Assert.Equal("Update QuestCodex to 1.8.0 or later", Texts.QuestCodexOutdated);
            Assert.Equal("Loading briefing...", Texts.Loading);
            Assert.Equal("Ready to turn in", Texts.TurnInTitle);
            Assert.Equal("Report to the trader to complete", Texts.TurnInSummary);
            Assert.Equal("Turn in", Texts.TurnIn);
        }
        finally { Texts.UseLanguage("kr"); }
    }

    [Fact]
    public void KoreanIsRestored()
    {
        try
        {
            Texts.UseLanguage("en");
            Texts.UseLanguage("kr");
            Assert.Equal("Customs에서 진행", Texts.HereTitle("Customs"));
            Assert.Equal("건네줄 목표 3개", Texts.HandoverCount(3));
            Assert.Equal("브리핑을 불러오는 중...", Texts.Loading);
            Assert.Equal("완료 가능", Texts.TurnInTitle);
            Assert.Equal("상인에게 보고하면 완료", Texts.TurnInSummary);
            Assert.Equal("완료하러 가기", Texts.TurnIn);
        }
        finally { Texts.UseLanguage("kr"); }
    }

    [Fact]
    public void ItemNamesRemainderFollowsTheLanguage()
    {
        try
        {
            Texts.UseLanguage("en");
            Assert.Equal("A, B, C +1 more", QuestDetail.ItemsText(new[] { "A", "B", "C", "D" }, "Hand over"));
            Texts.UseLanguage("kr");
            Assert.Equal("A, B, C 외 1개", QuestDetail.ItemsText(new[] { "A", "B", "C", "D" }, "Hand over"));
        }
        finally { Texts.UseLanguage("kr"); }
    }
}
