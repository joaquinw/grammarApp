using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;

namespace GrammarApp;

/// Talks to whatever app has focus: copy its selection out, paste the result back.
static class Native
{
    [DllImport("user32.dll")] public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint mods, uint vk);
    [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll")] static extern short GetAsyncKeyState(int vk);
    [DllImport("user32.dll")] static extern void keybd_event(byte vk, byte scan, uint flags, UIntPtr extra);

    const byte Ctrl = 0x11, Shift = 0x10, Alt = 0x12, Win = 0x5B, A = 0x41, C = 0x43, V = 0x56;

    static void CtrlChord(byte key)
    {
        keybd_event(Ctrl, 0, 0, UIntPtr.Zero);
        keybd_event(key, 0, 0, UIntPtr.Zero);
        keybd_event(key, 0, 2, UIntPtr.Zero);
        keybd_event(Ctrl, 0, 2, UIntPtr.Zero);
    }

    // The hotkey's own Shift is still held when we fire; Ctrl+Shift+C would open DevTools instead of copying.
    static async Task WaitForModifiersUp()
    {
        for (int i = 0; i < 75 && new[] { Ctrl, Shift, Alt, Win }.Any(k => GetAsyncKeyState(k) < 0); i++)
            await Task.Delay(20);
    }

    /// Copies the selection; if nothing is selected, selects the whole field first.
    public static async Task<string> CaptureAsync()
    {
        await WaitForModifiersUp();
        var saved = Snapshot();
        Try(Clipboard.Clear);
        CtrlChord(C);
        var text = await WaitForText();
        if (text.Length == 0)
        {
            CtrlChord(A);
            CtrlChord(C);
            text = await WaitForText();
        }
        Restore(saved);
        return text;
    }

    public static async Task PasteAsync(IntPtr target, string text)
    {
        if (text.Length == 0) return;
        var saved = Snapshot();
        Try(() => Clipboard.SetText(text));
        SetForegroundWindow(target);
        await Task.Delay(80);
        CtrlChord(V);
        await Task.Delay(300); // let the target read the clipboard before we put the user's data back
        Restore(saved);
    }

    static async Task<string> WaitForText()
    {
        for (int i = 0; i < 20; i++)
        {
            await Task.Delay(20);
            string t = "";
            Try(() => t = Clipboard.ContainsText() ? Clipboard.GetText() : "");
            if (t.Length > 0) return t;
        }
        return "";
    }

    // Copy every format out so the user's clipboard (images, files, rich text) survives our round trip.
    static DataObject? Snapshot()
    {
        DataObject? copy = null;
        Try(() =>
        {
            var src = Clipboard.GetDataObject();
            if (src == null) return;
            copy = new DataObject();
            foreach (var f in src.GetFormats(false))
                try { if (src.GetData(f) is { } v) copy.SetData(f, v); } catch { /* format not transferable */ }
        });
        return copy;
    }

    static void Restore(DataObject? saved)
    {
        if (saved == null || saved.GetFormats().Length == 0) Try(Clipboard.Clear);
        else Try(() => Clipboard.SetDataObject(saved, true));
    }

    // Clipboard is a shared lock other apps grab briefly; retry instead of crashing.
    public static void Try(Action a)
    {
        for (int i = 0; i < 5; i++)
        {
            try { a(); return; }
            catch (COMException) { System.Threading.Thread.Sleep(30); }
            catch (ExternalException) { System.Threading.Thread.Sleep(30); }
        }
    }
}
