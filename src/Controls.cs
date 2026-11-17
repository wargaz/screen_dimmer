using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace Dimma
{
    abstract class ThemedControl : Control
    {
        public Theme Theme;
        public float UiScale = 1f;
        protected bool hover;

        protected ThemedControl()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw |
                     ControlStyles.Selectable, true);
            TabStop = true;
        }

        protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); hover = true; Invalidate(); }
        protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); hover = false; Invalidate(); }
    }

    // Slider i Windows 11-stil.
    class Slider : ThemedControl
    {
        int val, minimum, maximum = 100;
        bool dragging;
        public event EventHandler ValueChanged;

        public int Minimum
        {
            get { return minimum; }
            set { minimum = value; if (val < minimum) Value = minimum; Invalidate(); }
        }

        public int Value
        {
            get { return val; }
            set
            {
                int v = Math.Max(minimum, Math.Min(maximum, value));
                if (v == val) return;
                val = v;
                Invalidate();
                if (ValueChanged != null) ValueChanged(this, EventArgs.Empty);
            }
        }

        float ThumbRadius { get { return 10f * UiScale; } }
        float TrackLeft { get { return ThumbRadius + 1; } }
        float TrackRight { get { return Width - ThumbRadius - 1; } }
        float ThumbX { get { return TrackLeft + (TrackRight - TrackLeft) * (val - minimum) / (float)(maximum - minimum); } }

        void SetFromX(int x)
        {
            float t = (x - TrackLeft) / (TrackRight - TrackLeft);
            t = Math.Max(0f, Math.Min(1f, t));
            Value = minimum + (int)Math.Round(t * (maximum - minimum));
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left) return;
            Focus();
            dragging = true;
            SetFromX(e.X);
            Invalidate();
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (dragging) SetFromX(e.X);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            dragging = false;
            Invalidate();
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            Value += e.Delta > 0 ? 2 : -2;
            var h = e as HandledMouseEventArgs;
            if (h != null) h.Handled = true;
        }

        protected override bool IsInputKey(Keys keyData)
        {
            switch (keyData)
            {
                case Keys.Left: case Keys.Right: case Keys.Up: case Keys.Down: return true;
            }
            return base.IsInputKey(keyData);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            switch (e.KeyCode)
            {
                case Keys.Left: case Keys.Down: Value -= 1; break;
                case Keys.Right: case Keys.Up: Value += 1; break;
                case Keys.PageDown: Value -= 10; break;
                case Keys.PageUp: Value += 10; break;
                case Keys.Home: Value = minimum; break;
                case Keys.End: Value = maximum; break;
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(BackColor);
            if (Theme == null) return;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            float cy = Height / 2f;
            float tx = ThumbX;
            float th = 4f * UiScale;

            using (var p = new Pen(Theme.Track, th) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                if (TrackRight > tx) g.DrawLine(p, tx, cy, TrackRight, cy);
            using (var p = new Pen(Theme.Accent, th) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                if (tx > TrackLeft) g.DrawLine(p, TrackLeft, cy, tx, cy);

            float r = ThumbRadius;
            using (var b = new SolidBrush(Theme.ThumbOuter))
                g.FillEllipse(b, tx - r, cy - r, 2 * r, 2 * r);
            using (var p = new Pen(Theme.ThumbBorder, 1f))
                g.DrawEllipse(p, tx - r, cy - r, 2 * r, 2 * r);

            float ir = r * (dragging ? 0.5f : (hover ? 0.7f : 0.6f));
            using (var b = new SolidBrush(Theme.Accent))
                g.FillEllipse(b, tx - ir, cy - ir, 2 * ir, 2 * ir);
        }
    }

    // Av/på-knapp i Windows 11-stil med text bredvid.
    class ToggleSwitch : ThemedControl
    {
        bool isChecked;
        public event EventHandler CheckedChanged;

        public ToggleSwitch() { Cursor = Cursors.Hand; }

        public bool Checked
        {
            get { return isChecked; }
            set
            {
                if (value == isChecked) return;
                isChecked = value;
                Invalidate();
                if (CheckedChanged != null) CheckedChanged(this, EventArgs.Empty);
            }
        }

        public void SetSilently(bool value)
        {
            isChecked = value;
            Invalidate();
        }

        protected override void OnClick(EventArgs e)
        {
            base.OnClick(e);
            Checked = !Checked;
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.KeyCode == Keys.Space) Checked = !Checked;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(BackColor);
            if (Theme == null) return;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            float w = 40f * UiScale, h = 20f * UiScale;
            float x = 1f, y = (Height - h) / 2f, cy = Height / 2f;

            using (var path = new GraphicsPath())
            {
                path.AddArc(x, y, h, h, 90, 180);
                path.AddArc(x + w - h, y, h, h, 270, 180);
                path.CloseFigure();

                if (isChecked)
                {
                    using (var b = new SolidBrush(Theme.Accent)) g.FillPath(b, path);
                    float kr = h * (hover ? 0.35f : 0.3f);
                    float kx = x + w - h / 2f;
                    using (var b = new SolidBrush(Theme.KnobOn)) g.FillEllipse(b, kx - kr, cy - kr, 2 * kr, 2 * kr);
                }
                else
                {
                    if (hover) using (var b = new SolidBrush(Theme.Hover)) g.FillPath(b, path);
                    using (var p = new Pen(Theme.SubText, Math.Max(1f, UiScale))) g.DrawPath(p, path);
                    float kr = h * (hover ? 0.3f : 0.25f);
                    float kx = x + h / 2f;
                    using (var b = new SolidBrush(Theme.SubText)) g.FillEllipse(b, kx - kr, cy - kr, 2 * kr, 2 * kr);
                }
            }

            int tx = (int)(x + w + 10 * UiScale);
            TextRenderer.DrawText(g, Text, Font, new Rectangle(tx, 0, Width - tx, Height), Theme.Text,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
        }
    }

    // Platt textknapp som får en bakgrund vid hovring.
    class FlatButton : ThemedControl
    {
        public FlatButton() { Cursor = Cursors.Hand; }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.KeyCode == Keys.Space || e.KeyCode == Keys.Enter) OnClick(EventArgs.Empty);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(BackColor);
            if (Theme == null) return;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            if (hover)
                using (var path = Theme.RoundedRect(new RectangleF(0, 0, Width - 1, Height - 1), 4 * UiScale))
                using (var b = new SolidBrush(Theme.Hover))
                    g.FillPath(b, path);
            TextRenderer.DrawText(g, Text, Font, ClientRectangle, Theme.Text,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
        }
    }
}
