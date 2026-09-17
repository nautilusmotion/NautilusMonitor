// NauTilus Monitor - Gráfica de líneas en vivo (historial), WPF nativo sin librerías.
// Mantiene un buffer circular por serie y dibuja una polilínea por cada una,
// con rejilla y etiquetas de eje (0/25/50/75/100).

using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace NautilusMotion.Monitor
{
    public class LineChart : Border
    {
        private readonly int _cap;         // nº de muestras visibles (ancho temporal)
        private readonly double _max;      // valor máximo del eje (100 para %)
        private readonly int _series;
        private readonly double[][] _buf;  // [serie][cap]; NaN = sin dato
        private readonly Canvas _canvas;
        private readonly Polyline[] _poly;
        private readonly Line[] _grid;
        private readonly TextBlock[] _axis; // etiquetas 0/25/50/75/100
        private readonly double[] _axisVals = { 100, 75, 50, 25, 0 };
        private const double PadL = 30;     // hueco izquierdo para las cifras del eje

        public LineChart(int capacity, double max, Brush[] colors, double height)
        {
            _cap = capacity < 4 ? 4 : capacity;
            _max = max <= 0 ? 100 : max;
            _series = colors.Length;

            Height = height;
            Background = Theme.LogBg;
            BorderBrush = Theme.CardBorder;
            BorderThickness = new Thickness(1);
            CornerRadius = new CornerRadius(8);
            SnapsToDevicePixels = true;
            ClipToBounds = true;

            _buf = new double[_series][];
            for (int s = 0; s < _series; s++)
            {
                _buf[s] = new double[_cap];
                for (int i = 0; i < _cap; i++) _buf[s][i] = double.NaN;
            }

            _canvas = new Canvas();

            _grid = new Line[3];
            for (int i = 0; i < 3; i++)
            {
                var ln = new Line { Stroke = Theme.Track, StrokeThickness = 1, SnapsToDevicePixels = true };
                _grid[i] = ln;
                _canvas.Children.Add(ln);
            }

            _poly = new Polyline[_series];
            for (int s = 0; s < _series; s++)
            {
                var p = new Polyline
                {
                    Stroke = colors[s],
                    StrokeThickness = 2,
                    StrokeLineJoin = PenLineJoin.Round,
                    StrokeStartLineCap = PenLineCap.Round,
                    StrokeEndLineCap = PenLineCap.Round
                };
                _poly[s] = p;
                _canvas.Children.Add(p);
            }

            // Etiquetas del eje Y (encima de las líneas)
            _axis = new TextBlock[_axisVals.Length];
            for (int i = 0; i < _axisVals.Length; i++)
            {
                var t = new TextBlock { Text = ((int)_axisVals[i]).ToString(), Foreground = Theme.Muted, FontSize = 10, FontFamily = Theme.Mono };
                _axis[i] = t;
                _canvas.Children.Add(t);
            }

            Child = _canvas;
            SizeChanged += delegate { Redraw(); };
        }

        // Inserta una nueva muestra (un valor por serie) y desplaza el historial.
        public void Push(double[] values)
        {
            for (int s = 0; s < _series; s++)
            {
                Array.Copy(_buf[s], 1, _buf[s], 0, _cap - 1);         // desplaza a la izquierda
                _buf[s][_cap - 1] = (values != null && s < values.Length) ? values[s] : double.NaN;
            }
            Redraw();
        }

        private void Redraw()
        {
            double w = _canvas.ActualWidth;
            double h = _canvas.ActualHeight;
            if (w <= 0 || h <= 0) return;
            double plotW = w - PadL;
            if (plotW <= 1) return;

            for (int i = 0; i < 3; i++)
            {
                double y = h * (i + 1) / 4.0;
                _grid[i].X1 = PadL; _grid[i].X2 = w; _grid[i].Y1 = y; _grid[i].Y2 = y;
            }

            // etiquetas del eje: 100 arriba, 0 abajo
            for (int i = 0; i < _axisVals.Length; i++)
            {
                double norm = _axisVals[i] / _max; if (norm < 0) norm = 0; if (norm > 1) norm = 1;
                double y = h - norm * h - 6;
                if (y < 0) y = 0; if (y > h - 12) y = h - 12;
                Canvas.SetLeft(_axis[i], 3);
                Canvas.SetTop(_axis[i], y);
            }

            for (int s = 0; s < _series; s++)
            {
                var pts = new PointCollection();
                for (int i = 0; i < _cap; i++)
                {
                    double v = _buf[s][i];
                    if (double.IsNaN(v)) continue;
                    double x = PadL + plotW * i / (double)(_cap - 1);   // más reciente a la derecha
                    double norm = v / _max; if (norm < 0) norm = 0; if (norm > 1) norm = 1;
                    double y = h - norm * h;
                    pts.Add(new Point(x, y));
                }
                _poly[s].Points = pts;
            }
        }
    }
}
