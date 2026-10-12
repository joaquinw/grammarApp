using GrammarApp;

namespace GrammarApp.Tests;

public class PromptTests
{
    const string NoThink = "<think>\n\n</think>\n\n";

    [Fact]
    public void Prompt_is_chatml_with_examples_in_order_and_ends_ready_for_a_direct_answer()
    {
        var prompt = Checker.BuildPrompt("SYSTEM", new[] { ("u1", "a1"), ("u2", "a2") }, "USER TEXT");

        Assert.StartsWith("<|im_start|>system\nSYSTEM<|im_end|>\n", prompt);
        Assert.Contains($"<|im_start|>user\nu1<|im_end|>\n<|im_start|>assistant\n{NoThink}a1<|im_end|>\n", prompt);
        Assert.True(prompt.IndexOf("u1", StringComparison.Ordinal) < prompt.IndexOf("u2", StringComparison.Ordinal));
        Assert.EndsWith($"<|im_start|>user\nUSER TEXT<|im_end|>\n<|im_start|>assistant\n{NoThink}", prompt);
    }

    [Fact]
    public void Prompt_without_examples_has_only_system_and_user() =>
        Assert.Equal($"<|im_start|>system\nS<|im_end|>\n<|im_start|>user\nU<|im_end|>\n<|im_start|>assistant\n{NoThink}",
                     Checker.BuildPrompt("S", Array.Empty<(string, string)>(), "U"));

    [Theory]
    [InlineData("  Hello there.<|im_end|>", "Hello there.")]
    [InlineData("<think>\nlet me think\n</think>\n\nHello there.", "Hello there.")]
    [InlineData("<think></think>Hola.<|im_end|>\n", "Hola.")]
    [InlineData("Line one\nLine two", "Line one\nLine two")]
    public void Reply_is_cleaned_of_chat_markers_and_thinking(string raw, string expected) =>
        Assert.Equal(expected, Checker.CleanReply(raw));
}
