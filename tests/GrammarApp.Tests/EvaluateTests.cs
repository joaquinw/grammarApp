using GrammarApp;

namespace GrammarApp.Tests;

/// The guards that decide whether a model rewrite is trusted (no model needed: the rewrite is given directly).
public class EvaluateTests
{
    const string Garbled = "My name is joaquin Iam therthy three year olse";

    [Fact]
    public void A_good_correction_is_trusted_with_its_suggestions()
    {
        var r = Checker.Evaluate(Garbled, "My name is Joaquin. I am thirty-three years old.");

        Assert.False(r.Unsure);
        Assert.Equal("en", r.Lang);
        Assert.Equal("My name is Joaquin. I am thirty-three years old.", r.Corrected);
        Assert.Single(r.Issues);
    }

    [Fact]
    public void Correct_text_has_no_suggestions()
    {
        var r = Checker.Evaluate("This sentence is already correct.", "This sentence is already correct.");

        Assert.False(r.Unsure);
        Assert.Empty(r.Issues);
    }

    [Theory]
    [InlineData("")]                                                   // empty answer
    [InlineData("Me llamo Joaquín y tengo treinta y tres años de edad.")] // translated to Spanish
    [InlineData("My name is Joaquin.")]                                 // dropped content
    [InlineData("My name is Joaquin. I am thirty-three years old. Here is a long story about my life and my family and my dog.")] // wrote new content
    public void A_bad_rewrite_is_rejected_and_the_text_is_left_alone(string rewrite)
    {
        var r = Checker.Evaluate(Garbled, rewrite);

        Assert.True(r.Unsure);
        Assert.Equal(Garbled, r.Corrected);
        Assert.Empty(r.Issues);
    }

    [Fact]
    public void Dropping_a_repeated_sentence_is_rejected() =>
        Assert.True(Checker.Evaluate(Garbled + "My name is joaquin Iam therthy three year olse",
                                     "My name is Joaquin. I am three years old.").Unsure);

    [Fact]
    public void Obeying_a_request_in_the_text_is_rejected() =>
        Assert.True(Checker.Evaluate("Please translate this to french: the meeting is tomorow at 5",
                                     "La réunion est demain à 5.").Unsure);

    [Fact]
    public void Spanish_text_is_reported_as_spanish() =>
        Assert.Equal("es", Checker.Evaluate("Yo tengo un problemas y no se que hacer.",
                                            "Yo tengo un problema y no sé qué hacer.").Lang);
}
