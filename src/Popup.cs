using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Dimma
{
    // Den lilla panelen som dyker upp i nedre högra hörnet när man klickar på ikonen.
    class Popup : Form
    {
        [DllImport("dwmapi.dll")]
        static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

        [DllImport("user32.dll")]
        static extern bool SetForegroundWindow(IntPtr hWnd);

        const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
        const int DWMWCP_ROUND = 2;

        readonly Slider bright, red;
        readonly ToggleSwitch autostart;
        readonly FlatButton reset;
        readonly float s;
        Theme theme;
        Rectangle captionBright, captionRed, footer;
        DateTime lastHidden = DateTime.MinValue;

        public event EventHandler ValuesChanged;

        public Popup()
        {
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            TopMost = true;
            StartPosition = FormStartPosition.Manual;
            KeyPreview = true;
            AutoScaleMode = AutoScaleMode.None;
            Text = "Dimma";
            Font = new Font("Segoe UI", 9f);

            using (var g = CreateGraphics()) s = g.DpiX / 96f;

            bright = new Slider { UiScale = s, Minimum = Settings.MinBrightness };
            bright.Value = Settings.Brightness;
            red = new Slider { UiScale = s };
            red.Value = Settings.Red;
            bright.ValueChanged += OnSliderChanged;
            red.ValueChanged += OnSliderChanged;

            autostart = new ToggleSwitch { UiScale = s, Text = "Starta med Windows" };
            autostart.SetSilently(Autostart.IsEnabled());
            autostart.CheckedChanged += (o, e) => Autostart.Set(autostart.Checked);

            reset = new FlatButton { UiScale = s, Text = "Återställ" };
            reset.Click += (o, e) => ResetValues();

            Controls.AddRange(new Control[] { bright, red, autostart, reset });
            LayoutControls();
            ApplyTheme();
        }

        int S(float v) { return (int)Math.Round(v * s); }

        void LayoutControls()
        {
            int w = S(340), pad = S(20), inset = S(11);
            int y = S(16);

            captionBright = new Rectangle(pad, y, w - 2 * pad, S(20));
            y += S(22);
            bright.Bounds = new Rectangle(pad - inset, y, w - 2 * pad + 2 * inset, S(32));
            y += S(32) + S(12);

            captionRed = new Rectangle(pad, y, w - 2 * pad, S(20));
            y += S(22);
            red.Bounds = new Rectangle(pad - inset, y, w - 2 * pad + 2 * inset, S(32));
            y += S(32) + S(16);

            int footerH = S(56);
            footer = new Rectangle(0, y, w, footerH);
            autostart.Bounds = new Rectangle(pad, y + (footerH - S(28)) / 2, S(200), S(28));
            reset.Bounds = new Rectangle(w - S(12) - S(90), y + (footerH - S(32)) / 2, S(90), S(32));

            ClientSize = new Size(w, y + footerH);
        }

        void ApplyTheme()
        {
            theme = Theme.Current();
            BackColor = theme.Back;
            foreach (Control c in Controls)
            {
                var tc = c as ThemedControl;
                if (tc == null) continue;
                tc.Theme = theme;
                tc.BackColor = footer.Contains(c.Bounds) ? theme.Footer : theme.Back;
                tc.Invalidate();
            }
            Invalidate();
        }

        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                cp.ClassStyle |= 0x20000; // CS_DROPSHADOW
                cp.ExStyle |= 0x80;       // WS_EX_TOOLWINDOW, syns inte i Alt+Tab
                return cp;
            }
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            int pref = DWMWCP_ROUND;
            try { DwmSetWindowAttribute(Handle, DWMWA_WINDOW_CORNER_PREFERENCE, ref pref, sizeof(int)); }
            catch { }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(theme.Back);
            using (var b = new SolidBrush(theme.Footer)) g.FillRectangle(b, footer);

            var left = TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix;
            var right = TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix;

            TextRenderer.DrawText(g, "Ljusstyrka", Font, captionBright, theme.Text, left);
            TextRenderer.DrawText(g, bright.Value + " %", Font, captionBright, theme.SubText, right);
            TextRenderer.DrawText(g, "Röd ton", Font, captionRed, theme.Text, left);
            TextRenderer.DrawText(g, red.Value + " %", Font, captionRed, theme.SubText, right);
        }

        void OnSliderChanged(object sender, EventArgs e)
        {
            Settings.Brightness = bright.Value;
            Settings.Red = red.Value;
            ColorFilter.Apply(bright.Value / 100.0, red.Value / 100.0);
            Invalidate(captionBright);
            Invalidate(captionRed);
            if (ValuesChanged != null) ValuesChanged(this, EventArgs.Empty);
        }

        public void ResetValues()
        {
            bright.Value = 100;
            red.Value = 0;
            Settings.Save();
        }

        public void Toggle()
        {
            if (Visible) { Hide(); return; }
            // Ett klick på ikonen när panelen är öppen stänger den först (Deactivate)
            // och skulle annars öppna den igen direkt.
            if ((DateTime.Now - lastHidden).TotalMilliseconds < 300) return;
            ShowAtTray();
        }

        void ShowAtTray()
        {
            ApplyTheme();
            autostart.SetSilently(Autostart.IsEnabled());
            Rectangle wa = Screen.FromPoint(Cursor.Position).WorkingArea;
            Location = new Point(wa.Right - Width - S(12), wa.Bottom - Height - S(12));
            Show();
            Activate();
            SetForegroundWindow(Handle);
            bright.Focus();
        }

        protected override void OnDeactivate(EventArgs e)
        {
            base.OnDeactivate(e);
            lastHidden = DateTime.Now;
            Hide();
            Settings.Save();
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.KeyCode == Keys.Escape) Hide();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            // Stäng aldrig panelen på riktigt via Alt+F4, göm den bara.
            if (e.CloseReason == CloseReason.UserClosing) { e.Cancel = true; Hide(); }
            base.OnFormClosing(e);
        }
    }
}
