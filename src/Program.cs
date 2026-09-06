// NauTilus Monitor - Punto de entrada
// Desarrollado por NauTilus Motion (https://nautilusmotion.com)
// Codigo abierto - Licencia MIT

using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Security.Principal;
using System.Threading;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace NautilusMotion.Monitor
{
    public static class Program
    {
        private static Mutex _mutex;

        // Peticion de sensores avanzados heredada de una relanzada elevada.
        public static bool AutoAdvanced;
        // Modo demostracion (para capturas de documentacion): datos representativos, sin timer.
        public static bool DemoMode;

        [STAThread]
        public static void Main(string[] args)
        {
            // Modo interno de captura de pantalla (documentacion). No lee sensores en vivo.
            // Uso: --shot <ruta> [lang] [picker]
            if (args != null && args.Length >= 2 && args[0] == "--shot")
            {
                DemoMode = true;
                bool picker = false;
                for (int i = 2; i < args.Length; i++)
                {
                    if (args[i] == "picker") picker = true;
                    else SetLangFromCode(args[i]);
                }
                RenderShot(args[1], picker);
                return;
            }

            // Prueba interna de grabacion (sin GUI): graba N segundos con sensores
            // reales a un CSV.  Uso: --rectest <ruta> [segundos] [lang]
            if (args != null && args.Length >= 2 && args[0] == "--rectest")
            {
                int secs = 5;
                if (args.Length >= 3) int.TryParse(args[2], out secs);
                for (int i = 3; i < args.Length; i++) SetLangFromCode(args[i]);
                RunRecTest(args[1], secs);
                return;
            }

            // Relanzado elevado para activar los sensores avanzados: --advanced [lang]
            if (args != null && args.Length >= 1 && args[0] == "--advanced")
            {
                AutoAdvanced = true;
                if (args.Length >= 2) SetLangFromCode(args[1]);

                var app0 = new Application();
                app0.ShutdownMode = ShutdownMode.OnLastWindowClose;
                HookErrors(app0);
                var main0 = new MainWindow();
                app0.Run(main0);
                return;
            }

            // Una sola instancia a la vez
            bool isNew;
            _mutex = new Mutex(true, "NautilusMotion.Monitor.SingleInstance", out isNew);
            if (!isNew) return;

            var app = new Application();
            app.ShutdownMode = ShutdownMode.OnLastWindowClose;
            HookErrors(app);

            var startWindow = new LanguageWindow();
            app.Run(startWindow);

            GC.KeepAlive(_mutex);
        }

        private static void HookErrors(Application app)
        {
            app.DispatcherUnhandledException += delegate(object s, DispatcherUnhandledExceptionEventArgs e)
            {
                MessageBox.Show("An unexpected error occurred:\n\n" + e.Exception.Message,
                    "NauTilus Monitor", MessageBoxButton.OK, MessageBoxImage.Warning);
                e.Handled = true;
            };
        }

        public static bool IsElevated()
        {
            try
            {
                var id = WindowsIdentity.GetCurrent();
                var pr = new WindowsPrincipal(id);
                return pr.IsInRole(WindowsBuiltInRole.Administrator);
            }
            catch { return false; }
        }

        // Relanza la app como administrador para cargar los sensores avanzados.
        // Devuelve true si la nueva instancia arranco (esta debe cerrarse).
        public static bool RelaunchElevated(string langCode)
        {
            try
            {
                string exe = Assembly.GetExecutingAssembly().Location;
                var psi = new ProcessStartInfo(exe, "--advanced " + langCode);
                psi.UseShellExecute = true;
                psi.Verb = "runas"; // dispara UAC
                Process.Start(psi);
                return true;
            }
            catch { return false; } // el usuario cancelo el UAC, o fallo
        }

        public static string LangCode()
        {
            switch (Loc.Current)
            {
                case Lang.En: return "en";
                case Lang.Es: return "es";
                case Lang.Ru: return "ru";
                case Lang.Fr: return "fr";
                case Lang.De: return "de";
                case Lang.Pt: return "pt";
            }
            return "en";
        }

        private static void SetLangFromCode(string code)
        {
            switch (code)
            {
                case "en": Loc.Current = Lang.En; break;
                case "es": Loc.Current = Lang.Es; break;
                case "ru": Loc.Current = Lang.Ru; break;
                case "fr": Loc.Current = Lang.Fr; break;
                case "de": Loc.Current = Lang.De; break;
                case "pt": Loc.Current = Lang.Pt; break;
            }
        }

        private static void RunRecTest(string path, int secs)
        {
            var sampler = new Sampler();
            try { sampler.Warmup(); } catch { }
            if (IsElevated()) { try { AdvancedSensors.Enable(); } catch { } }
            var logger = new TempLogger();
            if (!logger.Start(path)) return;
            Thread.Sleep(700);
            for (int i = 0; i < secs; i++)
            {
                Snapshot s = null;
                try { s = sampler.Sample(); } catch { }
                if (s != null) logger.Write(s);
                Thread.Sleep(1000);
            }
            logger.Stop();
        }

        private static void RenderShot(string path, bool picker)
        {
            var app = new Application();
            app.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            Window w = picker ? (Window)new LanguageWindow() : new MainWindow();
            w.WindowStartupLocation = WindowStartupLocation.Manual;
            w.Left = -12000;
            w.Top = -12000;
            w.ShowInTaskbar = false;
            w.Loaded += delegate
            {
                var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
                timer.Tick += delegate
                {
                    timer.Stop();
                    try
                    {
                        int width = (int)Math.Ceiling(w.ActualWidth);
                        int height = (int)Math.Ceiling(w.ActualHeight);
                        var rtb = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
                        rtb.Render(w);
                        var enc = new PngBitmapEncoder();
                        enc.Frames.Add(BitmapFrame.Create(rtb));
                        using (var fs = File.Create(path))
                            enc.Save(fs);
                    }
                    catch { }
                    app.Shutdown();
                };
                timer.Start();
            };
            w.Show();
            app.Run();
        }
    }
}
