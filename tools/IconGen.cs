// Generador del icono de NauTilus Monitor.
// Dibuja un medidor radial (gauge) blanco con aguja sobre una teja roja de marca
// -el mismo lenguaje visual que los gauges de la app- en varios tamanos y los
// empaqueta en un .ico (entradas PNG). Tambien exporta un PNG de 256px.
//
// Uso:  IconGen.exe <salida.ico> <salida.png>

using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace IconGen
{
    public static class Program
    {
        [STAThread]
        public static void Main(string[] args)
        {
            string icoPath = args.Length > 0 ? args[0] : "appicon.ico";
            string pngPath = args.Length > 1 ? args[1] : "appicon.png";

            int[] sizes = new int[] { 256, 128, 64, 48, 32, 24, 16 };
            var pngs = new List<byte[]>();
            foreach (int s in sizes)
                pngs.Add(RenderPng(s));

            WriteIco(icoPath, sizes, pngs);
            File.WriteAllBytes(pngPath, pngs[0]);
            Console.WriteLine("Icono generado: " + icoPath);
        }

        private static Color C(string hex) { return (Color)ColorConverter.ConvertFromString(hex); }

        private static Point Polar(Point c, double r, double deg)
        {
            double a = deg * Math.PI / 180.0;
            return new Point(c.X + r * Math.Cos(a), c.Y + r * Math.Sin(a));
        }

        private static Geometry Arc(Point c, double r, double startDeg, double sweepDeg)
        {
            var g = new StreamGeometry();
            using (StreamGeometryContext ctx = g.Open())
            {
                Point p0 = Polar(c, r, startDeg);
                Point p1 = Polar(c, r, startDeg + sweepDeg);
                ctx.BeginFigure(p0, false, false);
                ctx.ArcTo(p1, new Size(r, r), 0, sweepDeg > 180, SweepDirection.Clockwise, true, false);
            }
            g.Freeze();
            return g;
        }

        private static byte[] RenderPng(int s)
        {
            var dv = new DrawingVisual();
            using (DrawingContext dc = dv.RenderOpen())
            {
                // Teja roja de marca (misma que el Optimizer, para que sean familia)
                double margin = s * 0.045;
                double radius = s * 0.225;
                var rect = new Rect(margin, margin, s - 2 * margin, s - 2 * margin);
                var grad = new LinearGradientBrush(C("#FF3742"), C("#B70D16"), new Point(0.15, 0), new Point(0.85, 1));
                dc.DrawRoundedRectangle(grad, null, rect, radius, radius);

                // Medidor radial (gauge) de 270 grados
                var center = new Point(s / 2.0, s * 0.545);
                double r = s * 0.300;
                double th = Math.Max(1.0, s * 0.088);
                double start = 135, sweepFull = 270, frac = 0.62;
                double sweepVal = sweepFull * frac;

                var track = new SolidColorBrush(Color.FromArgb(0x59, 0xFF, 0xFF, 0xFF)); track.Freeze();
                var white = Brushes.White;

                var penTrack = new Pen(track, th) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round }; penTrack.Freeze();
                var penVal = new Pen(white, th) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round }; penVal.Freeze();

                dc.DrawGeometry(null, penTrack, Arc(center, r, start, sweepFull));
                dc.DrawGeometry(null, penVal, Arc(center, r, start, sweepVal));

                // Aguja hacia el valor
                double needleLen = r * 0.94;
                Point tip = Polar(center, needleLen, start + sweepVal);
                var penNeedle = new Pen(white, Math.Max(1.0, s * 0.052)) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round }; penNeedle.Freeze();
                dc.DrawLine(penNeedle, center, tip);

                // Cubo central
                dc.DrawEllipse(white, null, center, s * 0.078, s * 0.078);
                dc.DrawEllipse(new SolidColorBrush(C("#B70D16")), null, center, s * 0.034, s * 0.034);
            }

            var rtb = new RenderTargetBitmap(s, s, 96, 96, PixelFormats.Pbgra32);
            rtb.Render(dv);
            var enc = new PngBitmapEncoder();
            enc.Frames.Add(BitmapFrame.Create(rtb));
            using (var ms = new MemoryStream())
            {
                enc.Save(ms);
                return ms.ToArray();
            }
        }

        private static void WriteIco(string path, int[] sizes, List<byte[]> pngs)
        {
            using (var fs = new FileStream(path, FileMode.Create))
            using (var bw = new BinaryWriter(fs))
            {
                bw.Write((short)0);            // reservado
                bw.Write((short)1);            // tipo = icono
                bw.Write((short)sizes.Length); // numero de imagenes

                int offset = 6 + 16 * sizes.Length;
                for (int i = 0; i < sizes.Length; i++)
                {
                    int s = sizes[i];
                    bw.Write((byte)(s >= 256 ? 0 : s));
                    bw.Write((byte)(s >= 256 ? 0 : s));
                    bw.Write((byte)0);
                    bw.Write((byte)0);
                    bw.Write((short)1);
                    bw.Write((short)32);
                    bw.Write(pngs[i].Length);
                    bw.Write(offset);
                    offset += pngs[i].Length;
                }
                foreach (byte[] p in pngs)
                    bw.Write(p);
            }
        }
    }
}
