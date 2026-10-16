using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Briefing.Core.Screen;

namespace Briefing.Tests;

// 세 언어 모두 빈 문구가 없는지, 게임 글꼴에 없는 기호가 없는지, 러시아어가 영어 그대로 남지 않았는지 본다.
// 고정 문구는 리플렉션으로 전부, 인자를 받는 문구는 대표 인자로 부른다.
[Collection("Texts")]
public class TextsTableTests
{
    private static readonly string[] Languages = { "kr", "en", "ru" };
    // Bender 와 KS X 1001 에 없는 기호
    private static readonly char[] Forbidden = { '…', '✓', '⚠', '▾', '▴', '✕', '—' };
    // 언어와 상관없이 같은 문구(지도 출처 표기)
    private static readonly HashSet<string> SameInAllLanguages = new HashSet<string> { "MapCredit" };

    // 새 함수를 더하면 여기에도 더해야 Every_formatting_text_is_covered 가 통과한다.
    private static IEnumerable<(string Name, string Text)> Formatted()
    {
        yield return ("MinutesAgo", Texts.MinutesAgo(5));
        yield return ("HereTitle", Texts.HereTitle("Customs"));
        yield return ("HandoverCount", Texts.HandoverCount(1));
        yield return ("HandoverCount", Texts.HandoverCount(3));
        yield return ("FindCount", Texts.FindCount(1));
        yield return ("FindCount", Texts.FindCount(3));
        yield return ("MoreItems", Texts.MoreItems(2));
        foreach (var level in new[] { -1, 0, 2 }) yield return ("FloorName", Texts.FloorName(level, null));
        yield return ("FloorName", Texts.FloorName(1, "deck"));
        foreach (var kind in new[] { "pmc", "shared", "scav", "transit" }) yield return ("ExitKind", Texts.ExitKind(kind));
        yield return ("TransitTo", Texts.TransitTo("Customs"));
        foreach (var kind in new[] { "coop", "train", "secret", "alpinist", "switch" }) yield return ("ExitRequirement", Texts.ExitRequirement(kind));
        yield return ("ExitChance", Texts.ExitChance(12.5));
        yield return ("KeypadCode", Texts.KeypadCode("2024"));
        yield return ("ExplosiveDoor", Texts.ExplosiveDoor("C4"));
        yield return ("FrozenHatch", Texts.FrozenHatch("Torch"));
        yield return ("MapCredit", Texts.MapCredit("Shebuka", "CC BY-NC-SA 4.0"));
        yield return ("ShortRows", Texts.ShortRows(2));
        yield return ("FindHere", Texts.FindHere("Customs"));
        yield return ("FilterOnly", Texts.FilterOnly("Quest"));
        yield return ("OrItems", Texts.OrItems("A, B"));
        yield return ("BuildNotAccepted", Texts.BuildNotAccepted("PBS-1"));
        yield return ("HaveNeed", Texts.HaveNeed("1", "2"));
        yield return ("ShortBy", Texts.ShortBy("1"));
        foreach (var label in new[] { "weapon", "caliber", "weaponMod", "equipment", "forbidden" }) yield return ("GearLabel", Texts.GearLabel(label));
        yield return ("WearingNow", Texts.WearingNow("Helmet"));
        yield return ("MoreAlternatives", Texts.MoreAlternatives(4));
        yield return ("ListCount", Texts.ListCount(7));
        foreach (var mark in new[] { GearMark.Equipped, GearMark.Stash, GearMark.Missing }) yield return ("LegendMark", Texts.LegendMark(mark));
        yield return ("ExitVia", Texts.ExitVia("Klimov Street"));
        foreach (var status in new[] { "Survived", "Runner", "Transit", "Killed", "Left", "MissingInAction" }) yield return ("ExitStatus", Texts.ExitStatus(status));
    }

    private static IEnumerable<(string Name, string Text)> All()
    {
        var fixedTexts = typeof(Texts).GetProperties(BindingFlags.Public | BindingFlags.Static)
            .Where(p => p.PropertyType == typeof(string) && p.Name != nameof(Texts.Language))
            .Select(p => (p.Name, (string)p.GetValue(null)));
        return fixedTexts.Concat(Formatted()).ToList();
    }

    private static Dictionary<string, List<(string Name, string Text)>> ByLanguage()
    {
        var result = new Dictionary<string, List<(string Name, string Text)>>();
        try
        {
            foreach (var lang in Languages)
            {
                Texts.UseLanguage(lang);
                result[lang] = All().ToList();
            }
        }
        finally { Texts.UseLanguage("kr"); }
        return result;
    }

    [Fact]
    public void Every_text_is_present_in_every_language_without_missing_glyphs()
    {
        foreach (var (lang, texts) in ByLanguage())
        {
            Assert.True(texts.Count > 100, $"{lang}: {texts.Count} texts");
            foreach (var (name, text) in texts)
            {
                Assert.False(string.IsNullOrWhiteSpace(text), $"{lang} {name} is empty");
                Assert.True(text.IndexOfAny(Forbidden) < 0, $"{lang} {name} has a glyph the game font lacks: {text}");
            }
        }
    }

    [Fact]
    public void Russian_texts_are_translated()
    {
        var all = ByLanguage();
        var en = all["en"];
        var ru = all["ru"];

        Assert.Equal(en.Select(t => t.Name), ru.Select(t => t.Name));
        for (var i = 0; i < ru.Count; i++)
        {
            var name = ru[i].Name;
            if (SameInAllLanguages.Contains(name)) continue;
            Assert.True(ru[i].Text != en[i].Text, $"ru {name} is still English: {ru[i].Text}");
            Assert.True(ru[i].Text.Any(c => c >= 'А' && c <= 'я' || c == 'ё' || c == 'Ё'), $"ru {name} has no Cyrillic: {ru[i].Text}");
        }
    }

    [Fact]
    public void Every_formatting_text_is_covered()
    {
        var methods = typeof(Texts).GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Where(m => m.ReturnType == typeof(string) && !m.IsSpecialName)
            .Select(m => m.Name)
            .ToHashSet();

        Assert.Equal(methods.OrderBy(n => n), Formatted().Select(t => t.Name).Distinct().OrderBy(n => n));
    }

    [Theory]
    [InlineData("kr", "kr", "브리핑")]
    [InlineData("ru", "ru", "Брифинг")]
    [InlineData("en", "en", "Briefing")]
    [InlineData("ge", "en", "Briefing")] // 그 밖의 게임 언어는 영어(요청 언어 규칙과 같다)
    [InlineData(null, "en", "Briefing")]
    public void Language_key_picks_the_table(string lang, string expected, string title)
    {
        try
        {
            Texts.UseLanguage(lang);
            Assert.Equal(expected, Texts.Language);
            Assert.Equal(title, Texts.TabTitle);
        }
        finally { Texts.UseLanguage("kr"); }
    }
}
