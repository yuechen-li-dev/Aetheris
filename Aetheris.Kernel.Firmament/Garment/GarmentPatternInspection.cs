using Aetheris.Kernel.Firmament.Materializer;

namespace Aetheris.Kernel.Firmament.Garment;

public sealed record GarmentPatternBoundary(string Name, IReadOnlyList<ResolvedProfileSegment2D> Segments,
    double LengthMm);
public sealed record GarmentPatternSeam(string Name, string A, string B, bool Reversed,
    double LengthAMm, double LengthBMm, double Mismatch, double AllowedEase);

/// <summary>Pattern inspection precedes meshing. Named edits select their ordered descendants;
/// consumed carrier names are never silently rebound to a child.</summary>
public static class GarmentPatternInspection
{
    public static IReadOnlyDictionary<string, GarmentPatternBoundary> Boundaries(GarmentPanel panel)
    {
        var segments = panel.Profile.Loops.Single().Segments;
        var result = new Dictionary<string, GarmentPatternBoundary>(StringComparer.Ordinal);
        foreach (var segment in segments)
        {
            result.Add(segment.Name, new(segment.Name, [segment], CurveLength(segment.Geometry)));
        }
        foreach (var edit in panel.Profile.BoundaryEdits ?? [])
        {
            var descendants = edit.Descendants.Select(name => segments.Single(segment => segment.Name == name)).ToArray();
            if (!result.TryAdd(edit.Name, new(edit.Name, descendants,
                    descendants.Sum(segment => CurveLength(segment.Geometry))))
                && !result[edit.Name].Segments.SequenceEqual(descendants))
            {
                GarmentAuthoring.Fail("garment-boundary-name-ambiguous", panel.Identity + ".edges." + edit.Name);
            }
        }
        return result;
    }

    public static IReadOnlyList<GarmentPatternSeam> Inspect(GarmentSource source)
    {
        var used = new HashSet<string>(StringComparer.Ordinal);
        var result = new List<GarmentPatternSeam>();
        foreach (var panel in source.Panels)
        {
            if (panel.Profile.Loops.Count != 1 || !panel.Profile.Loops[0].IsOuter)
            {
                GarmentAuthoring.Fail("garment-profile-holes-unsupported", panel.Identity);
            }
            var boundaries = Boundaries(panel);
            foreach (var pin in panel.Pins)
            {
                if (!boundaries.ContainsKey(pin))
                {
                    GarmentAuthoring.Fail("garment-pin-edge-unknown", panel.Identity + ".edges." + pin);
                }
            }
        }
        foreach (var seam in source.Stitches)
        {
            var a = Resolve(seam.A);
            var b = Resolve(seam.B);
            double mismatch = Math.Abs(a.LengthMm - b.LengthMm) / Math.Max(a.LengthMm, b.LengthMm);
            if (mismatch > seam.Ease + .001)
            {
                GarmentAuthoring.Fail("garment-stitch-length-mismatch",
                    FormattableString.Invariant($"{seam.Name}: {a.LengthMm:F3}mm / {b.LengthMm:F3}mm; explicit ease >= {mismatch:R} required."));
            }
            result.Add(new(seam.Name, seam.A, seam.B, seam.Reversed, a.LengthMm, b.LengthMm, mismatch, seam.Ease));
        }
        return result;

        GarmentPatternBoundary Resolve(string reference)
        {
            int separator = reference.IndexOf(".edges.", StringComparison.Ordinal);
            if (separator < 1)
            {
                GarmentAuthoring.Fail("garment-stitch-reference", reference);
            }
            string identity = reference[..separator];
            string name = reference[(separator + 7)..];
            var panel = source.Panels.SingleOrDefault(panel => panel.Name == identity || panel.Identity == identity);
            if (panel is null || !Boundaries(panel).TryGetValue(name, out var boundary))
            {
                GarmentAuthoring.Fail("garment-stitch-edge-unknown", reference);
                return null!;
            }
            foreach (var segment in boundary.Segments)
            {
                if (!used.Add(panel.Identity + ".edges." + segment.Name))
                {
                    GarmentAuthoring.Fail("garment-stitch-edge-reused", reference + " overlaps an already stitched span.");
                }
            }
            return boundary;
        }
    }

    internal static double CurveLength(LineArcProfileCurve2D curve)
    {
        switch (curve)
        {
            case LineArcLineSegment2D line:
                return Distance(line.Start, line.End);
            case LineArcCircularArc2D arc:
                return arc.Radius * Math.Abs(arc.SweepAngleRadians);
            case LineArcCubicBezier2D cubic:
                return Integrate(0, 1, Speed(0), Speed(.5), Speed(1), 0);

                double Speed(double t)
                {
                    double u = 1 - t;
                    double x = 3 * (u * u * (cubic.Control1.X - cubic.Start.X)
                        + 2 * u * t * (cubic.Control2.X - cubic.Control1.X) + t * t * (cubic.End.X - cubic.Control2.X));
                    double y = 3 * (u * u * (cubic.Control1.Y - cubic.Start.Y)
                        + 2 * u * t * (cubic.Control2.Y - cubic.Control1.Y) + t * t * (cubic.End.Y - cubic.Control2.Y));
                    return Math.Sqrt(x * x + y * y);
                }
                double Integrate(double a, double b, double fa, double fm, double fb, int depth)
                {
                    double middle = (a + b) / 2;
                    double left = Speed((a + middle) / 2);
                    double right = Speed((middle + b) / 2);
                    double whole = (b - a) * (fa + 4 * fm + fb) / 6;
                    double halves = (b - a) * (fa + 4 * left + 2 * fm + 4 * right + fb) / 12;
                    if (depth >= 12 || Math.Abs(halves - whole) < 1e-5)
                    {
                        return halves + (halves - whole) / 15;
                    }
                    return Integrate(a, middle, fa, left, fm, depth + 1)
                        + Integrate(middle, b, fm, right, fb, depth + 1);
                }
            default:
                GarmentAuthoring.Fail("garment-curve-unsupported", "Patterns admit lines, circular arcs and cubic Beziers.");
                return 0;
        }
    }

    internal static (double X, double Y) Start(LineArcProfileCurve2D curve) => curve switch
    {
        LineArcLineSegment2D line => line.Start,
        LineArcCubicBezier2D cubic => cubic.Start,
        LineArcCircularArc2D arc => (arc.Center.X + arc.Radius * Math.Cos(arc.StartAngleRadians),
            arc.Center.Y + arc.Radius * Math.Sin(arc.StartAngleRadians)),
        _ => throw new NotSupportedException("Unsupported pattern curve."),
    };

    private static double Distance((double X, double Y) a, (double X, double Y) b)
    {
        double x = b.X - a.X;
        double y = b.Y - a.Y;
        return Math.Sqrt(x * x + y * y);
    }
}
