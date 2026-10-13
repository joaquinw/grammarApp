using GrammarApp;

namespace GrammarApp.Tests;

public class HistoryTests
{
    static readonly DateTime T = new(2026, 10, 12);

    [Fact]
    public void Newest_first_trimmed_and_blank_ignored()
    {
        var items = new List<HistoryEntry>();
        History.Push(items, "first", T);
        History.Push(items, "  second \n", T);
        History.Push(items, "   ", T);

        Assert.Equal(new[] { "second", "first" }, items.Select(e => e.Text));
    }

    [Fact]
    public void Sending_the_same_text_again_moves_it_to_the_top()
    {
        var items = new List<HistoryEntry>();
        History.Push(items, "a", T);
        History.Push(items, "b", T);
        History.Push(items, "a", T.AddMinutes(1));

        Assert.Equal(new[] { "a", "b" }, items.Select(e => e.Text));
        Assert.Equal(T.AddMinutes(1), items[0].When);
    }

    [Fact]
    public void Keeps_only_the_latest_Max_entries()
    {
        var items = new List<HistoryEntry>();
        for (int i = 0; i < History.Max + 5; i++) History.Push(items, $"text {i}", T);

        Assert.Equal(History.Max, items.Count);
        Assert.Equal($"text {History.Max + 4}", items[0].Text);
    }
}
