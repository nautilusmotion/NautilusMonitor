// NauTilus Monitor - Controles visuales: gauge radial y barra de nivel

using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace NautilusMotion.Monitor
{
    // Gauge circular (arco de 270 grados) al estilo de la marca.
    public class Gauge : Grid
    {
        private readonly double _size;
        private readonly double _cx, _cy, _r, _th;
        private readonly double _startDeg = 135;   // esquina inferior izquierda
        private readonly double _sweepMax = 270;    // hasta la inferior derecha

        private readonly Path _valueArc;
        private readonly TextBlock _big;
        private readonly TextBlock _small;
        private readonly TextBlock _caption;

        public Gauge(string caption, double size)
        {
            _size = size;
            _th = 11;
            _cx = size / 2.0;
            _cy = size / 2.0;
            _r = size / 2.0 - _th / 2.0 - 2;

            Width = size;
            Height = size;

            var canvas = new Canvas { Width = size, Height = size };
            canvas.Children.Add(MakeArc(_startDeg, _sweepMax, Theme.Track, _th)); // pista
            _valueArc = MakeArc(_startDeg, 0.01, Theme.AccentBrush, _th);          // valor
            canvas.Children.Add(_valueArc);
            Children.Add(canvas);

            var textStack = new StackPanel
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            _big = new TextBlock { Text = "--", Foreground = Theme.TitleText, FontSize = 28, FontWeight = FontWeights.Bold, HorizontalAlignment = HorizontalAlignment.Center };
            _small = new TextBlock { Text = "", Foreground = Theme.Muted, FontSize = 11, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, -1, 0, 0) };
            textStack.Children.Add(_big);
            textStack.Children.Add(_small);
            Children.Add(textStack);

            _caption = new TextBlock
            {
                Text = caption.ToUpperInvariant(),
                Foreground = Theme.AccentBrush,
                FontSize = 10,
                FontWeight = FontWeights.Bold,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Bottom,
                Margin = new Thickness(0, 0, 0, 2)
            };
            Children.Add(_caption);
        }

        // pct 0-100. big = numero grande; small = subtexto.
        // Arco en rojo de marca; numero en blanco.
        public void SetValue(double pct, string big, string small)
        {
            if (pct < 0) pct = 0; if (pct > 100) pct = 100;
            double sweep = _sweepMax * pct / 100.0;
            if (sweep < 0.01) sweep = 0.01;
            _valueArc.Data = ArcGeometry(_startDeg, sweep);
            _valueArc.Stroke = Theme.AccentBrush;
            _big.Text = big;
            _big.Foreground = Theme.TitleText;
            _small.Text = small == null ? "" : small;
        }

        public void SetUnknown(string label)
        {
            _valueArc.Data = ArcGeometry(_startDeg, 0.01);
            _big.Text = "n/d";
            _big.Foreground = Theme.Muted;
            _small.Text = label == null ? "" : label;
        }

        private Path MakeArc(double startDeg, double sweepDeg, Brush stroke, double th)
        {
            return new Path
            {
                Data = ArcGeometry(startDeg, sweepDeg),
                Stroke = stroke,
                StrokeThickness = th,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round
            };
        }

        private Geometry ArcGeometry(double startDeg, double sweepDeg)
        {
            Point p0 = Polar(startDeg);
            Point p1 = Polar(startDeg + sweepDeg);
            var fig = new PathFigure { StartPoint = p0, IsClosed = false, IsFilled = false };
            var seg = new ArcSegment
            {
                Point = p1,
                Size = new Size(_r, _r),
                SweepDirection = SweepDirection.Clockwise,
                IsLargeArc = Math.Abs(sweepDeg) > 180,
                RotationAngle = 0
            };
            fig.Segments.Add(seg);
            var g = new PathGeometry();
            g.Figures.Add(fig);
            return g;
        }

        private Point Polar(double angleDeg)
        {
            double a = angleDeg * Math.PI / 180.0;
            return new Point(_cx + _r * Math.Cos(a), _cy + _r * Math.Sin(a));
        }
    }

    // Barra de nivel horizontal (por-nucleo, disco, etc.). Se auto-ajusta con
    // columnas proporcionales, sin depender de medidas de layout.
    public class BarMeter : Border
    {
        private readonly ColumnDefinition _fillCol;
        private readonly ColumnDefinition _restCol;
        private readonly Border _fill;

        public BarMeter(double height)
        {
            Height = height;
            CornerRadius = new CornerRadius(height / 2.0);
            Background = Theme.Track;
            SnapsToDevicePixels = true;

            var g = new Grid();
            _fillCol = new ColumnDefinition { Width = new GridLength(0.01, GridUnitType.Star) };
            _restCol = new ColumnDefinition { Width = new GridLength(100, GridUnitType.Star) };
            g.ColumnDefinitions.Add(_fillCol);
            g.ColumnDefinitions.Add(_restCol);

            _fill = new Border { CornerRadius = new CornerRadius(height / 2.0), Background = Theme.AccentBrush };
            Grid.SetColumn(_fill, 0);
            g.Children.Add(_fill);
            Child = g;
        }

        public void SetValue(double pct, Brush color)
        {
            if (pct < 0) pct = 0; if (pct > 100) pct = 100;
            if (pct < 0.5) pct = 0.5; // siempre visible un minimo
            _fillCol.Width = new GridLength(pct, GridUnitType.Star);
            _restCol.Width = new GridLength(100 - pct, GridUnitType.Star);
            _fill.Background = color;
        }
    }
}
