using System.Windows;
using System.Windows.Media;

namespace D2RBuffTracker.Overlay;

/// <summary>
/// A World-of-Warcraft-style radial cooldown "swipe": a dark wedge that covers
/// the fraction of time that has elapsed and grows clockwise as the buff counts
/// down, filling the icon by the time it expires. <see cref="Progress"/> is the
/// remaining fraction (0..1).
/// </summary>
public sealed class RadialCooldown : FrameworkElement
{
    public static readonly DependencyProperty ProgressProperty =
        DependencyProperty.Register(
            nameof(Progress), typeof(double), typeof(RadialCooldown),
            new FrameworkPropertyMetadata(1.0, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty FillProperty =
        DependencyProperty.Register(
            nameof(Fill), typeof(Brush), typeof(RadialCooldown),
            new FrameworkPropertyMetadata(
                new SolidColorBrush(Color.FromArgb(150, 0, 0, 0)),
                FrameworkPropertyMetadataOptions.AffectsRender));

    /// <summary>Fraction of the buff duration still remaining (0..1).</summary>
    public double Progress
    {
        get => (double)GetValue(ProgressProperty);
        set => SetValue(ProgressProperty, value);
    }

    /// <summary>The wedge fill (defaults to a semi-transparent black shade).</summary>
    public Brush Fill
    {
        get => (Brush)GetValue(FillProperty);
        set => SetValue(FillProperty, value);
    }

    protected override void OnRender(DrawingContext dc)
    {
        var p = Math.Clamp(Progress, 0, 1);
        // Shade the elapsed portion: the wedge starts empty and grows clockwise
        // from 12 o'clock, filling the icon as the buff runs out.
        var elapsed = 1 - p;
        if (elapsed <= 0)
            return;

        var w = ActualWidth;
        var h = ActualHeight;
        if (w <= 0 || h <= 0)
            return;

        var centre = new Point(w / 2, h / 2);
        // Radius large enough to cover the square corners (clipped to bounds).
        var radius = 0.5 * Math.Sqrt(w * w + h * h) + 1;

        if (elapsed >= 1)
        {
            dc.DrawEllipse(Fill, null, centre, radius, radius);
            return;
        }

        var sweep = elapsed * 360.0; // shaded elapsed wedge, growing from the top

        var start = PointOnCircle(centre, radius, 0);
        var end = PointOnCircle(centre, radius, sweep);

        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            ctx.BeginFigure(centre, isFilled: true, isClosed: true);
            ctx.LineTo(start, isStroked: false, isSmoothJoin: false);
            ctx.ArcTo(end, new Size(radius, radius), 0,
                isLargeArc: sweep > 180, SweepDirection.Clockwise,
                isStroked: false, isSmoothJoin: false);
            ctx.LineTo(centre, isStroked: false, isSmoothJoin: false);
        }
        geometry.Freeze();

        dc.DrawGeometry(Fill, null, geometry);
    }

    private static Point PointOnCircle(Point centre, double radius, double degrees)
    {
        var rad = degrees * Math.PI / 180.0;
        return new Point(
            centre.X + radius * Math.Sin(rad),
            centre.Y - radius * Math.Cos(rad));
    }
}
