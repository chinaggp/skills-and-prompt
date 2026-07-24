namespace WindowsBlackHole.Core;

public static class BlackHoleGeometry
{
    public const double WindowSize = 520;
    public const double Center = WindowSize / 2;
    public const double EventHorizonRadius = 66;
    public const double InfluenceRadius = 235;

    public static double DistanceFromCenter(double x, double y)
    {
        var dx = x - Center;
        var dy = y - Center;
        return Math.Sqrt((dx * dx) + (dy * dy));
    }

    public static bool IsInsideEventHorizon(double x, double y) =>
        DistanceFromCenter(x, y) <= EventHorizonRadius;

    public static double Proximity(double x, double y)
    {
        var distance = DistanceFromCenter(x, y);
        return Math.Clamp(1 - (distance / InfluenceRadius), 0, 1);
    }

    public static DragLensTransform DragLensTransform(double x, double y)
    {
        var distance = DistanceFromCenter(x, y);
        var proximity = Proximity(x, y);
        if (proximity <= 0)
        {
            return new DragLensTransform(x, y, 1, 1, 0, 0, 0);
        }

        var eased = proximity * proximity * (3 - (2 * proximity));
        var pull = 0.08 + (eased * 0.24);
        var transformedX = x + ((Center - x) * pull);
        var transformedY = y + ((Center - y) * pull);
        var rotation = distance <= double.Epsilon
            ? 0
            : Math.Atan2(y - Center, x - Center) * 180 / Math.PI;
        var scaleAlongGravity = 1 + (eased * 1.45);
        var scaleAcrossGravity = 1 - (eased * 0.58);
        var skew = Math.Sin(proximity * Math.PI) * 12;
        var opacity = Math.Clamp(proximity * 1.8, 0, 1);

        if (distance < EventHorizonRadius)
        {
            var collapse = 1 - (distance / EventHorizonRadius);
            scaleAlongGravity *= 1 - (collapse * 0.9);
            scaleAcrossGravity *= 1 - (collapse * 0.82);
            opacity *= 1 - (collapse * 0.82);
        }

        return new DragLensTransform(
            transformedX,
            transformedY,
            scaleAlongGravity,
            scaleAcrossGravity,
            rotation,
            skew,
            opacity);
    }
}

public readonly record struct DragLensTransform(
    double X,
    double Y,
    double ScaleX,
    double ScaleY,
    double RotationDegrees,
    double SkewDegrees,
    double Opacity);
