using System;
using System.Windows.Forms;
using Microsoft.Win32;

namespace Dimma
{
    static class Settings
    {
        const string KeyPath = @"Software\Dimma";
        public const int MinBrightness = 5;

        public static int Brightness = 100;
        public static int Red = 0;

        /// <returns>true if this is the first time the app runs.</returns>
        public static bool Load()
        {
            try
            {
                using (var k = Registry.CurrentUser.OpenSubKey(KeyPath))
                {
                    if (k == null) return true;
                    Brightness = ReadInt(k, "Brightness", 100, MinBrightness, 100);
                    Red = ReadInt(k, "Red", 0, 0, 100);
                    return false;
                }
            }
            catch
            {
                return false;
            }
        }

        public static void Save()
        {
            try
            {
                using (var k = Registry.CurrentUser.CreateSubKey(KeyPath))
                {
                    k.SetValue("Brightness", Brightness, RegistryValueKind.DWord);
                    k.SetValue("Red", Red, RegistryValueKind.DWord);
                }
            }
            catch { }
        }

        static int ReadInt(RegistryKey k, string name, int fallback, int min, int max)
        {
            int v;
            try { v = Convert.ToInt32(k.GetValue(name, fallback)); }
            catch { v = fallback; }
            return Math.Max(min, Math.Min(max, v));
        }
    }

    static class Autostart
    {
        const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        const string Name = "Dimma";

        static string Command()
        {
            return "\"" + Application.ExecutablePath + "\"";
        }

        static string Current()
        {
            try
            {
                using (var k = Registry.CurrentUser.OpenSubKey(RunKey))
                    return k == null ? null : k.GetValue(Name) as string;
            }
            catch { return null; }
        }

        public static bool IsRegistered()
        {
            return Current() != null;
        }

        public static bool IsEnabled()
        {
            return string.Equals(Current(), Command(), StringComparison.OrdinalIgnoreCase);
        }

        public static void Set(bool on)
        {
            try
            {
                using (var k = Registry.CurrentUser.CreateSubKey(RunKey))
                {
                    if (on) k.SetValue(Name, Command());
                    else k.DeleteValue(Name, false);
                }
            }
            catch { }
        }
    }
}
