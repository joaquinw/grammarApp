using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;
using Forms = System.Windows.Forms;

namespace GrammarApp;

public partial class MainWindow : Window
{
    const int HotkeyId = 1;
    IntPtr _target;                 // app we copied from and will paste back into (Zero = opened from tray)
    string _lang = "en";
    string _corrected = "";         // model's full rewrite; "Fix all" applies it
    CancellationTokenSource? _cts;
    bool _pinned;                   // pinned: stays open over other apps, wherever the user dragged it
    readonly DispatcherTimer _debounce = new() { Interval = TimeSpan.FromMilliseconds(1000) }; // each check costs seconds of CPU

    public bool HotkeyOk { get; }

    bool IsSpanish => _lang.StartsWith("es");

    public MainWindow()
    {
        InitializeComponent();
        ApplyTheme();

        _debounce.Tick += async (_, _) => { _debounce.Stop(); await CheckAsync(); };
        Input.TextChanged += (_, _) => { IssueList.IsEnabled = false; TranslationRow.Opacity = 0.5; _debounce.Stop(); _debounce.Start(); };
        SizeChanged += (_, _) => { if (IsVisible) KeepOnScreen(); };
        Deactivated += (_, _) => { if (!_pinned) Hide(); };
        Header.MouseLeftButtonDown += (_, _) => DragMove();
        PreviewKeyDown += OnKey;

        var hwnd = new WindowInteropHelper(this).EnsureHandle();
        HwndSource.FromHwnd(hwnd).AddHook(WndProc);
        HotkeyOk = Native.RegisterHotKey(hwnd, HotkeyId, 0x2 | 0x4 | 0x4000, 0x20); // Ctrl+Shift+Space, no auto-repeat
    }

    IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == 0x0312 && wParam == (IntPtr)HotkeyId) // WM_HOTKEY
        {
            handled = true;
            _ = SummonAsync(capture: true);
        }
        return IntPtr.Zero;
    }

    public async Task SummonAsync(bool capture)
    {
        if (IsVisible && !_pinned) { Hide(); return; }
        if (IsVisible && !capture) { Activate(); return; } // tray click on a pinned window: don't wipe its text
        _target = capture ? Native.GetForegroundWindow() : IntPtr.Zero;
        var text = capture ? await Native.CaptureAsync() : "";

        Input.Text = text;
        HistoryPanel.Visibility = Visibility.Collapsed;
        ReplaceBtn.Content = _target == IntPtr.Zero ? "Copy" : "Replace";
        if (_pinned) { Show(); Activate(); } // WPF keeps Left/Top while hidden, so it reopens where the user left it
        else ShowNearCursor();
        Input.Focus();
        Input.CaretIndex = text.Length;
        await CheckNowAsync();
    }

    void ShowNearCursor()
    {
        var p = Forms.Cursor.Position;
        var area = Forms.Screen.FromPoint(p).WorkingArea;
        Show();
        Activate();
        UpdateLayout();
        var dpi = VisualTreeHelper.GetDpi(this);
        Left = Math.Max(area.Left / dpi.DpiScaleX, Math.Min(p.X / dpi.DpiScaleX - 40, area.Right / dpi.DpiScaleX - ActualWidth));
        Top = p.Y / dpi.DpiScaleY + 12;
        KeepOnScreen();
    }

    // The window grows as results arrive; push it up so its bottom stays on whichever screen it's on now.
    void KeepOnScreen()
    {
        var area = Forms.Screen.FromHandle(new WindowInteropHelper(this).Handle).WorkingArea;
        var dpi = VisualTreeHelper.GetDpi(this);
        Top = Math.Max(area.Top / dpi.DpiScaleY, Math.Min(Top, area.Bottom / dpi.DpiScaleY - ActualHeight));
    }

    void Pin_Click(object sender, RoutedEventArgs e)
    {
        _pinned = !_pinned;
        PinBtn.Content = _pinned ? "\uE840" : "\uE718"; // Pinned / Pin glyphs
        PinBtn.Foreground = (Brush)FindResource(_pinned ? "Accent" : "Sub");
        PinBtn.ToolTip = _pinned ? "Unpin (close when clicking elsewhere)" : "Keep open while you work in other apps";
    }

    Task CheckNowAsync()
    {
        _debounce.Stop();
        return CheckAsync();
    }

    async Task CheckAsync()
    {
        _cts?.Cancel();
        _cts = new CancellationTokenSource();
        var ct = _cts.Token;
        var text = Input.Text;

        if (string.IsNullOrWhiteSpace(text))
        {
            Render(new List<Issue>());
            TranslationRow.Visibility = Visibility.Collapsed;
            Status.Text = HotkeyOk ? "Type here, or select text anywhere + Ctrl+Shift+Space" : "Type or paste text here";
            return;
        }

        if (text.Length > Checker.MaxChars)
        {
            Status.Text = $"Text too long — select up to {Checker.MaxChars:N0} characters";
            return;
        }

        Status.Text = "Checking…";
        try
        {
            var result = await Checker.CheckAsync(text, ct);
            if (ct.IsCancellationRequested || text != Input.Text) return;
            _lang = result.Lang;
            Hint.Text = $"Ctrl+Enter replace · Esc close · {Checker.Device}";
            _corrected = result.Corrected;
            Render(result.Issues);
            bool changed = _corrected.Trim() != text.Trim();
            FixAllBtn.IsEnabled = changed;
            int n = Math.Max(result.Issues.Count, changed ? 1 : 0);
            var name = IsSpanish ? "Español" : "English";
            Status.Text = result.Unsure ? $"{name} · No confident suggestions"
                : n == 0 ? $"{name} · Looks good ✓"
                : $"{name} · {n} {(IsSpanish ? (n == 1 ? "sugerencia" : "sugerencias") : (n == 1 ? "suggestion" : "suggestions"))}";
            await TranslateAsync(_corrected, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
        catch (FileNotFoundException)
        {
            Status.Text = "model.gguf is missing — keep it next to GrammarApp.exe";
        }
        catch (Exception ex)
        {
            Status.Text = "Checker error: " + ex.Message;
        }
    }

    void Render(List<Issue> issues)
    {
        IssueList.Children.Clear();
        IssueList.IsEnabled = true;

        foreach (var issue in issues)
        {
            var chips = new WrapPanel();
            chips.Children.Add(new TextBlock
            {
                Text = issue.Bad.Trim(),
                TextDecorations = TextDecorations.Strikethrough,
                Foreground = (Brush)FindResource("Bad"),
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 3, 8, 4),
            });
            foreach (var rep in issue.Replacements)
            {
                var b = new Button { Content = rep.Trim().Length > 0 ? rep.Trim() : "(remove)", Style = (Style)FindResource("Chip") };
                b.Click += async (_, _) =>
                {
                    Replace(Input.Text.Remove(issue.Offset, issue.Length).Insert(issue.Offset, rep));
                    await CheckNowAsync();
                };
                chips.Children.Add(b);
            }

            var explanation = new TextBlock
            {
                TextWrapping = TextWrapping.Wrap,
                Foreground = (Brush)FindResource("Fg"),
                FontSize = 12.5,
                Margin = new Thickness(0, 2, 0, 6),
                Visibility = Visibility.Collapsed,
            };
            var why = new Button { Content = IsSpanish ? "¿Por qué?" : "Why?", Style = (Style)FindResource("Icon"), Margin = new Thickness(0, 0, 0, 4),
                                   ToolTip = IsSpanish ? "Explicar la regla y el beneficio" : "Explain the rule and how it helps" };
            why.Click += async (_, _) => await ExplainAsync(issue, explanation);
            chips.Children.Add(why);

            IssueList.Children.Add(new Border
            {
                Background = (Brush)FindResource("Card"),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(12, 8, 12, 4),
                Margin = new Thickness(0, 8, 0, 0),
                Child = new StackPanel { Children = { chips, explanation } },
            });
        }
    }

    // First click asks the model (a few seconds); later clicks just show/hide the answer.
    async Task ExplainAsync(Issue issue, TextBlock target)
    {
        if (target.Tag is true) // already explained
        {
            target.Visibility = target.IsVisible ? Visibility.Collapsed : Visibility.Visible;
            return;
        }
        target.Text = IsSpanish ? "Explicando…" : "Explaining…";
        target.Visibility = Visibility.Visible;
        var ct = _cts?.Token ?? CancellationToken.None; // a new check rebuilds the cards, so it cancels this too
        try
        {
            var text = await Checker.ExplainAsync(Input.Text, issue, _lang, ct);
            target.Inlines.Clear();
            foreach (var line in text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (target.Inlines.Count > 0) target.Inlines.Add(new System.Windows.Documents.LineBreak());
                int colon = line.IndexOf(':');
                if (colon is > 0 and < 20) // "Rule:" / "Benefit:" labels in bold
                {
                    target.Inlines.Add(new System.Windows.Documents.Bold(new System.Windows.Documents.Run(line[..(colon + 1)])));
                    target.Inlines.Add(line[(colon + 1)..]);
                }
                else target.Inlines.Add(line);
            }
            target.Tag = true;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
        catch (Exception) { target.Text = IsSpanish ? "No se pudo explicar" : "Couldn't explain this one"; }
    }

    // Edits through the selection so Ctrl+Z can undo a fix or a translation.
    void Replace(string text)
    {
        Input.SelectAll();
        Input.SelectedText = text;
        Input.CaretIndex = text.Length;
    }

    async void FixAll_Click(object sender, RoutedEventArgs e)
    {
        Replace(_corrected);
        await CheckNowAsync();
    }

    // Runs after every check: the translation row always mirrors the text above in the other language.
    async Task TranslateAsync(string text, CancellationToken ct)
    {
        TranslationLabel.Text = IsSpanish ? "ENGLISH" : "ESPAÑOL";
        TranslationRow.Visibility = Visibility.Visible;
        UseTranslationBtn.IsEnabled = false;
        Translation.Text = "Translating…";
        try
        {
            var translated = await Checker.TranslateAsync(text, IsSpanish ? "en" : "es", ct);
            if (ct.IsCancellationRequested) return;
            Translation.Text = translated;
            UseTranslationBtn.IsEnabled = true;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { return; }
        catch (Exception)
        {
            Translation.Text = "Translation failed";
            UseTranslationBtn.IsEnabled = false;
        }
        TranslationRow.Opacity = 1;
    }

    async void UseTranslation_Click(object sender, RoutedEventArgs e)
    {
        Replace(Translation.Text);
        await CheckNowAsync();
    }

    async void Replace_Click(object sender, RoutedEventArgs e)
    {
        var text = Input.Text;
        History.Add(text);
        if (!_pinned) Hide();
        if (_target == IntPtr.Zero) Native.Try(() => Clipboard.SetText(text));
        else await Native.PasteAsync(_target, text);
    }

    void History_Click(object sender, RoutedEventArgs e)
    {
        bool show = HistoryPanel.Visibility != Visibility.Visible;
        HistoryPanel.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
        HistoryBtn.Foreground = (Brush)FindResource(show ? "Accent" : "Sub");
        if (show) RenderHistory();
    }

    void RenderHistory()
    {
        HistoryList.Children.Clear();
        ClearHistoryBtn.IsEnabled = History.Items.Count > 0;
        if (History.Items.Count == 0)
        {
            HistoryList.Children.Add(new TextBlock { Text = "Nothing yet. Texts you Replace or Copy show up here.",
                                                     Foreground = (Brush)FindResource("Sub"), Margin = new Thickness(0, 4, 0, 4) });
            return;
        }
        foreach (var entry in History.Items)
        {
            var b = new Button
            {
                Style = (Style)FindResource("Btn"),
                HorizontalContentAlignment = HorizontalAlignment.Left,
                Margin = new Thickness(0, 0, 0, 6),
                Padding = new Thickness(12, 6, 12, 6),
                ToolTip = entry.Text,
                Content = new StackPanel
                {
                    Children =
                    {
                        new TextBlock { Text = entry.When.ToString("g"), FontSize = 11, Foreground = (Brush)FindResource("Sub") },
                        new TextBlock { Text = entry.Text, TextWrapping = TextWrapping.Wrap, MaxHeight = 54, TextTrimming = TextTrimming.CharacterEllipsis },
                    },
                },
            };
            AutomationProperties.SetName(b, entry.Text);
            b.Click += async (_, _) =>
            {
                History_Click(this, new RoutedEventArgs());
                Replace(entry.Text);
                Input.Focus();
                await CheckNowAsync();
            };
            HistoryList.Children.Add(b);
        }
    }

    void ClearHistory_Click(object sender, RoutedEventArgs e)
    {
        History.Clear();
        RenderHistory();
    }

    void Close_Click(object sender, RoutedEventArgs e) => Hide();

    void OnKey(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape) { Hide(); e.Handled = true; }
        else if (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.Control) { Replace_Click(this, e); e.Handled = true; }
        else if (e.Key == Key.H && Keyboard.Modifiers == ModifierKeys.Control) { History_Click(this, e); e.Handled = true; }
    }

    void ApplyTheme()
    {
        bool light = Registry.GetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", "AppsUseLightTheme", 1) is not 0;
        var palette = light
            ? new Dictionary<string, string> { ["Bg"] = "#FFFFFF", ["Card"] = "#F3F4F6", ["Fg"] = "#111827", ["Sub"] = "#6B7280", ["Line"] = "#E5E7EB", ["Accent"] = "#2563EB", ["AccentSoft"] = "#E0E7FF", ["Bad"] = "#DC2626" }
            : new Dictionary<string, string> { ["Bg"] = "#1E1E22", ["Card"] = "#2A2A30", ["Fg"] = "#F3F4F6", ["Sub"] = "#9CA3AF", ["Line"] = "#34343A", ["Accent"] = "#60A5FA", ["AccentSoft"] = "#1E3A5F", ["Bad"] = "#F87171" };
        foreach (var (key, hex) in palette)
            Resources[key] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
    }
}
