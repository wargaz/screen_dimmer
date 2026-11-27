using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Security;
using System.Security.Principal;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using Microsoft.Win32;

namespace ScreenDimmer
{
    static class Settings
    {
        const string KeyPath = @"Software\ScreenDimmer";
        public const int MinBrightness = 20;

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

    // Starts the app at sign-in through a Task Scheduler logon task. Earlier versions used the
    // registry Run key, but Windows sometimes skips entries there without any error.
    static class Autostart
    {
        const string TaskName = "ScreenDimmer";
        const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        static readonly string[] LegacyRunValues = { "ScreenDimmer", "Dimma" };
        static bool? enabled;

        static string ExePath { get { return Application.ExecutablePath; } }

        /// <summary>Called once at startup: moves old Run entries over and fixes a moved exe path.</summary>
        public static void SyncOnStartup(bool firstRun)
        {
            bool legacy = LegacyRunExists();
            string command = TaskCommand();
            if (!firstRun && !legacy && command == null) { enabled = false; return; }
            if (legacy || !SamePath(command, ExePath)) Set(true);
            else enabled = true;
        }

        public static bool IsEnabled()
        {
            if (enabled == null) enabled = SamePath(TaskCommand(), ExePath);
            return enabled.Value;
        }

        public static void Set(bool on)
        {
            RemoveLegacyRun();
            string output;
            if (on) enabled = CreateTask();
            else
            {
                RunSchtasks("/Delete /TN \"" + TaskName + "\" /F", out output);
                enabled = false;
            }
        }

        static bool CreateTask()
        {
            string user = SecurityElement.Escape(WindowsIdentity.GetCurrent().Name);
            string exe = SecurityElement.Escape(ExePath);
            string dir = SecurityElement.Escape(Path.GetDirectoryName(ExePath));
            string xml =
                "<?xml version=\"1.0\" encoding=\"UTF-16\"?>\r\n" +
                "<Task version=\"1.2\" xmlns=\"http://schemas.microsoft.com/windows/2004/02/mit/task\">\r\n" +
                "  <RegistrationInfo><Description>Starts ScreenDimmer when you sign in.</Description></RegistrationInfo>\r\n" +
                "  <Triggers><LogonTrigger><Enabled>true</Enabled><UserId>" + user + "</UserId><Delay>PT3S</Delay></LogonTrigger></Triggers>\r\n" +
                "  <Principals><Principal id=\"Author\"><UserId>" + user + "</UserId>" +
                "<LogonType>InteractiveToken</LogonType><RunLevel>LeastPrivilege</RunLevel></Principal></Principals>\r\n" +
                "  <Settings>" +
                "<MultipleInstancesPolicy>IgnoreNew</MultipleInstancesPolicy>" +
                "<DisallowStartIfOnBatteries>false</DisallowStartIfOnBatteries>" +
                "<StopIfGoingOnBatteries>false</StopIfGoingOnBatteries>" +
                "<ExecutionTimeLimit>PT0S</ExecutionTimeLimit>" +
                "<Priority>4</Priority>" +
                "<AllowStartOnDemand>true</AllowStartOnDemand>" +
                "<Enabled>true</Enabled>" +
                "</Settings>\r\n" +
                "  <Actions Context=\"Author\"><Exec><Command>" + exe + "</Command>" +
                "<WorkingDirectory>" + dir + "</WorkingDirectory></Exec></Actions>\r\n" +
                "</Task>\r\n";

            string file = Path.Combine(Path.GetTempPath(), "ScreenDimmer-task.xml");
            try
            {
                File.WriteAllText(file, xml, Encoding.Unicode);
                string output;
                return RunSchtasks("/Create /TN \"" + TaskName + "\" /XML \"" + file + "\" /F", out output) == 0;
            }
            catch { return false; }
            finally { try { File.Delete(file); } catch { } }
        }

        // The exe path the task starts, or null if there is no task.
        static string TaskCommand()
        {
            string xml;
            if (RunSchtasks("/Query /TN \"" + TaskName + "\" /XML", out xml) != 0) return null;
            var m = Regex.Match(xml, "<Command>(.*?)</Command>", RegexOptions.Singleline);
            return m.Success ? WebUtility.HtmlDecode(m.Groups[1].Value).Trim().Trim('"') : null;
        }

        static int RunSchtasks(string args, out string output)
        {
            output = "";
            try
            {
                var psi = new ProcessStartInfo("schtasks.exe", args)
                {
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                };
                using (var p = Process.Start(psi))
                {
                    output = p.StandardOutput.ReadToEnd();
                    p.StandardError.ReadToEnd();
                    p.WaitForExit(10000);
                    return p.ExitCode;
                }
            }
            catch { return -1; }
        }

        static bool SamePath(string a, string b)
        {
            return a != null && string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
        }

        static bool LegacyRunExists()
        {
            try
            {
                using (var k = Registry.CurrentUser.OpenSubKey(RunKey))
                {
                    if (k == null) return false;
                    foreach (string name in LegacyRunValues)
                        if (k.GetValue(name) != null) return true;
                }
            }
            catch { }
            return false;
        }

        static void RemoveLegacyRun()
        {
            try
            {
                using (var k = Registry.CurrentUser.OpenSubKey(RunKey, true))
                {
                    if (k == null) return;
                    foreach (string name in LegacyRunValues) k.DeleteValue(name, false);
                }
            }
            catch { }
        }
    }
}
