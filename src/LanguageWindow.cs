// NauTilus Monitor - Pantalla de seleccion de idioma (se muestra antes de la app)

using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace NautilusMotion.Monitor
{
    public class LanguageWindow : Window
    {
        public LanguageWindow()
        {
            Title = "NauTilus Monitor";
            LoadAppIcon();
            Width = 440;
            Height = 664;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            ResizeMode = ResizeMode.NoResize;
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
            root.MouseLeftButtonDown += delegate(object s, MouseButtonEventArgs e)
            {
                if (e.ButtonState == MouseButtonState.Pressed) try { DragMove(); } catch { }
            };

            var sp = new StackPanel { Margin = new Thickness(28, 0, 28, 24) };

            var top = new Grid { Height = 44, Margin = new Thickness(0, 4, 0, 0) };
            top.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            top.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var close = new Border { Width = 32, Height = 30, CornerRadius = new CornerRadius(7), Background = Brushes.Transparent, Cursor = Cursors.Hand, VerticalAlignment = VerticalAlignment.Center };
            var closeTx = new TextBlock { Text = "✕", Foreground = Theme.Muted, FontSize = 13, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            close.Child = closeTx;
            close.MouseEnter += delegate { close.Background = Theme.Danger; closeTx.Foreground = Brushes.White; };
            close.MouseLeave += delegate { close.Background = Brushes.Transparent; closeTx.Foreground = Theme.Muted; };
            close.MouseLeftButtonDown += delegate(object s, MouseButtonEventArgs e) { e.Handled = true; };
            close.MouseLeftButtonUp += delegate { Application.Current.Shutdown(); };
            Grid.SetColumn(close, 1);
            top.Children.Add(close);
            sp.Children.Add(top);

            var logo = new Border { Width = 84, Height = 84, CornerRadius = new CornerRadius(20), HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 6, 0, 0) };
            try
            {
                var asm = System.Reflection.Assembly.GetExecutingAssembly();
                using (var st = asm.GetManifestResourceStream("NautilusMonitor.appicon.png"))
                    if (st != null)
                        logo.Background = new ImageBrush(BitmapFrame.Create(st, BitmapCreateOptions.None, BitmapCacheOption.OnLoad));
            }
            catch { }
            sp.Children.Add(logo);

            var name = new TextBlock { HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 16, 0, 0) };
            name.Inlines.Add(new Run("NauTilus ") { Foreground = Theme.TitleText, FontWeight = FontWeights.Bold, FontSize = 24 });
            name.Inlines.Add(new Run("Monitor") { Foreground = Theme.AccentBrush, FontWeight = FontWeights.Bold, FontSize = 24 });
            sp.Children.Add(name);

            sp.Children.Add(new TextBlock
            {
                Text = "Choose your language · Selecciona tu idioma",
                Foreground = Theme.Muted,
                FontSize = 12,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 8, 0, 20)
            });

            for (int i = 0; i < Loc.Names.Length; i++)
            {
                Lang lang = (Lang)i;
                sp.Children.Add(LangButton(Loc.Names[i], Loc.SubNames[i], lang));
            }

            root.Child = sp;
            Content = root;
        }

        private UIElement LangButton(string native, string english, Lang lang)
        {
            var b = new Border
            {
                Background = Theme.Card,
                BorderBrush = Theme.CardBorder,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(18, 13, 18, 13),
                Margin = new Thickness(0, 0, 0, 10),
                Cursor = Cursors.Hand
            };
            var g = new Grid();
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var nat = new TextBlock { Text = native, Foreground = Theme.TitleText, FontSize = 16, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center };
            var eng = new TextBlock { Text = english, Foreground = Theme.Muted, FontSize = 12, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(nat, 0);
            Grid.SetColumn(eng, 1);
            g.Children.Add(nat);
            g.Children.Add(eng);
            b.Child = g;

            b.MouseEnter += delegate { b.BorderBrush = Theme.AccentBrush; b.Background = Theme.LogBg; };
            b.MouseLeave += delegate { b.BorderBrush = Theme.CardBorder; b.Background = Theme.Card; };
            b.MouseLeftButtonDown += delegate(object s, MouseButtonEventArgs e) { e.Handled = true; };
            b.MouseLeftButtonUp += delegate { Choose(lang); };
            return b;
        }

        private void Choose(Lang lang)
        {
            Loc.Current = lang;

            // Para leer temperaturas y ventiladores hacen falta los sensores
            // avanzados, que requieren administrador. Si aun no estamos elevados y
            // la DLL de LibreHardwareMonitor esta presente, relanzamos la app como
            // administrador (un UAC) y esa instancia ya arranca con todo activo.
            if (!Program.IsElevated() && AdvancedSensors.DllPresent())
            {
                if (Program.RelaunchElevated(Program.LangCode()))
                {
                    Application.Current.Shutdown();
                    return;
                }
                // El usuario cancelo el UAC: se continua en modo ligero.
            }

            var main = new MainWindow();
            main.Show();
            Close();
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
    }
}
