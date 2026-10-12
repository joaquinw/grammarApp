using GrammarApp;

namespace GrammarApp.Tests;

public class DiffTests
{
    // Applying every suggestion (right to left, like clicking each chip) must give exactly the model's rewrite.
    static string ApplyAll(string text, List<Issue> issues) =>
        issues.OrderByDescending(i => i.Offset)
              .Aggregate(text, (t, i) => t.Remove(i.Offset, i.Length).Insert(i.Offset, i.Replacements[0]));

    [Theory]
    [InlineData("I has went to the store and buyed three apple.", "I went to the store and bought three apples.")]
    [InlineData("Hello how are you", "Hello, how are you?")]
    [InlineData("teh cat", "The cat")]
    [InlineData("yo no se", "Yo no sé.")]
    [InlineData("same text", "same text")]
    [InlineData("My name is joaquin Iam therthy three year olse", "My name is Joaquin. I am thirty-three years old.")]
    [InlineData("My name is joaquin Iam therthy three year olseMy name is joaquin Iam therthy three year olse",
                "My name is Joaquin. I am thirty-three years old. My name is Joaquin. I am thirty-three years old.")]
    [InlineData("Ayer fuimos a la playa, hacia mucho calor.", "Ayer fuimos a la playa; hacía mucho calor.")]
    [InlineData("word", "")]
    [InlineData("", "new text")]
    public void Applying_all_suggestions_reproduces_the_rewrite(string original, string corrected) =>
        Assert.Equal(corrected, ApplyAll(original, Checker.Diff(original, corrected)));

    [Theory]
    [InlineData("I has went to the store and buyed three apple.", "I went to the store and bought three apples.")]
    [InlineData("Yo tengo un problemas y no se que hacer.", "Yo tengo un problema y no sé qué hacer.")]
    [InlineData("My name is joaquin Iam therthy three year olse", "My name is Joaquin. I am thirty-three years old.")]
    public void Each_suggestion_points_at_its_own_text_in_the_original(string original, string corrected)
    {
        foreach (var issue in Checker.Diff(original, corrected))
            Assert.Equal(issue.Bad, original.Substring(issue.Offset, issue.Length));
    }

    [Fact]
    public void Identical_text_has_no_suggestions() =>
        Assert.Empty(Checker.Diff("This sentence is already correct.", "This sentence is already correct."));

    [Fact]
    public void Separate_errors_become_separate_suggestions()
    {
        var issues = Checker.Diff("I has went to the store and buyed three apple.", "I went to the store and bought three apples.");

        Assert.Collection(issues,
            i => { Assert.Equal("has ", i.Bad); Assert.Equal("", i.Replacements[0]); },
            i => { Assert.Equal("buyed", i.Bad); Assert.Equal("bought", i.Replacements[0]); },
            i => { Assert.Equal("apple.", i.Bad); Assert.Equal("apples.", i.Replacements[0]); });
    }

    [Fact]
    public void Adjacent_changes_merge_into_one_phrase()
    {
        var issue = Assert.Single(Checker.Diff("My name is joaquin Iam therthy three year olse",
                                               "My name is Joaquin. I am thirty-three years old."));

        Assert.Equal("joaquin Iam therthy three year olse", issue.Bad);
        Assert.Equal("Joaquin. I am thirty-three years old.", issue.Replacements[0]);
        Assert.Equal(11, issue.Offset);
    }

    [Fact]
    public void Punctuation_stays_with_its_word() =>
        Assert.Equal("weekend,", Assert.Single(Checker.Diff("last weekend but", "last weekend, but")).Replacements[0]);

    [Fact]
    public void Spacing_only_changes_are_ignored() =>
        Assert.Empty(Checker.Diff("too  many spaces", "too many spaces"));

    [Fact]
    public void Text_added_at_the_end_is_anchored_to_the_last_word()
    {
        var issue = Assert.Single(Checker.Diff("Hello there", "Hello there friend"));

        Assert.Equal("there", issue.Bad);
        Assert.Equal("there friend", issue.Replacements[0]);
    }
}
