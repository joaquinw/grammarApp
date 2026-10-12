using System;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Win32;
using Forms = System.Windows.Forms;

namespace GrammarApp;

public static class Program
{
    [STAThread]
    static int Main(string[] args)
    {
        using var mutex = new Mutex(true, "GrammarApp.SingleInstance", out bool first);
        if (!first) return 0;

        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        // ponytail: a tray app must never die from one bad inference; errors are already shown in the status line
        app.DispatcherUnhandledException += (_, e) => e.Handled = true;
        var win = new MainWindow();
        using var tray = CreateTray(win);
        // Load the model now so the first hotkey press is fast; then show which device runs it.
        _ = Checker.WarmUpAsync().ContinueWith(t => app.Dispatcher.Invoke(() =>
        {
            var text = t.IsFaulted ? "GrammarApp · model.gguf missing" : $"GrammarApp · {Checker.Device}";
            tray.Text = text.Length > 63 ? text[..63] : text; // NotifyIcon tooltip limit
        }));
        app.Run();
        return 0;
    }

    static Forms.NotifyIcon CreateTray(MainWindow win)
    {
        var tray = new Forms.NotifyIcon { Icon = MakeIcon(), Text = "GrammarApp  (Ctrl+Shift+Space)", Visible = true };
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("Open", null, (_, _) => _ = win.SummonAsync(capture: false));
        var startup = new Forms.ToolStripMenuItem("Start with Windows") { CheckOnClick = true, Checked = StartupEnabled };
        startup.CheckedChanged += (_, _) => StartupEnabled = startup.Checked;
        menu.Items.Add(startup);
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("Exit", null, (_, _) => { tray.Visible = false; Application.Current.Shutdown(); });
        tray.ContextMenuStrip = menu;
        tray.MouseClick += (_, e) => { if (e.Button == Forms.MouseButtons.Left) _ = win.SummonAsync(capture: false); };

        if (win.HotkeyOk)
            tray.ShowBalloonTip(4000, "GrammarApp is running", "Select text in any app and press Ctrl+Shift+Space.", Forms.ToolTipIcon.Info);
        else
            tray.ShowBalloonTip(6000, "Hotkey unavailable", "Ctrl+Shift+Space is taken by another app. Click this tray icon instead.", Forms.ToolTipIcon.Warning);
        return tray;
    }

    const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";

    static bool StartupEnabled
    {
        get => Registry.CurrentUser.OpenSubKey(RunKey)?.GetValue("GrammarApp") != null;
        set
        {
            using var key = Registry.CurrentUser.CreateSubKey(RunKey);
            if (value) key.SetValue("GrammarApp", $"\"{Environment.ProcessPath}\"");
            else key.DeleteValue("GrammarApp", false);
        }
    }

    static Icon MakeIcon()
    {
        using var bmp = new Bitmap(32, 32);
        using (var g = Graphics.FromImage(bmp))
        using (var bg = new SolidBrush(Color.FromArgb(37, 99, 235)))
        using (var font = new Font("Segoe UI", 17, System.Drawing.FontStyle.Bold, GraphicsUnit.Pixel))
        using (var fmt = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
        {
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
            g.FillEllipse(bg, 1, 1, 30, 30);
            g.DrawString("G", font, Brushes.White, new RectangleF(0, 1, 32, 32), fmt);
        }
        return Icon.FromHandle(bmp.GetHicon());
    }
}
