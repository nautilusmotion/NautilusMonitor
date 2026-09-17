// NauTilus Monitor - Ajustes persistentes (registro de usuario, sin admin).
// Guarda idioma, alertas y el arranque con Windows.

using System;
using System.Reflection;
using Microsoft.Win32;

namespace NautilusMotion.Monitor
{
    public static class Settings
    {
        private const string Key = @"Software\NauTilus Motion\NauTilus Monitor";
        private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string RunName = "NauTilus Monitor";

        private static string ExePath()
        {
            try { return Assembly.GetExecutingAssembly().Location; } catch { return ""; }
        }

        // ---- Idioma ----
        public static string GetLang()
        {
            try { using (var k = Registry.CurrentUser.OpenSubKey(Key)) { if (k != null) return k.GetValue("Language") as string; } }
            catch { }
            return null;
        }

        public static void SetLang(string code)
        {
            try { using (var k = Registry.CurrentUser.CreateSubKey(Key)) k.SetValue("Language", code ?? "en"); }
            catch { }
        }

        // ---- Alertas de temperatura ----
        public static bool GetAlertsEnabled()
        {
            try { using (var k = Registry.CurrentUser.OpenSubKey(Key)) { if (k != null) { object v = k.GetValue("AlertsEnabled"); if (v != null) return Convert.ToInt32(v) != 0; } } }
            catch { }
            return true; // activadas por defecto
        }

        public static void SetAlertsEnabled(bool on)
        {
            try { using (var k = Registry.CurrentUser.CreateSubKey(Key)) k.SetValue("AlertsEnabled", on ? 1 : 0, RegistryValueKind.DWord); }
            catch { }
        }

        public static int GetTempAlertC()
        {
            try { using (var k = Registry.CurrentUser.OpenSubKey(Key)) { if (k != null) { object v = k.GetValue("TempAlertC"); if (v != null) { int n = Convert.ToInt32(v); if (n >= 50 && n <= 110) return n; } } } }
            catch { }
            return 90; // umbral por defecto
        }

        // ---- Arranque con Windows (clave Run del usuario, sin admin) ----
        public static bool GetStartWithWindows()
        {
            try { using (var k = Registry.CurrentUser.OpenSubKey(RunKey)) return k != null && k.GetValue(RunName) != null; }
            catch { return false; }
        }

        public static void SetStartWithWindows(bool on)
        {
            try
            {
                using (var k = Registry.CurrentUser.CreateSubKey(RunKey))
                {
                    if (on) k.SetValue(RunName, "\"" + ExePath() + "\" --autostart");
                    else if (k.GetValue(RunName) != null) k.DeleteValue(RunName, false);
                }
            }
            catch { }
        }
    }
}
