using System.Windows;
using System.Windows.Media;

namespace LogiBatteryWidget.App.Controls;

/// <summary>
/// A circular progress ring in the style of iOS/macOS's Batteries widget: a muted full-circle
/// track with a colored arc drawn clockwise from the top for the current percentage. WPF has no
/// built-in ring/arc shape, so this draws the arc geometry by hand in <see cref="OnRender"/>.
/// </summary>
public sealed class RingProgress : FrameworkElement
{
    public static readonly DependencyProperty PercentageProperty = DependencyProperty.Register(
        nameof(Percentage), typeof(double), typeof(RingProgress),
        new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty RingBrushProperty = DependencyProperty.Register(
        nameof(RingBrush), typeof(Brush), typeof(RingProgress),
        new FrameworkPropertyMetadata(Brushes.LightGray, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty TrackBrushProperty = DependencyProperty.Register(
        nameof(TrackBrush), typeof(Brush), typeof(RingProgress),
        new FrameworkPropertyMetadata(Brushes.DarkGray, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty RingThicknessProperty = DependencyProperty.Register(
        nameof(RingThickness), typeof(double), typeof(RingProgress),
        new FrameworkPropertyMetadata(4.0, FrameworkPropertyMetadataOptions.AffectsRender));

    public double Percentage
    {
        get => (double)GetValue(PercentageProperty);
        set => SetValue(PercentageProperty, value);
    }

    public Brush RingBrush
    {
        get => (Brush)GetValue(RingBrushProperty);
        set => SetValue(RingBrushProperty, value);
    }

    public Brush TrackBrush
    {
        get => (Brush)GetValue(TrackBrushProperty);
        set => SetValue(TrackBrushProperty, value);
    }

    public double RingThickness
    {
        get => (double)GetValue(RingThicknessProperty);
        set => SetValue(RingThicknessProperty, value);
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        var size = Math.Min(ActualWidth, ActualHeight);
        if (size <= 0)
        {
            return;
        }

        var thickness = RingThickness;
        var radius = (size - thickness) / 2;
        var center = new Point(ActualWidth / 2, ActualHeight / 2);

        var trackPen = new Pen(TrackBrush, thickness)
        {
            StartLineCap = PenLineCap.Round,
            EndLineCap = PenLineCap.Round,
        };
        drawingContext.DrawEllipse(null, trackPen, center, radius, radius);

        var pct = Math.Clamp(Percentage, 0, 100);
        if (pct <= 0)
        {
            return;
        }

        var ringPen = new Pen(RingBrush, thickness)
        {
            StartLineCap = PenLineCap.Round,
            EndLineCap = PenLineCap.Round,
        };

        // A full circle can't be represented as a single arc segment (start == end point gives a
        // degenerate/invisible arc), so draw it as a plain ellipse instead.
        if (pct >= 99.95)
        {
            drawingContext.DrawEllipse(null, ringPen, center, radius, radius);
            return;
        }

        const double startAngleDegrees = -90; // 12 o'clock
        var sweepAngleDegrees = 360.0 * (pct / 100.0);
        var isLargeArc = sweepAngleDegrees > 180.0;

        var startPoint = PointOnCircle(center, radius, startAngleDegrees);
        var endPoint = PointOnCircle(center, radius, startAngleDegrees + sweepAngleDegrees);

        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            ctx.BeginFigure(startPoint, isFilled: false, isClosed: false);
            ctx.ArcTo(endPoint, new Size(radius, radius), rotationAngle: 0, isLargeArc,
                SweepDirection.Clockwise, isStroked: true, isSmoothJoin: false);
        }
        geometry.Freeze();

        drawingContext.DrawGeometry(null, ringPen, geometry);
    }

    private static Point PointOnCircle(Point center, double radius, double angleDegrees)
    {
        var angleRadians = angleDegrees * Math.PI / 180.0;
        return new Point(
            center.X + radius * Math.Cos(angleRadians),
            center.Y + radius * Math.Sin(angleRadians));
    }
}
