using System;
using System.Windows.Forms;
using Microsoft.Win32;

namespace ScreenDimmer
{
    static class Settings
    {
        const string KeyPath = @"Software\ScreenDimmer";
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
                    if (k == null) return !MigrateLegacy();
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

        // The app was called Dimma before. Move its saved values to the new key.
        static bool MigrateLegacy()
        {
            const string legacyPath = @"Software\Dimma";
            using (var old = Registry.CurrentUser.OpenSubKey(legacyPath))
            {
                if (old == null) return false;
                Brightness = ReadInt(old, "Brightness", 100, MinBrightness, 100);
                Red = ReadInt(old, "Red", 0, 0, 100);
            }
            Save();
            try { Registry.CurrentUser.DeleteSubKeyTree(legacyPath, false); } catch { }
            return true;
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
        const string Name = "ScreenDimmer";

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
            if (Current() != null) return true;
            try
            {
                using (var k = Registry.CurrentUser.OpenSubKey(RunKey))
                    return k != null && k.GetValue("Dimma") != null; // from when the app was called Dimma
            }
            catch { return false; }
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
                    k.DeleteValue("Dimma", false); // entry from when the app was called Dimma
                }
            }
            catch { }
        }
    }
}
