// NauTilus Monitor - Interfaz principal (WPF construida en codigo, sin XAML)
// Desarrollado por NauTilus Motion (https://nautilusmotion.com)

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace NautilusMotion.Monitor
{
    public class MainWindow : Window
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        private Sampler _sampler;
        private StaticInfo _info;
        private Thread _worker;
        private volatile bool _run = true;

        // Gauges
        private Gauge _gCpu, _gGpu, _gRam, _gDisk;

        // CPU
        private TextBlock _cpuNameTx, _clockTx, _coresTx;
        private UniformGrid _coreHost;
        private BarMeter[] _coreBars;
        private TextBlock[] _corePcts;

        // Fichas dinamicas
        private StackPanel _tempsHost, _fansHost;
        private BarMeter _diskBar;
        private TextBlock _diskRWTx, _netDownTx, _netUpTx, _uptimeTx, _procTx, _osTx, _boardTx;

        // Sensores avanzados
        private Border _advChip;
        private TextBlock _advChipTx, _advHint;

        // Grabacion de temperaturas (a CSV)
        private Border _recChip, _openChip;
        private TextBlock _recChipTx, _recStatus;
        private readonly TempLogger _logger = new TempLogger();

        // Segundo plano (bandeja del sistema)
        private System.Windows.Forms.NotifyIcon _tray;
        private volatile bool _uiVisible = true;
        private bool _balloonShown;
        private Snapshot _lastSnap;

        public MainWindow()
        {
            Title = "NauTilus Monitor";
            LoadAppIcon();
            Width = 980;
            Height = 850;
            MinWidth = 900;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            ResizeMode = ResizeMode.CanMinimize;
            FontFamily = Theme.Font;
            UseLayoutRounding = true;
            TextOptions.SetTextFormattingMode(this, TextFormattingMode.Display);

            var root = new Border
            {
                CornerRadius = new CornerRadius(14),
                Background = Theme.WindowBg,
                BorderBrush = Theme.CardBorder,
                BorderThickness = new Thickness(1)
            };

            var grid = new Grid();
            grid.RowDefinitions.Add(Row(GridLength.Auto));                      // barra de titulo
            grid.RowDefinitions.Add(Row(GridLength.Auto));                      // encabezado
            grid.RowDefinitions.Add(Row(new GridLength(1, GridUnitType.Star))); // contenido
            grid.RowDefinitions.Add(Row(GridLength.Auto));                      // pie

            var title = BuildTitleBar();
            var hero = BuildHero();            Grid.SetRow(hero, 1);
            var content = BuildContent();      Grid.SetRow(content, 2);
            var foot = BuildFooter();          Grid.SetRow(foot, 3);

            grid.Children.Add(title);
            grid.Children.Add(hero);
            grid.Children.Add(content);
            grid.Children.Add(foot);

            root.Child = grid;
            Content = root;

            if (!Program.DemoMode) SetupTray();

            Loaded += delegate { Start(); };
            Closed += delegate { Cleanup(); };
        }

        private void Cleanup()
        {
            _run = false;
            _logger.Stop();
            if (_tray != null) { try { _tray.Visible = false; _tray.Dispose(); } catch { } _tray = null; }
        }

        // ---------------------------------------------------------------
        //  Arranque / bucle de muestreo
        // ---------------------------------------------------------------
        private void Start()
        {
            if (Program.DemoMode) { StartDemo(); return; }

            _worker = new Thread(delegate()
            {
                StaticInfo info = null;
                try { info = StaticInfo.GetOnce(); } catch { }
                if (info == null) info = new StaticInfo();
                Dispatcher.BeginInvoke((Action)delegate { ApplyStatic(info); });
                _info = info;

                _sampler = new Sampler();
                try { _sampler.Warmup(); } catch { }

                // Sensores avanzados automaticos: el proceso normalmente ya viene
                // elevado (la app se relanza como admin al elegir idioma). Se intenta
                // siempre; si no hay driver/DLL o no hay permisos, cae a modo Lite.
                Dispatcher.BeginInvoke((Action)delegate { if (!AdvancedSensors.Enabled) _advChipTx.Text = Loc.T("adv.enabling"); });
                try { AdvancedSensors.Enable(); } catch { }
                Dispatcher.BeginInvoke((Action)delegate { UpdateAdvChip(); });

                Thread.Sleep(700); // deja transcurrir tiempo para el primer delta de CPU

                while (_run)
                {
                    Snapshot snap = null;
                    try { snap = _sampler.Sample(); } catch { }
                    if (snap != null)
                    {
                        _lastSnap = snap;
                        if (_logger.Active) _logger.Write(snap);    // sigue grabando aunque este en segundo plano
                        if (_uiVisible)
                        {
                            Snapshot s = snap;
                            Dispatcher.BeginInvoke((Action)delegate { UpdateUi(s); });
                        }
                    }
                    Thread.Sleep(1000);
                }
            });
            _worker.IsBackground = true;
            _worker.Start();
        }

        private void StartDemo()
        {
            var info = new StaticInfo();
            info.CpuName = "AMD Ryzen 5 3600 6-Core Processor";
            info.Cores = 6; info.Threads = 12; info.BaseClockGhz = 3.6;
            info.GpuName = "AMD Radeon RX 5700 XT";
            info.RamTotalGB = 16;
            info.Os = "Windows 11 Pro  ·  build 26100";
            info.Board = "ASUS TUF GAMING B550-PLUS";
            ApplyStatic(info);

            var s = new Snapshot();
            s.CpuTotal = 37; s.CpuClockMhz = 4025;
            s.CpuCores = new double[] { 62, 18, 41, 12, 55, 9, 33, 21, 47, 15, 28, 11 };
            s.RamTotalGB = 16; s.RamUsedGB = 9.9; s.RamUsedPct = 62;
            s.GpuLoadPct = 45; s.DiskActivePct = 12; s.DiskReadMBs = 46.2; s.DiskWriteMBs = 7.8;
            s.NetDownKBs = 860; s.NetUpKBs = 120; s.ProcCount = 214;
            s.Uptime = new TimeSpan(0, 3, 42, 0);
            s.Temps.Add(new TempReading { Name = "CPU · Core (Tctl)", Celsius = 58 });
            s.Temps.Add(new TempReading { Name = "GPU · Core", Celsius = 62 });
            s.Temps.Add(new TempReading { Name = "Almacenamiento · NVMe", Celsius = 41 });
            s.Temps.Add(new TempReading { Name = "Placa base", Celsius = 39 });
            s.Fans.Add(new FanReading { Name = "CPU", Rpm = 1180 });
            s.Fans.Add(new FanReading { Name = "Chasis #1", Rpm = 900 });
            s.Fans.Add(new FanReading { Name = "GPU", Rpm = 1420 });

            AdvancedSensors.Status = "ok";
            UpdateUi(s);
            MarkAdvOn();
        }

        // ---------------------------------------------------------------
        //  Barra de titulo
        // ---------------------------------------------------------------
        private UIElement BuildTitleBar()
        {
            var bar = new Border { Background = Brushes.Transparent, Height = 48, Padding = new Thickness(16, 0, 10, 0) };
            bar.MouseLeftButtonDown += delegate(object s, MouseButtonEventArgs e) { if (e.ButtonState == MouseButtonState.Pressed) try { DragMove(); } catch { } };

            var g = new Grid();
            g.ColumnDefinitions.Add(Col(new GridLength(1, GridUnitType.Star)));
            g.ColumnDefinitions.Add(Col(GridLength.Auto));

            var left = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            var mark = new Border { Width = 26, Height = 26, CornerRadius = new CornerRadius(8), Background = Theme.AccentGradient(), VerticalAlignment = VerticalAlignment.Center };
            mark.Child = new TextBlock { Text = "N", Foreground = Theme.AccentText, FontWeight = FontWeights.Bold, FontSize = 15, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            left.Children.Add(mark);
            var name = new TextBlock { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(10, 0, 0, 0) };
            name.Inlines.Add(new Run("NauTilus ") { Foreground = Theme.TitleText, FontWeight = FontWeights.SemiBold, FontSize = 15 });
            name.Inlines.Add(new Run("Motion") { Foreground = Theme.AccentBrush, FontWeight = FontWeights.SemiBold, FontSize = 15 });
            left.Children.Add(name);
            var pill = new Border { Background = Theme.Card, CornerRadius = new CornerRadius(6), Padding = new Thickness(8, 2, 8, 2), Margin = new Thickness(10, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center, BorderBrush = Theme.CardBorder, BorderThickness = new Thickness(1) };
            pill.Child = new TextBlock { Text = "MONITOR", Foreground = Theme.Muted, FontSize = 10, FontWeight = FontWeights.SemiBold };
            left.Children.Add(pill);
            Grid.SetColumn(left, 0);
            g.Children.Add(left);

            var btns = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center };
            btns.Children.Add(WinButton("—", delegate { if (_tray != null) HideToTray(); else WindowState = WindowState.Minimized; }, false));
            btns.Children.Add(WinButton("✕", delegate { Close(); }, true));
            Grid.SetColumn(btns, 1);
            g.Children.Add(btns);

            bar.Child = g;
            return bar;
        }

        private UIElement WinButton(string glyph, Action onClick, bool danger)
        {
            var b = new Border { Width = 34, Height = 30, CornerRadius = new CornerRadius(7), Background = Brushes.Transparent, Margin = new Thickness(2, 0, 0, 0), Cursor = Cursors.Hand };
            var tb = new TextBlock { Text = glyph, Foreground = Theme.Muted, FontSize = 13, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            b.Child = tb;
            b.MouseEnter += delegate { b.Background = danger ? Theme.Danger : Theme.Card; tb.Foreground = Brushes.White; };
            b.MouseLeave += delegate { b.Background = Brushes.Transparent; tb.Foreground = Theme.Muted; };
            b.MouseLeftButtonDown += delegate(object s, MouseButtonEventArgs e) { e.Handled = true; };
            b.MouseLeftButtonUp += delegate { onClick(); };
            return b;
        }

        private UIElement BuildHero()
        {
            var sp = new StackPanel { Margin = new Thickness(24, 4, 24, 10) };
            sp.Children.Add(new TextBlock { Text = Loc.T("hero.title"), Foreground = Theme.TitleText, FontSize = 24, FontWeight = FontWeights.Bold });
            sp.Children.Add(new TextBlock { Text = Loc.T("hero.sub"), Foreground = Theme.Muted, FontSize = 13, Margin = new Thickness(0, 6, 0, 0), TextWrapping = TextWrapping.Wrap });
            return sp;
        }

        // ---------------------------------------------------------------
        //  Contenido
        // ---------------------------------------------------------------
        private UIElement BuildContent()
        {
            var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Margin = new Thickness(24, 2, 24, 8) };
            var col = new StackPanel();

            col.Children.Add(BuildAdvBar());
            col.Children.Add(BuildGaugeCard());

            // Rejilla de dos columnas para las fichas
            var g = new Grid { Margin = new Thickness(0, 4, 0, 0) };
            g.ColumnDefinitions.Add(Col(new GridLength(1, GridUnitType.Star)));
            g.ColumnDefinitions.Add(Col(new GridLength(1, GridUnitType.Star)));

            var leftCol = new StackPanel { Margin = new Thickness(0, 0, 6, 0) };
            leftCol.Children.Add(BuildCpuCard());
            leftCol.Children.Add(BuildDiskCard());
            leftCol.Children.Add(BuildNetCard());
            Grid.SetColumn(leftCol, 0);
            g.Children.Add(leftCol);

            var rightCol = new StackPanel { Margin = new Thickness(6, 0, 0, 0) };
            rightCol.Children.Add(BuildTempsCard());
            rightCol.Children.Add(BuildFansCard());
            rightCol.Children.Add(BuildSystemCard());
            Grid.SetColumn(rightCol, 1);
            g.Children.Add(rightCol);

            col.Children.Add(g);
            scroll.Content = col;
            return scroll;
        }

        private UIElement BuildAdvBar()
        {
            var outer = new StackPanel { Margin = new Thickness(0, 0, 0, 12) };
            var chips = new StackPanel { Orientation = Orientation.Horizontal };

            _advChip = ChipBorder(Loc.T("adv.enable"), out _advChipTx);
            _advChip.MouseEnter += delegate { if (!AdvancedSensors.Enabled) _advChip.BorderBrush = Theme.AccentBrush; };
            _advChip.MouseLeave += delegate { if (!AdvancedSensors.Enabled) _advChip.BorderBrush = Theme.CardBorder; };
            _advChip.MouseLeftButtonUp += delegate { OnAdvClick(); };
            chips.Children.Add(_advChip);

            _recChip = ChipBorder(Loc.T("rec.start"), out _recChipTx);
            _recChip.Margin = new Thickness(8, 0, 0, 0);
            _recChip.MouseEnter += delegate { if (!_logger.Active) _recChip.BorderBrush = Theme.AccentBrush; };
            _recChip.MouseLeave += delegate { if (!_logger.Active) _recChip.BorderBrush = Theme.CardBorder; };
            _recChip.MouseLeftButtonUp += delegate { OnRecClick(); };
            chips.Children.Add(_recChip);

            TextBlock openTx;
            _openChip = ChipBorder(Loc.T("rec.openfolder"), out openTx);
            _openChip.Margin = new Thickness(8, 0, 0, 0);
            _openChip.Visibility = Visibility.Collapsed;
            _openChip.MouseEnter += delegate { _openChip.BorderBrush = Theme.AccentBrush; };
            _openChip.MouseLeave += delegate { _openChip.BorderBrush = Theme.CardBorder; };
            _openChip.MouseLeftButtonUp += delegate { OpenLogsFolder(); };
            chips.Children.Add(_openChip);

            outer.Children.Add(chips);

            _advHint = new TextBlock { Text = Loc.T("adv.hint"), Foreground = Theme.Muted, FontSize = 11, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(2, 8, 0, 0) };
            outer.Children.Add(_advHint);

            _recStatus = new TextBlock { Text = "", Foreground = Theme.Muted, FontSize = 11, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(2, 4, 0, 0) };
            outer.Children.Add(_recStatus);

            return outer;
        }

        private Border ChipBorder(string text, out TextBlock tx)
        {
            var b = new Border { Background = Theme.Card, BorderBrush = Theme.CardBorder, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(8), Padding = new Thickness(14, 8, 14, 8), Cursor = Cursors.Hand, VerticalAlignment = VerticalAlignment.Center };
            tx = new TextBlock { Text = text, Foreground = Theme.BodyText, FontSize = 13, FontWeight = FontWeights.SemiBold };
            b.Child = tx;
            return b;
        }

        private UIElement BuildGaugeCard()
        {
            var card = Card();
            card.Margin = new Thickness(0, 0, 0, 12);
            var ug = new UniformGrid { Columns = 4, Rows = 1 };
            _gCpu = new Gauge(Loc.T("g.cpu"), 128);
            _gGpu = new Gauge(Loc.T("g.gpu"), 128);
            _gRam = new Gauge(Loc.T("g.ram"), 128);
            _gDisk = new Gauge(Loc.T("g.disk"), 128);
            ug.Children.Add(Wrap(_gCpu));
            ug.Children.Add(Wrap(_gGpu));
            ug.Children.Add(Wrap(_gRam));
            ug.Children.Add(Wrap(_gDisk));
            ((StackPanel)card.Child).Children.Add(ug);
            return card;
        }

        private UIElement Wrap(UIElement child)
        {
            var b = new Border { Padding = new Thickness(4, 6, 4, 4) };
            var host = new Grid();
            host.Children.Add(child);
            b.Child = host;
            return b;
        }

        private UIElement BuildCpuCard()
        {
            var card = Card();
            var sp = (StackPanel)card.Child;
            sp.Children.Add(Header(Loc.T("card.cpu")));

            _cpuNameTx = new TextBlock { Text = "--", Foreground = Theme.TitleText, FontSize = 14, FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 2, 0, 8) };
            sp.Children.Add(_cpuNameTx);

            var row = new Grid();
            row.ColumnDefinitions.Add(Col(GridLength.Auto));
            row.ColumnDefinitions.Add(Col(new GridLength(1, GridUnitType.Star)));
            var clockBox = new StackPanel();
            _clockTx = new TextBlock { Text = "-- GHz", Foreground = Theme.AccentBrush, FontSize = 26, FontWeight = FontWeights.Bold };
            clockBox.Children.Add(_clockTx);
            clockBox.Children.Add(new TextBlock { Text = Loc.T("lbl.clock"), Foreground = Theme.Muted, FontSize = 11 });
            Grid.SetColumn(clockBox, 0);
            row.Children.Add(clockBox);

            _coresTx = new TextBlock { Text = "", Foreground = Theme.BodyText, FontSize = 12, VerticalAlignment = VerticalAlignment.Bottom, HorizontalAlignment = HorizontalAlignment.Right, TextAlignment = TextAlignment.Right, TextWrapping = TextWrapping.Wrap };
            Grid.SetColumn(_coresTx, 1);
            row.Children.Add(_coresTx);
            sp.Children.Add(row);

            sp.Children.Add(new TextBlock { Text = Loc.T("card.percore"), Foreground = Theme.Muted, FontSize = 10, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 12, 0, 6) });
            _coreHost = new UniformGrid { Columns = 2 };
            sp.Children.Add(_coreHost);
            return card;
        }

        private UIElement BuildTempsCard()
        {
            var card = Card();
            var sp = (StackPanel)card.Child;
            sp.Children.Add(Header(Loc.T("card.temps")));
            _tempsHost = new StackPanel { Margin = new Thickness(0, 2, 0, 0) };
            sp.Children.Add(_tempsHost);
            return card;
        }

        private UIElement BuildFansCard()
        {
            var card = Card();
            var sp = (StackPanel)card.Child;
            sp.Children.Add(Header(Loc.T("card.fans")));
            _fansHost = new StackPanel { Margin = new Thickness(0, 2, 0, 0) };
            sp.Children.Add(_fansHost);
            return card;
        }

        private UIElement BuildDiskCard()
        {
            var card = Card();
            var sp = (StackPanel)card.Child;
            sp.Children.Add(Header(Loc.T("card.disk")));
            _diskBar = new BarMeter(10) { Margin = new Thickness(0, 4, 0, 8) };
            sp.Children.Add(_diskBar);
            _diskRWTx = new TextBlock { Text = "--", Foreground = Theme.BodyText, FontSize = 13, FontFamily = Theme.Mono };
            sp.Children.Add(_diskRWTx);
            return card;
        }

        private UIElement BuildNetCard()
        {
            var card = Card();
            var sp = (StackPanel)card.Child;
            sp.Children.Add(Header(Loc.T("card.net")));
            _netDownTx = KvValue();
            _netUpTx = KvValue();
            sp.Children.Add(Kv(Loc.T("net.down"), _netDownTx));
            sp.Children.Add(Kv(Loc.T("net.up"), _netUpTx));
            return card;
        }

        private UIElement BuildSystemCard()
        {
            var card = Card();
            var sp = (StackPanel)card.Child;
            sp.Children.Add(Header(Loc.T("card.system")));
            _osTx = KvValue(); _boardTx = KvValue(); _uptimeTx = KvValue(); _procTx = KvValue();
            sp.Children.Add(Kv(Loc.T("lbl.os"), _osTx));
            sp.Children.Add(Kv(Loc.T("lbl.board"), _boardTx));
            sp.Children.Add(Kv(Loc.T("lbl.uptime"), _uptimeTx));
            sp.Children.Add(Kv(Loc.T("lbl.processes"), _procTx));
            return card;
        }

        // ---------------------------------------------------------------
        //  Actualizacion en vivo
        // ---------------------------------------------------------------
        private void ApplyStatic(StaticInfo info)
        {
            _info = info;
            _cpuNameTx.Text = string.IsNullOrEmpty(info.CpuName) ? "--" : info.CpuName;

            string cores = "";
            if (info.Cores > 0) cores += info.Cores + " " + Loc.T("lbl.cores");
            if (info.Threads > 0) cores += (cores.Length > 0 ? " · " : "") + info.Threads + " " + Loc.T("lbl.threads");
            if (info.BaseClockGhz > 0) cores += (cores.Length > 0 ? "\n" : "") + info.BaseClockGhz.ToString("0.0", Inv) + " GHz " + Loc.T("lbl.base");
            _coresTx.Text = cores;

            _osTx.Text = string.IsNullOrEmpty(info.Os) ? "--" : info.Os;
            _boardTx.Text = string.IsNullOrEmpty(info.Board) ? "--" : info.Board;

            // Construye las barras por-nucleo
            int n = info.Threads > 0 ? info.Threads : Environment.ProcessorCount;
            if (n < 1) n = 1;
            _coreHost.Children.Clear();
            _coreBars = new BarMeter[n];
            _corePcts = new TextBlock[n];
            for (int i = 0; i < n; i++)
            {
                var cell = new Grid { Margin = new Thickness(0, 0, 10, 6) };
                cell.ColumnDefinitions.Add(Col(new GridLength(26)));
                cell.ColumnDefinitions.Add(Col(new GridLength(1, GridUnitType.Star)));
                cell.ColumnDefinitions.Add(Col(new GridLength(34)));
                var lbl = new TextBlock { Text = (i).ToString(), Foreground = Theme.Muted, FontSize = 10, FontFamily = Theme.Mono, VerticalAlignment = VerticalAlignment.Center };
                Grid.SetColumn(lbl, 0); cell.Children.Add(lbl);
                var bar = new BarMeter(8) { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 6, 0) };
                Grid.SetColumn(bar, 1); cell.Children.Add(bar);
                var pct = new TextBlock { Text = "--", Foreground = Theme.BodyText, FontSize = 10, FontFamily = Theme.Mono, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center };
                Grid.SetColumn(pct, 2); cell.Children.Add(pct);
                _coreBars[i] = bar; _corePcts[i] = pct;
                _coreHost.Children.Add(cell);
            }
        }

        private void UpdateUi(Snapshot s)
        {
            // CPU
            string clockStr = s.CpuClockMhz > 0 ? (s.CpuClockMhz / 1000.0).ToString("0.00", Inv) + " GHz" : "-- GHz";
            _gCpu.SetValue(s.CpuTotal, F0(s.CpuTotal) + "%", clockStr);
            _clockTx.Text = clockStr;

            // GPU
            if (s.GpuLoadPct >= 0)
                _gGpu.SetValue(s.GpuLoadPct, F0(s.GpuLoadPct) + "%", GpuTempSmall(s));
            else
                _gGpu.SetUnknown(Loc.T("na"));

            // RAM
            _gRam.SetValue(s.RamUsedPct, F0(s.RamUsedPct) + "%",
                F1(s.RamUsedGB) + " / " + F1(s.RamTotalGB) + " GB");

            // Disco (gauge)
            if (s.DiskActivePct >= 0)
                _gDisk.SetValue(s.DiskActivePct, F0(s.DiskActivePct) + "%", "");
            else
                _gDisk.SetUnknown(Loc.T("na"));

            // Por-nucleo
            if (s.CpuCores != null && _coreBars != null)
            {
                int m = Math.Min(s.CpuCores.Length, _coreBars.Length);
                for (int i = 0; i < m; i++)
                {
                    _coreBars[i].SetValue(s.CpuCores[i], Theme.AccentBrush);
                    _corePcts[i].Text = F0(s.CpuCores[i]) + "%";
                }
            }

            // Temperaturas
            _tempsHost.Children.Clear();
            if (s.Temps.Count == 0)
                _tempsHost.Children.Add(NoneNote(Loc.T("temps.none")));
            else
                foreach (TempReading t in s.Temps)
                    _tempsHost.Children.Add(TempRow(t));

            // Ventiladores
            _fansHost.Children.Clear();
            if (s.Fans.Count == 0)
                _fansHost.Children.Add(NoneNote(Loc.T("fans.none")));
            else
                foreach (FanReading f in s.Fans)
                    _fansHost.Children.Add(FanRow(f));

            // Disco (ficha)
            if (s.DiskActivePct >= 0)
            {
                _diskBar.SetValue(s.DiskActivePct, Theme.AccentBrush);
                _diskRWTx.Text = Loc.T("disk.read") + " " + Mbs(s.DiskReadMBs) + "   ·   " + Loc.T("disk.write") + " " + Mbs(s.DiskWriteMBs);
                _diskRWTx.Foreground = Theme.BodyText;
            }
            else
            {
                _diskBar.SetValue(0, Theme.Track);
                _diskRWTx.Text = Loc.T("na");
                _diskRWTx.Foreground = Theme.Muted;
            }

            // Red
            _netDownTx.Text = Rate(s.NetDownKBs);
            _netUpTx.Text = Rate(s.NetUpKBs);

            // Sistema
            _uptimeTx.Text = Uptime(s.Uptime);
            _procTx.Text = s.ProcCount > 0 ? s.ProcCount.ToString() : "--";
        }

        private string GpuTempSmall(Snapshot s)
        {
            foreach (TempReading t in s.Temps)
                if (t.Name != null && t.Name.IndexOf("GPU", StringComparison.OrdinalIgnoreCase) >= 0)
                    return F0(t.Celsius) + "°C";
            return "";
        }

        // ---------------------------------------------------------------
        //  Sensores avanzados
        // ---------------------------------------------------------------
        private void OnAdvClick()
        {
            if (Program.DemoMode || AdvancedSensors.Enabled) return;

            if (!AdvancedSensors.DllPresent())
            {
                _advHint.Text = Loc.T("adv.dllmissing");
                _advHint.Foreground = Theme.AccentBrush;
                return;
            }

            if (!Program.IsElevated())
            {
                bool ok = Program.RelaunchElevated(Program.LangCode());
                if (ok) { _run = false; Application.Current.Shutdown(); }
                else { _advHint.Text = Loc.T("adv.error"); _advHint.Foreground = Theme.AccentBrush; }
                return;
            }

            // Ya elevado: activa directamente
            _advChipTx.Text = Loc.T("adv.enabling");
            var th = new Thread(delegate()
            {
                bool ok = false;
                try { ok = AdvancedSensors.Enable(); } catch { }
                Dispatcher.BeginInvoke((Action)delegate { UpdateAdvChip(); });
            });
            th.IsBackground = true; th.Start();
        }

        private void UpdateAdvChip()
        {
            if (AdvancedSensors.Enabled) { MarkAdvOn(); return; }
            if (AdvancedSensors.Status == "dll_missing") { _advHint.Text = Loc.T("adv.dllmissing"); _advHint.Foreground = Theme.AccentBrush; }
            else if (AdvancedSensors.Status == "error") { _advHint.Text = Loc.T("adv.error"); _advHint.Foreground = Theme.AccentBrush; }
            _advChipTx.Text = Loc.T("adv.enable");
        }

        private void MarkAdvOn()
        {
            _advChip.Background = Theme.AccentBrush;
            _advChip.BorderBrush = Theme.AccentBrush;
            _advChipTx.Text = Loc.T("adv.on");
            _advChipTx.Foreground = Theme.AccentText;
            _advChip.Cursor = Cursors.Arrow;
            _advHint.Text = "";
        }

        // ---------------------------------------------------------------
        //  Grabacion de temperaturas (CSV)
        // ---------------------------------------------------------------
        private void OnRecClick()
        {
            if (Program.DemoMode) return;
            if (_logger.Active) StopRec(); else StartRec();
        }

        private void StartRec()
        {
            if (!_logger.Start(null))
            {
                _recStatus.Text = Loc.T("rec.error");
                _recStatus.Foreground = Theme.AccentBrush;
                return;
            }
            _recChip.Background = Theme.AccentBrush;
            _recChip.BorderBrush = Theme.AccentBrush;
            _recChipTx.Text = Loc.T("rec.on");
            _recChipTx.Foreground = Theme.AccentText;
            _recStatus.Text = Loc.T("rec.savingto") + " " + _logger.Path;
            _recStatus.Foreground = Theme.Muted;
            _openChip.Visibility = Visibility.Visible;
        }

        private void StopRec()
        {
            if (!_logger.Active) return;
            string path = _logger.Path;
            _logger.Stop();
            _recChip.Background = Theme.Card;
            _recChip.BorderBrush = Theme.CardBorder;
            _recChipTx.Text = Loc.T("rec.start");
            _recChipTx.Foreground = Theme.BodyText;
            if (!string.IsNullOrEmpty(path))
            {
                _recStatus.Text = Loc.T("rec.savedto") + " " + path;
                _recStatus.Foreground = Theme.Muted;
            }
        }

        private void OpenLogsFolder()
        {
            try
            {
                string path = _logger.Path;
                if (!string.IsNullOrEmpty(path) && File.Exists(path))
                    Process.Start("explorer.exe", "/select,\"" + path + "\"");
                else
                {
                    string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "NauTilus Monitor");
                    Directory.CreateDirectory(dir);
                    Process.Start(dir);
                }
            }
            catch { }
        }

        // ---------------------------------------------------------------
        //  Segundo plano (bandeja del sistema)
        // ---------------------------------------------------------------
        private void SetupTray()
        {
            try
            {
                _tray = new System.Windows.Forms.NotifyIcon();
                _tray.Text = "NauTilus Monitor";
                _tray.Icon = LoadTrayIcon();
                var menu = new System.Windows.Forms.ContextMenuStrip();
                var mShow = new System.Windows.Forms.ToolStripMenuItem(Loc.T("tray.show"));
                mShow.Click += delegate { ShowFromTray(); };
                var mExit = new System.Windows.Forms.ToolStripMenuItem(Loc.T("tray.exit"));
                mExit.Click += delegate { Close(); };
                menu.Items.Add(mShow);
                menu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
                menu.Items.Add(mExit);
                _tray.ContextMenuStrip = menu;
                _tray.DoubleClick += delegate { ShowFromTray(); };
                _tray.Visible = true;
            }
            catch { _tray = null; }
        }

        private System.Drawing.Icon LoadTrayIcon()
        {
            try
            {
                var asm = Assembly.GetExecutingAssembly();
                using (var st = asm.GetManifestResourceStream("NautilusMonitor.appicon.ico"))
                    if (st != null) return new System.Drawing.Icon(st);
            }
            catch { }
            return System.Drawing.SystemIcons.Application;
        }

        private void HideToTray()
        {
            _uiVisible = false;
            Hide();
            if (!_balloonShown && _tray != null)
            {
                _balloonShown = true;
                try { _tray.ShowBalloonTip(3000, "NauTilus Monitor", Loc.T("tray.background"), System.Windows.Forms.ToolTipIcon.Info); }
                catch { }
            }
        }

        private void ShowFromTray()
        {
            Show();
            WindowState = WindowState.Normal;
            Activate();
            Topmost = true; Topmost = false; // lo trae al frente
            _uiVisible = true;
            if (_lastSnap != null) UpdateUi(_lastSnap);
        }

        // ---------------------------------------------------------------
        //  Piezas de UI reutilizables
        // ---------------------------------------------------------------
        private Border Card()
        {
            var card = new Border { Background = Theme.Card, BorderBrush = Theme.CardBorder, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(12), Padding = new Thickness(16, 14, 16, 14), Margin = new Thickness(0, 0, 0, 12) };
            card.Child = new StackPanel();
            return card;
        }

        private TextBlock Header(string text)
        {
            return new TextBlock { Text = text, Foreground = Theme.AccentBrush, FontSize = 11, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 0, 0, 6) };
        }

        private UIElement Kv(string key, TextBlock value)
        {
            var g = new Grid { Margin = new Thickness(0, 5, 0, 0) };
            g.ColumnDefinitions.Add(Col(GridLength.Auto));
            g.ColumnDefinitions.Add(Col(new GridLength(1, GridUnitType.Star)));
            var k = new TextBlock { Text = key, Foreground = Theme.Muted, FontSize = 12, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(k, 0);
            value.HorizontalAlignment = HorizontalAlignment.Right;
            value.TextAlignment = TextAlignment.Right;
            Grid.SetColumn(value, 1);
            g.Children.Add(k);
            g.Children.Add(value);
            return g;
        }

        private TextBlock KvValue()
        {
            return new TextBlock { Text = "--", Foreground = Theme.TitleText, FontSize = 13, FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap };
        }

        private UIElement NoneNote(string text)
        {
            return new TextBlock { Text = text, Foreground = Theme.Muted, FontSize = 12, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 2, 0, 2) };
        }

        private UIElement TempRow(TempReading t)
        {
            var g = new Grid { Margin = new Thickness(0, 5, 0, 0) };
            g.ColumnDefinitions.Add(Col(new GridLength(1, GridUnitType.Star)));
            g.ColumnDefinitions.Add(Col(GridLength.Auto));
            var name = new TextBlock { Text = t.Name, Foreground = Theme.BodyText, FontSize = 12, VerticalAlignment = VerticalAlignment.Center, TextWrapping = TextWrapping.Wrap };
            Grid.SetColumn(name, 0); g.Children.Add(name);
            var val = new TextBlock { Text = F0(t.Celsius) + " °C", Foreground = TempColor(t.Celsius), FontSize = 14, FontWeight = FontWeights.Bold, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(val, 1); g.Children.Add(val);
            return g;
        }

        private UIElement FanRow(FanReading f)
        {
            var g = new Grid { Margin = new Thickness(0, 5, 0, 0) };
            g.ColumnDefinitions.Add(Col(new GridLength(1, GridUnitType.Star)));
            g.ColumnDefinitions.Add(Col(GridLength.Auto));
            var name = new TextBlock { Text = f.Name, Foreground = Theme.BodyText, FontSize = 12, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(name, 0); g.Children.Add(name);
            var val = new TextBlock { Text = f.Rpm.ToString() + " RPM", Foreground = Theme.TitleText, FontSize = 14, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(val, 1); g.Children.Add(val);
            return g;
        }

        private UIElement BuildFooter()
        {
            var g = new Grid { Margin = new Thickness(24, 4, 24, 16) };
            g.ColumnDefinitions.Add(Col(new GridLength(1, GridUnitType.Star)));
            g.ColumnDefinitions.Add(Col(GridLength.Auto));
            var left = new TextBlock { FontSize = 11, VerticalAlignment = VerticalAlignment.Center };
            left.Inlines.Add(new Run(Loc.T("footer.dev")) { Foreground = Theme.Footer });
            left.Inlines.Add(new Run("NauTilus Motion") { Foreground = Theme.BodyText, FontWeight = FontWeights.SemiBold });
            left.Inlines.Add(new Run(Loc.T("footer.oss")) { Foreground = Theme.Footer });
            Grid.SetColumn(left, 0);
            g.Children.Add(left);
            var link = new TextBlock { Text = "nautilusmotion.com", Foreground = Theme.AccentBrush, FontSize = 11, VerticalAlignment = VerticalAlignment.Center, Cursor = Cursors.Hand };
            link.MouseLeftButtonUp += delegate { try { Process.Start("https://nautilusmotion.com"); } catch { } };
            Grid.SetColumn(link, 1);
            g.Children.Add(link);
            return g;
        }

        // ---------------------------------------------------------------
        //  Formato / utilidades
        // ---------------------------------------------------------------
        private static string F0(double v) { return v.ToString("0", Inv); }
        private static string F1(double v) { return v.ToString("0.0", Inv); }

        private static string Mbs(double mbs)
        {
            if (mbs < 0) return "--";
            if (mbs >= 1) return mbs.ToString("0.0", Inv) + " MB/s";
            return (mbs * 1024).ToString("0", Inv) + " KB/s";
        }

        private static string Rate(double kbs)
        {
            if (kbs >= 1024) return (kbs / 1024.0).ToString("0.0", Inv) + " MB/s";
            return kbs.ToString("0", Inv) + " KB/s";
        }

        private static Brush TempColor(double c)
        {
            if (c < 60) return Theme.Cool;
            if (c < 80) return Theme.Warm;
            return Theme.Hot;
        }

        private static string Uptime(TimeSpan up)
        {
            if (up.TotalSeconds < 1) return "--";
            if (up.TotalDays >= 1) return (int)up.TotalDays + "d " + up.Hours + "h " + up.Minutes + "m";
            if (up.TotalHours >= 1) return up.Hours + "h " + up.Minutes + "m";
            return up.Minutes + "m";
        }

        private void LoadAppIcon()
        {
            try
            {
                var asm = System.Reflection.Assembly.GetExecutingAssembly();
                using (var st = asm.GetManifestResourceStream("NautilusMonitor.appicon.png"))
                    if (st != null)
                        Icon = BitmapFrame.Create(st, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
            }
            catch { }
        }

        private static RowDefinition Row(GridLength h) { return new RowDefinition { Height = h }; }
        private static ColumnDefinition Col(GridLength w) { return new ColumnDefinition { Width = w }; }
    }
}
