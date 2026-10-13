using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace GrammarApp;

public record HistoryEntry(DateTime When, string Text);

/// The texts the user sent with Replace/Copy, newest first, kept in %LOCALAPPDATA%\GrammarApp\history.json.
public static class History
{
    public const int Max = 30;
    static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GrammarApp", "history.json");

    public static List<HistoryEntry> Items { get; } = Load();

    public static void Add(string text)
    {
        Push(Items, text, DateTime.Now);
        Save();
    }

    public static void Clear()
    {
        Items.Clear();
        Save();
    }

    /// Newest first; sending the same text again moves it to the top instead of duplicating it.
    internal static void Push(List<HistoryEntry> items, string text, DateTime when)
    {
        text = text.Trim();
        if (text.Length == 0) return;
        items.RemoveAll(e => e.Text == text);
        items.Insert(0, new HistoryEntry(when, text));
        if (items.Count > Max) items.RemoveRange(Max, items.Count - Max);
    }

    // ponytail: history is a convenience; a corrupt or locked file just means an empty list, never a crash
    static List<HistoryEntry> Load()
    {
        try { return JsonSerializer.Deserialize<List<HistoryEntry>>(File.ReadAllText(FilePath)) ?? new(); }
        catch (Exception) { return new(); }
    }

    static void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(Items));
        }
        catch (Exception) { }
    }
}
