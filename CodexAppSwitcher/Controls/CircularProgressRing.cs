using System;
using System.Windows;
using System.Windows.Media;

namespace CodexAppSwitcher.Controls;

/// <summary>
/// 用于桌面悬浮球的环形进度绘制控件。
/// </summary>
public sealed class CircularProgressRing : FrameworkElement
{
    /// <summary>
    /// 进度值依赖属性。
    /// </summary>
    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(
        nameof(Value),
        typeof(double),
        typeof(CircularProgressRing),
        new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.AffectsRender));

    /// <summary>
    /// 最大值依赖属性。
    /// </summary>
    public static readonly DependencyProperty MaximumProperty = DependencyProperty.Register(
        nameof(Maximum),
        typeof(double),
        typeof(CircularProgressRing),
        new FrameworkPropertyMetadata(100d, FrameworkPropertyMetadataOptions.AffectsRender));

    /// <summary>
    /// 轨道画刷依赖属性。
    /// </summary>
    public static readonly DependencyProperty TrackBrushProperty = DependencyProperty.Register(
        nameof(TrackBrush),
        typeof(Brush),
        typeof(CircularProgressRing),
        new FrameworkPropertyMetadata(Brushes.Transparent, FrameworkPropertyMetadataOptions.AffectsRender));

    /// <summary>
    /// 进度画刷依赖属性。
    /// </summary>
    public static readonly DependencyProperty ProgressBrushProperty = DependencyProperty.Register(
        nameof(ProgressBrush),
        typeof(Brush),
        typeof(CircularProgressRing),
        new FrameworkPropertyMetadata(Brushes.White, FrameworkPropertyMetadataOptions.AffectsRender));

    /// <summary>
    /// 圆环线宽依赖属性。
    /// </summary>
    public static readonly DependencyProperty StrokeThicknessProperty = DependencyProperty.Register(
        nameof(StrokeThickness),
        typeof(double),
        typeof(CircularProgressRing),
        new FrameworkPropertyMetadata(3d, FrameworkPropertyMetadataOptions.AffectsRender));

    /// <summary>
    /// 当前进度值。
    /// </summary>
    public double Value
    {
        get => (double)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    /// <summary>
    /// 进度最大值。
    /// </summary>
    public double Maximum
    {
        get => (double)GetValue(MaximumProperty);
        set => SetValue(MaximumProperty, value);
    }

    /// <summary>
    /// 圆环轨道画刷。
    /// </summary>
    public Brush TrackBrush
    {
        get => (Brush)GetValue(TrackBrushProperty);
        set => SetValue(TrackBrushProperty, value);
    }

    /// <summary>
    /// 圆环进度画刷。
    /// </summary>
    public Brush ProgressBrush
    {
        get => (Brush)GetValue(ProgressBrushProperty);
        set => SetValue(ProgressBrushProperty, value);
    }

    /// <summary>
    /// 圆环线宽。
    /// </summary>
    public double StrokeThickness
    {
        get => (double)GetValue(StrokeThicknessProperty);
        set => SetValue(StrokeThicknessProperty, value);
    }

    /// <inheritdoc />
    protected override void OnRender(DrawingContext drawingContext)
    {
        base.OnRender(drawingContext);

        var strokeThickness = Math.Max(1d, StrokeThickness);
        var radius = Math.Max(0d, Math.Min(RenderSize.Width, RenderSize.Height) / 2d - strokeThickness / 2d);
        if (radius <= 0d)
        {
            return;
        }

        var center = new Point(RenderSize.Width / 2d, RenderSize.Height / 2d);
        var trackPen = CreatePen(TrackBrush, strokeThickness);
        drawingContext.DrawEllipse(null, trackPen, center, radius, radius);

        var maximum = Maximum <= 0d ? 100d : Maximum;
        var percent = Math.Clamp(Value / maximum, 0d, 1d);
        if (percent <= 0d)
        {
            return;
        }

        var progressPen = CreatePen(ProgressBrush, strokeThickness);
        if (percent >= 0.999d)
        {
            drawingContext.DrawEllipse(null, progressPen, center, radius, radius);
            return;
        }

        var startPoint = PointOnCircle(center, radius, -90d);
        var endPoint = PointOnCircle(center, radius, percent * 360d - 90d);
        var geometry = new StreamGeometry();
        using (var context = geometry.Open())
        {
            context.BeginFigure(startPoint, false, false);
            context.ArcTo(endPoint, new Size(radius, radius), 0d, percent > 0.5d, SweepDirection.Clockwise, true, false);
        }

        geometry.Freeze();
        drawingContext.DrawGeometry(null, progressPen, geometry);
    }

    private static Pen CreatePen(Brush brush, double thickness)
    {
        var pen = new Pen(brush, thickness)
        {
            StartLineCap = PenLineCap.Round,
            EndLineCap = PenLineCap.Round,
            LineJoin = PenLineJoin.Round
        };

        if (pen.CanFreeze)
        {
            pen.Freeze();
        }

        return pen;
    }

    private static Point PointOnCircle(Point center, double radius, double angleDegrees)
    {
        var angle = angleDegrees * Math.PI / 180d;
        return new Point(
            center.X + radius * Math.Cos(angle),
            center.Y + radius * Math.Sin(angle));
    }
}
