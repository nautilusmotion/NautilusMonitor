// NauTilus Monitor - Paleta y estilos de marca
// Estetica de nautilusmotion.com: negro y rojo, con muy pocos toques de blanco.

using System.Windows.Media;

namespace NautilusMotion.Monitor
{
    internal static class Theme
    {
        public static Color C(string hex)
        {
            return (Color)ColorConverter.ConvertFromString(hex);
        }

        private static SolidColorBrush S(string hex)
        {
            var b = new SolidColorBrush(C(hex));
            b.Freeze();
            return b;
        }

        // Acento de marca (rojo NauTilus)
        public static readonly Color AccentHi = C("#FF3742");
        public static readonly Color AccentLo = C("#B70D16");

        public static readonly Brush AccentBrush = S("#E11420");
        public static readonly Brush AccentText  = S("#FFFFFF"); // texto blanco sobre el acento rojo

        // Superficies (negro)
        public static readonly Brush WindowBg   = MakeWindowBg();
        public static readonly Brush Card        = S("#121214");
        public static readonly Brush CardBorder  = S("#242428");
        public static readonly Brush LogBg       = S("#070708");
        public static readonly Brush Track       = S("#232327"); // fondo de gauges y barras

        // Texto (blanco usado con moderacion)
        public static readonly Brush TitleText = S("#F3F3F5");
        public static readonly Brush BodyText  = S("#BBBBC2");
        public static readonly Brush Muted     = S("#7C7C84");
        public static readonly Brush Footer    = S("#5A5A61");

        // Estado
        public static readonly Brush Danger  = S("#E11420");

        // Escala de temperatura / carga (fresco -> tibio -> caliente)
        public static readonly Brush Cool = S("#DADDE2"); // normal (casi blanco)
        public static readonly Brush Warm = S("#E8A21C"); // ambar
        public static readonly Brush Hot  = S("#E11420"); // rojo de marca

        public static readonly FontFamily Font = new FontFamily("Segoe UI");
        public static readonly FontFamily Mono = new FontFamily("Consolas");

        public static Brush AccentGradient()
        {
            var b = new LinearGradientBrush(AccentHi, AccentLo, 90);
            b.Freeze();
            return b;
        }

        private static Brush MakeWindowBg()
        {
            var b = new LinearGradientBrush(C("#0A0A0C"), C("#050506"), 90);
            b.Freeze();
            return b;
        }
    }
}
