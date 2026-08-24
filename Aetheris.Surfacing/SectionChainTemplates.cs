using Aetheris.Kernel.Core.Math;

namespace Aetheris.Surfacing;

public static class SectionChainTemplates
{
    private static readonly string[] SpanIds = ["South", "East", "North", "West"];

    /// <summary>Eight changing, explicitly framed stations for the SURF-X3 flagship fairing.</summary>
    public static SectionChain ErgonomicFairing(string stableId = "surf-x3-section-chain-ergonomic-body")
    {
        var stations = new[]
        {
            Station("Nose",       new(0, 0, 0),     5,  3.5, 0.0,  0,  0),
            Station("Front",      new(0, 0, 10),   12,  7.0, 0.0,  0,  1),
            Station("PalmFront",  new(0, 0, 26),   23, 12.0, 3.5,  1,  2),
            Station("Rise",       new(0, 1, 43),   28, 15.0, 5.0,  2,  4),
            Station("Peak",       new(0, 2, 59),   30, 16.5, 6.0,  2,  5),
            Station("PalmRear",   new(0, 1, 75),   26, 14.0, 5.0,  1,  3),
            Station("Rear",       new(0, 0, 89),   17,  9.0, 3.0, -1,  1),
            Station("Tail",       new(0, 0, 100),   6,  4.0, 0.0, -1,  0),
        };
        return new(stableId, stations, ExplicitCorrespondence(stations), SectionTransitionPolicy.Ruled,
            SectionTermination.Cap, SectionTermination.Cap);
    }

    public static SectionChain TwistWitness(string stableId = "section-chain-twist")
    {
        var stations = new[]
        {
            Station("S0", new(0, 0, 0), 10, 7, 2, 0, 0),
            Station("S1", new(0, 0, 18), 11, 7, 2, 10, 0),
            Station("S2", new(0, 0, 36), 10, 6, 2, 20, 0),
        };
        return new(stableId, stations, ExplicitCorrespondence(stations), SectionTransitionPolicy.Ruled,
            SectionTermination.Cap, SectionTermination.Cap);
    }

    public static SectionChain TwoProfileRuled(string stableId = "section-chain-two-profile-ruled")
    {
        var stations = new[]
        {
            Station("Base", new(0, 0, 0), 10, 8, 0, 0, 0),
            Station("Top", new(0, 0, 20), 7, 5, 0, 0, 0),
        };
        return new(stableId, stations, ExplicitCorrespondence(stations), SectionTransitionPolicy.Ruled,
            SectionTermination.Cap, SectionTermination.Cap);
    }

    private static Section Station(string id, Point3D origin, double halfWidth, double halfHeight, double roundness,
        double clockDegrees, double tiltDegrees)
    {
        var clock = clockDegrees * Math.PI / 180d; var tilt = tiltDegrees * Math.PI / 180d;
        var x = new Vector3D(Math.Cos(clock) * Math.Cos(tilt), Math.Sin(clock) * Math.Cos(tilt), -Math.Sin(tilt));
        var normal = new Vector3D(Math.Cos(clock) * Math.Sin(tilt), Math.Sin(clock) * Math.Sin(tilt), Math.Cos(tilt));
        var y = normal.Cross(x);
        var frame = SectionFrame.Create(origin, x, y);
        return new(id, frame, Profile(id + ".Profile", halfWidth, halfHeight, roundness));
    }

    private static SectionProfile Profile(string id, double w, double h, double roundness)
    {
        var corners = new[] { new SectionPoint2D(-w, -h), new SectionPoint2D(w, -h), new SectionPoint2D(w, h), new SectionPoint2D(-w, h) };
        var controls = new[]
        {
            new[] { corners[0], new(-w / 3, -h - roundness), new(w / 3, -h - roundness), corners[1] },
            new[] { corners[1], new(w + roundness, -h / 3), new(w + roundness, h / 3), corners[2] },
            new[] { corners[2], new(w / 3, h + roundness), new(-w / 3, h + roundness), corners[3] },
            new[] { corners[3], new(-w - roundness, h / 3), new(-w - roundness, -h / 3), corners[0] },
        };
        var spans = Enumerable.Range(0, 4).Select(index => new SectionProfileSpan(SpanIds[index], roundness <= 0
            ? new SectionProfileCurve.Line(corners[index], corners[(index + 1) % 4])
            : new SectionProfileCurve.PolynomialBSpline(3, controls[index], [4, 4], [0d, 1d]) as SectionProfileCurve)).ToArray();
        return new(id, spans, SpanIds[0]);
    }

    private static IReadOnlyList<AdjacentSectionCorrespondence> ExplicitCorrespondence(IReadOnlyList<Section> sections) =>
        Enumerable.Range(0, sections.Count - 1).Select(index => new AdjacentSectionCorrespondence(
            sections[index].SectionId, sections[index + 1].SectionId,
            SpanIds.Select(span => new SectionSpanCorrespondence(span, span)).ToArray())).ToArray();
}
