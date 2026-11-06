using Aetheris.Kernel.Firmament.FirmamentV2;
using Aetheris.Kernel.Firmament.Materializer;

namespace Aetheris.Kernel.Firmament.Tests;

/// <summary>`|> Round(r)` geometry: the arc is tangent to both trimmed neighbours and joins them continuously.</summary>
public sealed class ProfileCornerRoundTests
{
    public static TheoryData<string, LineArcProfileCurve2D, LineArcProfileCurve2D, double> Corners() => new()
    {
        // Convex and concave line corners, at right and oblique angles.
        { "line-line convex", new LineArcLineSegment2D((-20, 0), (0, 0)), new LineArcLineSegment2D((0, 0), (0, 20)), 3 },
        { "line-line concave", new LineArcLineSegment2D((-20, 0), (0, 0)), new LineArcLineSegment2D((0, 0), (0, -20)), 3 },
        { "line-line oblique", new LineArcLineSegment2D((-20, 0), (0, 0)), new LineArcLineSegment2D((0, 0), (10, 15)), 2 },
        // A line into a clockwise arc whose start tangent turns left of the line.
        { "line-arc", new LineArcLineSegment2D((-20, 0), (0, 0)), new LineArcCircularArc2D((10, 0), 10, Math.PI, -Math.PI / 2), 2 },
        // A counter-clockwise arc into a line leaving it at a corner.
        { "arc-line", new LineArcCircularArc2D((0, 10), 10, -Math.PI / 2, Math.PI / 4), new LineArcLineSegment2D((10 * Math.Sin(Math.PI / 4), 10 - 10 * Math.Cos(Math.PI / 4)), (20, 20)), 1 },
        // Two arcs meeting at a corner.
        { "arc-arc", new LineArcCircularArc2D((0, 10), 10, -Math.PI / 2, Math.PI / 3), new LineArcCircularArc2D((10 * Math.Sin(Math.PI / 3) + 8, 10 - 10 * Math.Cos(Math.PI / 3)), 8, Math.PI, -Math.PI / 3), 1 },
    };

    [Theory]
    [MemberData(nameof(Corners))]
    public void RoundIsTangentAndContinuous(string name, LineArcProfileCurve2D previous, LineArcProfileCurve2D next, double radius)
    {
        var result = ProfileCornerRound.TryRound(previous, next, radius, out var failure);
        Assert.True(result is not null, $"{name}: {failure}");
        Assert.Equal(radius, result.Arc.Radius, 12);
        AssertJoin(result.Previous, result.Arc, name + " entry");
        AssertJoin(result.Arc, result.Next, name + " exit");
        // The centre is exactly one radius from both neighbours' carriers.
        Assert.Equal(radius, CarrierDistance(previous, result.Arc.Center), 9);
        Assert.Equal(radius, CarrierDistance(next, result.Arc.Center), 9);
        // A round replaces a corner, so it turns by less than a half circle.
        Assert.InRange(Math.Abs(result.Arc.SweepAngleRadians), 1e-6, Math.PI);
    }

    [Fact]
    public void TangentJunctionsAndOversizedRadiiAreRefused()
    {
        var line = new LineArcLineSegment2D((-20, 0), (0, 0));
        Assert.Null(ProfileCornerRound.TryRound(line, new LineArcLineSegment2D((0, 0), (20, 0)), 2, out var straight));
        Assert.Contains("tangentially", straight);
        Assert.Null(ProfileCornerRound.TryRound(line, new LineArcLineSegment2D((0, 0), (0, 5)), 8, out var large));
        Assert.Contains("too large", large);
        Assert.Null(ProfileCornerRound.TryRound(line, new LineArcLineSegment2D((1, 0), (1, 5)), 1, out var apart));
        Assert.Contains("do not meet", apart);
    }

    private static void AssertJoin(LineArcProfileCurve2D a, LineArcProfileCurve2D b, string where)
    {
        var (end, tangentA) = EndOf(a); var (start, tangentB) = StartOf(b);
        Assert.True(Math.Abs(end.X - start.X) < 1e-9 && Math.Abs(end.Y - start.Y) < 1e-9, $"{where}: gap {end} -> {start}");
        Assert.True(Math.Abs(tangentA.X * tangentB.Y - tangentA.Y * tangentB.X) < 1e-9 && tangentA.X * tangentB.X + tangentA.Y * tangentB.Y > 0, $"{where}: not tangent");
    }

    private static ((double X, double Y) Point, (double X, double Y) Tangent) StartOf(LineArcProfileCurve2D c) => c switch
    {
        LineArcLineSegment2D l => (l.Start, Unit(l.End.X - l.Start.X, l.End.Y - l.Start.Y)),
        LineArcCircularArc2D a => (Point(a, a.StartAngleRadians), Tangent(a, a.StartAngleRadians)),
        _ => throw new InvalidOperationException()
    };

    private static ((double X, double Y) Point, (double X, double Y) Tangent) EndOf(LineArcProfileCurve2D c) => c switch
    {
        LineArcLineSegment2D l => (l.End, Unit(l.End.X - l.Start.X, l.End.Y - l.Start.Y)),
        LineArcCircularArc2D a => (Point(a, a.StartAngleRadians + a.SweepAngleRadians), Tangent(a, a.StartAngleRadians + a.SweepAngleRadians)),
        _ => throw new InvalidOperationException()
    };

    private static double CarrierDistance(LineArcProfileCurve2D c, (double X, double Y) p) => c switch
    {
        LineArcLineSegment2D l => Math.Abs((l.End.X - l.Start.X) * (p.Y - l.Start.Y) - (l.End.Y - l.Start.Y) * (p.X - l.Start.X)) / Math.Sqrt(Math.Pow(l.End.X - l.Start.X, 2) + Math.Pow(l.End.Y - l.Start.Y, 2)),
        LineArcCircularArc2D a => Math.Abs(Math.Sqrt(Math.Pow(p.X - a.Center.X, 2) + Math.Pow(p.Y - a.Center.Y, 2)) - a.Radius),
        _ => throw new InvalidOperationException()
    };

    private static (double X, double Y) Point(LineArcCircularArc2D a, double t) => (a.Center.X + a.Radius * Math.Cos(t), a.Center.Y + a.Radius * Math.Sin(t));
    private static (double X, double Y) Tangent(LineArcCircularArc2D a, double t) => Unit(-Math.Sin(t) * Math.Sign(a.SweepAngleRadians), Math.Cos(t) * Math.Sign(a.SweepAngleRadians));
    private static (double X, double Y) Unit(double x, double y) { var l = Math.Sqrt(x * x + y * y); return (x / l, y / l); }
}
