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
}
