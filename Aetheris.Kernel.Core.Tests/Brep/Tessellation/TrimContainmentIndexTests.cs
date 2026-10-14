using Aetheris.Kernel.Core.Brep.Tessellation;

namespace Aetheris.Kernel.Core.Tests.Brep.Tessellation;

/// <summary>BUILD-PERF-M4: the bucketed trim containment index answers exactly like the reference loop.</summary>
public sealed class TrimContainmentIndexTests
{
    public static TheoryData<string> Shapes() => new() { "rectangle", "star", "degenerate-segment", "comb" };

    private static List<(double U, double V)> Polygon(string shape)
    {
        var points = new List<(double U, double V)>();
        switch (shape)
        {
            case "rectangle":
                // A densely sampled domain rectangle, as constructed SectionChain trims produce.
                for (var i = 0; i < 40; i++) points.Add((i / 40d, 0d));
                for (var i = 0; i < 40; i++) points.Add((1d, i / 40d));
                for (var i = 40; i > 0; i--) points.Add((i / 40d, 1d));
                for (var i = 40; i > 0; i--) points.Add((0d, i / 40d));
                break;
            case "star":
                for (var i = 0; i < 64; i++)
                {
                    var angle = 2d * double.Pi * i / 64d; var radius = i % 2 == 0 ? 1d : 0.37d;
                    points.Add((radius * double.Cos(angle), radius * double.Sin(angle)));
                }
                break;
            case "degenerate-segment":
                for (var i = 0; i < 48; i++) points.Add((double.Cos(2d * double.Pi * i / 48d), double.Sin(2d * double.Pi * i / 48d)));
                points.Insert(10, points[10]);
                break;
            default:
                for (var i = 0; i < 20; i++) { points.Add((i * 0.1, 0d)); points.Add((i * 0.1 + 0.05, 1d)); }
                points.Add((2d, -0.5d)); points.Add((0d, -0.5d));
                break;
        }
        return points;
    }

    [Theory]
    [MemberData(nameof(Shapes))]
    public void IndexedContainmentMatchesTheReferenceLoop(string shape)
    {
        var polygon = Polygon(shape);
        var random = new Random(4242);
        var probes = new List<(double U, double V)>();
        // Vertices, edge midpoints, points just off edges, and random points across the bounds.
        for (var i = 0; i < polygon.Count; i++)
        {
            var a = polygon[i]; var b = polygon[(i + 1) % polygon.Count];
            probes.Add(a);
            probes.Add(((a.U + b.U) / 2d, (a.V + b.V) / 2d));
            foreach (var offset in new[] { 1e-10, 5e-10, 2e-9, 1e-6 })
                probes.Add(((a.U + b.U) / 2d + offset, (a.V + b.V) / 2d - offset));
        }
        for (var i = 0; i < 4000; i++) probes.Add((random.NextDouble() * 3d - 1.5d, random.NextDouble() * 3d - 1.5d));
        foreach (var probe in probes)
        {
            var (indexed, reference) = TrimmedSurfaceTessellator.ContainsPointBothWays(polygon, probe);
            Assert.True(indexed == reference, $"{shape}: ({probe.U:R}, {probe.V:R}) indexed={indexed} reference={reference}");
        }
    }
}
