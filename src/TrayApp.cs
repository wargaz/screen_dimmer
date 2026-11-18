using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

namespace ScreenDimmer
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            bool created;
            using (var mutex = new Mutex(true, @"Local\ScreenDimmer.SingleInstance", out created))
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
            // First run: turn autostart on. Otherwise: update the path in case the exe was moved.
            Autostart.SyncOnStartup(firstRun);
            if (firstRun) Settings.Save();

            popup = new Popup();
            popup.ValuesChanged += (o, e) => UpdateTooltip();

            var resetItem = new ToolStripMenuItem("Reset", null, (o, e) => popup.ResetValues());
            resetItem.ShortcutKeyDisplayString = "Ctrl+Alt+End";
            var autoItem = new ToolStripMenuItem("Start with Windows");
            autoItem.Click += (o, e) => Autostart.Set(!Autostart.IsEnabled());
            var menu = new ContextMenuStrip();
            menu.Opening += (o, e) => autoItem.Checked = Autostart.IsEnabled();
            menu.Items.Add(resetItem);
            menu.Items.Add(autoItem);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("Exit", null, (o, e) => ExitThread());

            tray = new NotifyIcon { ContextMenuStrip = menu };
            tray.MouseUp += (o, e) => { if (e.Button == MouseButtons.Left) popup.Toggle(); };
            UpdateIcon();
            UpdateTooltip();
            tray.Visible = true;

            if (!ColorFilter.Apply(Settings.Brightness / 100.0, Settings.Red / 100.0))
                tray.ShowBalloonTip(5000, "ScreenDimmer",
                    "Could not turn on the color filter. Is Windows Magnifier running?", ToolTipIcon.Warning);

            hotkeys = new HotkeyWindow();
            hotkeys.Pressed += () => popup.ResetValues();

            // Windows can drop the filter after sleep, display changes or unlock, so apply it again.
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
            tray.Text = "ScreenDimmer\nBrightness " + Settings.Brightness + "%  ·  Red tint " + Settings.Red + "%";
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

        // A half-filled circle, the usual symbol for contrast/brightness.
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

    // Global hotkey Ctrl+Alt+End that resets the screen if it has been dimmed too much.
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
