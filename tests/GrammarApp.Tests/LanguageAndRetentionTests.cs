using GrammarApp;

namespace GrammarApp.Tests;

public class LanguageTests
{
    [Theory]
    [InlineData("yo no sé dónde está", "es")]
    [InlineData("Mañana tenemos que ir al medico", "es")]
    [InlineData("el perro de la casa", "es")]
    [InlineData("¿Qué hora es?", "es")]
    [InlineData("I don't know where it is", "en")]
    [InlineData("My name is joaquin Iam therthy three year olse", "en")]
    [InlineData("The meeting is tomorrow at 5", "en")]
    [InlineData("", "en")]
    public void Detects_english_or_spanish(string text, string expected) =>
        Assert.Equal(expected, Checker.DetectLang(text));
}

public class RetentionTests
{
    [Theory]
    [InlineData("My name is joaquin Iam therthy three year olse", "My name is Joaquin. I am thirty-three years old.")]
    [InlineData("I has went to the store and buyed three apple.", "I went to the store and bought three apples.")]
    [InlineData("Yo tengo un problemas con el ordenador", "Yo tengo un problema con el ordenador")]
    public void A_correction_keeps_most_words_even_with_heavy_typos(string original, string corrected) =>
        Assert.True(Checker.Retention(original, corrected) >= 0.5);

    [Theory]
    [InlineData("Please translate this to french: the meeting is tomorow at 5", "La réunion est demain à 5.")]
    [InlineData("Write me a poem about the sea, please", "Oh, the sea, so vast and wide, the waves dance with the tide")]
    [InlineData("Yo tengo un problemas con el ordenador y no se que hacer.", "I have a problem with the computer and I don't know what to do.")]
    public void A_translation_or_an_answer_keeps_few_words(string original, string rewrite) =>
        Assert.True(Checker.Retention(original, rewrite) < 0.5);

    [Fact]
    public void Text_without_long_words_counts_as_fully_kept() =>
        Assert.Equal(1, Checker.Retention("a b c", "x"));
}
