using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

namespace Dimma
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            bool created;
            using (var mutex = new Mutex(true, @"Local\Dimma.SingleInstance", out created))
            {
                if (!created) return;
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new TrayApp());
            }
        }
    }

    class TrayApp : ApplicationContext
    {
        readonly NotifyIcon tray;
        readonly Popup popup;
        readonly HotkeyWindow hotkeys;
        readonly System.Windows.Forms.Timer reapplyTimer;
        Icon icon;

        public TrayApp()
        {
            bool firstRun = Settings.Load();
            // Första gången: slå på autostart. Annars: uppdatera sökvägen om exe-filen flyttats.
            if (firstRun || Autostart.IsRegistered()) Autostart.Set(true);
            if (firstRun) Settings.Save();

            popup = new Popup();
            popup.ValuesChanged += (o, e) => UpdateTooltip();

            var resetItem = new ToolStripMenuItem("Återställ", null, (o, e) => popup.ResetValues());
            resetItem.ShortcutKeyDisplayString = "Ctrl+Alt+End";
            var autoItem = new ToolStripMenuItem("Starta med Windows");
            autoItem.Click += (o, e) => Autostart.Set(!Autostart.IsEnabled());
            var menu = new ContextMenuStrip();
            menu.Opening += (o, e) => autoItem.Checked = Autostart.IsEnabled();
            menu.Items.Add(resetItem);
            menu.Items.Add(autoItem);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("Avsluta", null, (o, e) => ExitThread());

            tray = new NotifyIcon { ContextMenuStrip = menu };
            tray.MouseUp += (o, e) => { if (e.Button == MouseButtons.Left) popup.Toggle(); };
            UpdateIcon();
            UpdateTooltip();
            tray.Visible = true;

            if (!ColorFilter.Apply(Settings.Brightness / 100.0, Settings.Red / 100.0))
                tray.ShowBalloonTip(5000, "Dimma",
                    "Kunde inte aktivera färgfiltret. Är Förstoringsglaset igång?", ToolTipIcon.Warning);

            hotkeys = new HotkeyWindow();
            hotkeys.Pressed += () => popup.ResetValues();

            // Efter vila, skärmbyte eller upplåsning kan Windows tappa filtret, så lägg på det igen.
            reapplyTimer = new System.Windows.Forms.Timer { Interval = 1500 };
            reapplyTimer.Tick += (o, e) => { reapplyTimer.Stop(); ColorFilter.Reapply(); };

            SystemEvents.DisplaySettingsChanged += OnSystemChanged;
            SystemEvents.PowerModeChanged += OnPowerChanged;
            SystemEvents.SessionSwitch += OnSessionSwitch;
            SystemEvents.UserPreferenceChanged += OnPreferenceChanged;
        }

        void Reapply()
        {
            ColorFilter.Reapply();
            reapplyTimer.Stop();
            reapplyTimer.Start();
        }

        void OnSystemChanged(object sender, EventArgs e) { Reapply(); }

        void OnPowerChanged(object sender, PowerModeChangedEventArgs e)
        {
            if (e.Mode == PowerModes.Resume) Reapply();
        }

        void OnSessionSwitch(object sender, SessionSwitchEventArgs e)
        {
            if (e.Reason == SessionSwitchReason.SessionUnlock || e.Reason == SessionSwitchReason.ConsoleConnect) Reapply();
        }

        void OnPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
        {
            if (e.Category == UserPreferenceCategory.General) UpdateIcon();
        }

        void UpdateTooltip()
        {
            tray.Text = "Dimma\nLjusstyrka " + Settings.Brightness + " %  ·  Röd ton " + Settings.Red + " %";
        }

        void UpdateIcon()
        {
            var old = icon;
            icon = MakeIcon(Theme.SystemIsDark());
            tray.Icon = icon;
            if (old != null) old.Dispose();
        }

        [DllImport("user32.dll")]
        static extern bool DestroyIcon(IntPtr hIcon);

        // En halvfylld cirkel, samma symbol som för kontrast/ljusstyrka.
        static Icon MakeIcon(bool darkTaskbar)
        {
            int size = SystemInformation.SmallIconSize.Width;
            using (var bmp = new Bitmap(size, size))
            {
                using (var g = Graphics.FromImage(bmp))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    Color c = darkTaskbar ? Color.White : Color.Black;
                    float stroke = Math.Max(1f, size / 14f);
                    float m = size * 0.1f + stroke / 2f;
                    float d = size - 2 * m;
                    using (var b = new SolidBrush(c)) g.FillPie(b, m, m, d, d, 90, 180);
                    using (var p = new Pen(c, stroke)) g.DrawEllipse(p, m, m, d, d);
                }
                IntPtr h = bmp.GetHicon();
                var result = (Icon)Icon.FromHandle(h).Clone();
                DestroyIcon(h);
                return result;
            }
        }

        protected override void ExitThreadCore()
        {
            SystemEvents.DisplaySettingsChanged -= OnSystemChanged;
            SystemEvents.PowerModeChanged -= OnPowerChanged;
            SystemEvents.SessionSwitch -= OnSessionSwitch;
            SystemEvents.UserPreferenceChanged -= OnPreferenceChanged;
            reapplyTimer.Dispose();
            hotkeys.Dispose();
            tray.Visible = false;
            tray.Dispose();
            Settings.Save();
            ColorFilter.Shutdown();
            popup.Dispose();
            base.ExitThreadCore();
        }
    }

    // Global snabbtangent Ctrl+Alt+End som återställer skärmen om man dimmat för mycket.
    class HotkeyWindow : NativeWindow, IDisposable
    {
        [DllImport("user32.dll")]
        static extern bool RegisterHotKey(IntPtr hWnd, int id, uint mods, uint vk);

        [DllImport("user32.dll")]
        static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        const int WM_HOTKEY = 0x0312;
        const uint MOD_ALT = 0x1, MOD_CONTROL = 0x2, MOD_NOREPEAT = 0x4000;
        const int Id = 1;

        public event Action Pressed;

        public HotkeyWindow()
        {
            CreateHandle(new CreateParams());
            RegisterHotKey(Handle, Id, MOD_CONTROL | MOD_ALT | MOD_NOREPEAT, (uint)Keys.End);
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_HOTKEY && m.WParam.ToInt32() == Id && Pressed != null) Pressed();
            base.WndProc(ref m);
        }

        public void Dispose()
        {
            UnregisterHotKey(Handle, Id);
            DestroyHandle();
        }
    }
}
