using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using Microsoft.Win32;

namespace ScreenDimmer
{
    // Colors in the style of the Windows 11 quick settings, in light and dark mode.
    class Theme
    {
        public bool Dark;
        public Color Back, Footer, Text, SubText, Track, Accent, ThumbOuter, ThumbBorder, Hover, KnobOn;

        public static bool SystemIsDark()
        {
            try
            {
                object v = Registry.GetValue(
                    @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize",
                    "SystemUsesLightTheme", 1);
                return v is int && (int)v == 0;
            }
            catch { return false; }
        }

        public static Theme Current()
        {
            bool dark = SystemIsDark();
            var t = new Theme { Dark = dark, Accent = ReadAccent(dark) };
            if (dark)
            {
                t.Back = Color.FromArgb(44, 44, 44);
                t.Footer = Color.FromArgb(32, 32, 32);
                t.Text = Color.FromArgb(255, 255, 255);
                t.SubText = Color.FromArgb(200, 200, 200);
                t.Track = Color.FromArgb(140, 140, 140);
                t.ThumbOuter = Color.FromArgb(69, 69, 69);
                t.ThumbBorder = Color.FromArgb(85, 85, 85);
                t.Hover = Color.FromArgb(52, 52, 52);
                t.KnobOn = Color.FromArgb(0, 0, 0);
            }
            else
            {
                t.Back = Color.FromArgb(249, 249, 249);
                t.Footer = Color.FromArgb(238, 238, 238);
                t.Text = Color.FromArgb(26, 26, 26);
                t.SubText = Color.FromArgb(96, 96, 96);
                t.Track = Color.FromArgb(135, 135, 135);
                t.ThumbOuter = Color.FromArgb(255, 255, 255);
                t.ThumbBorder = Color.FromArgb(215, 215, 215);
                t.Hover = Color.FromArgb(225, 225, 225);
                t.KnobOn = Color.FromArgb(255, 255, 255);
            }
            return t;
        }

        // AccentPalette holds 8 colors (RGBA). Dark mode uses a lighter variant.
        static Color ReadAccent(bool dark)
        {
            try
            {
                var p = Registry.GetValue(
                    @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Accent",
                    "AccentPalette", null) as byte[];
                if (p != null && p.Length >= 32)
                {
                    int i = dark ? 1 : 4;
                    return Color.FromArgb(p[i * 4], p[i * 4 + 1], p[i * 4 + 2]);
                }
            }
            catch { }
            return dark ? Color.FromArgb(76, 194, 255) : Color.FromArgb(0, 95, 184);
        }

        public static GraphicsPath RoundedRect(RectangleF r, float radius)
        {
            float d = Math.Min(radius * 2, Math.Min(r.Width, r.Height));
            var p = new GraphicsPath();
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }
    }
}
