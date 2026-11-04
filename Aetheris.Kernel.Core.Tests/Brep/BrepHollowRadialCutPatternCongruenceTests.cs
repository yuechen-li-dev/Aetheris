using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Brep.Features;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Step242;

namespace Aetheris.Kernel.Core.Tests.Brep;

/// <summary>Openings of one radius are realized once and placed rigidly, so identical holes carry identical
/// representations, and an exported pattern adds exactly N times what one opening adds.</summary>
public sealed class BrepHollowRadialCutPatternCongruenceTests
{
    private static readonly Lazy<(ThinWalledBodyRealization Host, HollowRadialCutPlacement[] Placements)> Grid = new(() =>
    {
        var host = ThinWalledBodyBRepPlanner.CreateCylinder(56, 142, 0.8).Value!;
        var placements = Enumerable.Range(0, 3).SelectMany(row => Enumerable.Range(0, 5).Select(column =>
            new HollowRadialCutPlacement($"r{row}c{column}", 4.5, 30 + row * 20, (9 + column * 72) * System.Math.PI / 180))).ToArray();
        return (host, placements);
    });

    [Fact]
    public void CongruentOpeningsShareOneRealizationAndStayOnTheirExactCurves()
    {
        var (host, placements) = Grid.Value;
        var built = BrepHollowRadialCutPattern.Build(host, placements);
        Assert.True(built.IsSuccess, string.Join("; ", built.Diagnostics.Select(d => d.Message)));
        var body = built.Value.Body;
        var curves = body.Geometry.Curves.Select(c => c.Value).Where(c => c.CertifiedIntersection is not null).Select(c => c.CertifiedIntersection!).ToArray();
        Assert.Equal(2 * placements.Length, curves.Length);

        // One representation shape per radius: same span count and control-point count for every outer and inner curve.
        Assert.Single(curves.Select(c => (c.SegmentCount, c.Curve.ControlPoints.Count)).Distinct());
        Assert.All(curves, c => Assert.Equal(curves[0].Curve.KnotValues, c.Curve.KnotValues));

        // A placed copy is still within its certified bound of its own exact intersection.
        foreach (var curve in curves)
        {
            var domain = curve.Authority.Domain;
            for (var i = 0; i <= 32; i++)
            {
                // The realized spline is parameterized by the exact curve's own parameter.
                var t = domain.Start + (domain.End - domain.Start) * i / 32d;
                var distance = (curve.Curve.Evaluate(t) - curve.Authority.Evaluate(t)).Length;
                Assert.True(distance <= curve.CertifiedDeviationBoundMm + 1e-12, $"deviation {distance} exceeds {curve.CertifiedDeviationBoundMm}");
            }
        }
    }

    [Fact]
    public void ExportedPatternEntitiesAreLinearInTheOpeningCount()
    {
        var (host, placements) = Grid.Value;
        var options = new Step242ExportOptions { ProductName = "p", ApplicationName = "test", BrepExportPreflightMode = BrepExportPreflightMode.Enforce, BrepExportPreflightPolicy = BrepExportPreflightPolicy.TrustedProductionRoute };
        IReadOnlyDictionary<string, int> Census(int n) =>
            Step242EntityCensus.Count(Step242Exporter.ExportBody(BrepHollowRadialCutPattern.Build(host, placements[..n]).Value!.Body, options).Value!);
        var one = Census(1); var two = Census(2);
        Assert.Null(Step242EntityCensus.LinearMismatch(one, two, Census(placements.Length), placements.Length));
    }

    [Fact]
    public void CensusNamesTheEntityTypeAMissingInstanceDropped()
    {
        var one = new Dictionary<string, int> { ["ADVANCED_FACE"] = 6, ["CYLINDRICAL_SURFACE"] = 3, ["CARTESIAN_POINT"] = 40 };
        var two = new Dictionary<string, int> { ["ADVANCED_FACE"] = 7, ["CYLINDRICAL_SURFACE"] = 4, ["CARTESIAN_POINT"] = 70 };
        var five = new Dictionary<string, int> { ["ADVANCED_FACE"] = 10, ["CYLINDRICAL_SURFACE"] = 6, ["CARTESIAN_POINT"] = 1 };
        var mismatch = Step242EntityCensus.LinearMismatch(one, two, five, 5);
        Assert.Equal("CYLINDRICAL_SURFACE: expected 7 for 5 congruent instances, found 6", mismatch);
        // Shared leaf geometry (points, directions, vectors) is deduplicated by the writer and not counted.
        var complete = new Dictionary<string, int> { ["ADVANCED_FACE"] = 10, ["CYLINDRICAL_SURFACE"] = 7, ["CARTESIAN_POINT"] = 1 };
        Assert.Null(Step242EntityCensus.LinearMismatch(one, two, complete, 5));
    }
}
